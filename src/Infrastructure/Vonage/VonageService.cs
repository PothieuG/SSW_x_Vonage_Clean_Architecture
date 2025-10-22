using ErrorOr;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SSW_x_Vonage_Clean_Architecture.Application.Common.Interfaces;
using Vonage;
using Vonage.Request;
using Vonage.Voice;
using Vonage.Voice.Nccos;
using Vonage.Voice.Nccos.Endpoints;

namespace SSW_x_Vonage_Clean_Architecture.Infrastructure.Vonage;

internal sealed class VonageService : IVonageService
{
    private readonly ILogger<VonageService> _logger;
    private readonly VonageClient _vonageClient;
    private readonly VonageSettings _settings;
    private readonly HttpClient _httpClient;

    public VonageService(
        ILogger<VonageService> logger,
        IOptions<VonageSettings> settings,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _settings = settings.Value;
        _httpClient = httpClientFactory.CreateClient();

        // Get the private key content (either from file or direct content)
        var privateKey = GetPrivateKeyContent(_settings.ApplicationKey);

        // Initialize Vonage client with credentials
        var credentials = Credentials.FromAppIdAndPrivateKey(
            _settings.ApplicationId,
            privateKey);

        _vonageClient = new VonageClient(credentials);
    }

    private static string GetPrivateKeyContent(string applicationKey)
    {
        // Check if the applicationKey is a file path
        if (File.Exists(applicationKey))
        {
            return File.ReadAllText(applicationKey);
        }

        // Otherwise, treat it as the actual private key content
        return applicationKey;
    }

    public async Task<string> InitiateCallAsync(string phoneNumber, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("VonageService: Initiating call from {FromNumber} to {PhoneNumber}",
            _settings.FromNumber, phoneNumber);

        try
        {
            // Ensure webhook base URL is properly formatted
            var webhookBaseUrl = _settings.WebhookBaseUrl.TrimEnd('/');

            // Create RecordAction with transcription
            var recordAction = new RecordAction
            {
                EventUrl = [$"{webhookBaseUrl}/api/calls/recorded"],
                EndOnSilence = "3",
                BeepStart = true,
                Transcription = new RecordAction.TranscriptionSettings
                {
                    EventUrl = [$"{webhookBaseUrl}/api/calls/transcribed"],
                    Language = "fr-FR"
                }
            };

            // Create NCCO (Nexmo Call Control Object) to define call behavior
            var ncco = new Ncco(
                new TalkAction
                {
                    Text = "Bonjour, veuillez laisser un message après le bip svp.",
                    Language = "fr-FR",
                    Style = 0 // Female voice
                },
                recordAction
            );

            // Create call request
            var callRequest = new CallCommand
            {
                To = [new PhoneEndpoint { Number = phoneNumber }],
                From = new PhoneEndpoint { Number = _settings.FromNumber },
                Ncco = ncco
            };

            // Initiate the call
            var response = await _vonageClient.VoiceClient.CreateCallAsync(callRequest);

            _logger.LogInformation("VonageService: Call initiated successfully with UUID {CallUuid}", response.Uuid);

            return response.Uuid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "VonageService: Failed to initiate call to {PhoneNumber}", phoneNumber);
            throw;
        }
    }

    public async Task<ErrorOr<Stream>> DownloadRecordingAsync(string recordingUrl, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("VonageService: Downloading recording from {RecordingUrl}", recordingUrl);

        try
        {
            // Generate JWT token for authentication using Vonage's JWT library
            var jwt = new Jwt();
            var tokenResult = jwt.GenerateToken(_vonageClient.Credentials);

            if (tokenResult.IsFailure)
            {
                _logger.LogError("VonageService: Failed to generate JWT token");
                return Error.Failure("Vonage.JwtGenerationFailed", "Failed to generate JWT token");
            }

            // Use Match or GetSuccessUnsafe to get the token value
            var token = tokenResult.Match(
                success => success,
                failure => throw new InvalidOperationException("Token generation failed"));

            // Create request with JWT authentication
            using var request = new HttpRequestMessage(HttpMethod.Get, recordingUrl);
            request.Headers.Add("Authorization", $"Bearer {token}");

            // Execute request
            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "VonageService: Failed to download recording from {RecordingUrl}. Status: {StatusCode}",
                    recordingUrl,
                    response.StatusCode);

                return Error.Failure(
                    "Vonage.DownloadFailed",
                    $"Failed to download recording. Status: {response.StatusCode}");
            }

            // Copy HTTP stream to MemoryStream to make it seekable
            // This is required because OneDrive upload needs to know the stream length
            var memoryStream = new MemoryStream();
            await using var httpStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await httpStream.CopyToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0; // Reset to beginning for reading

            _logger.LogInformation(
                "VonageService: Recording downloaded successfully from {RecordingUrl}. Size: {Size} bytes",
                recordingUrl,
                memoryStream.Length);

            return memoryStream;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "VonageService: Network error downloading recording from {RecordingUrl}", recordingUrl);
            return Error.Failure("Vonage.NetworkError", $"Network error: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "VonageService: Unexpected error downloading recording from {RecordingUrl}", recordingUrl);
            return Error.Failure("Vonage.UnexpectedError", $"Unexpected error: {ex.Message}");
        }
    }
}
