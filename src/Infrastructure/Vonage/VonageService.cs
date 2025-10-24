using System.Text.Json;
using ErrorOr;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SSW_x_Vonage_Clean_Architecture.Application.Common.Interfaces;
using SSW_x_Vonage_Clean_Architecture.Application.UseCases.Calls.Commands.HandleTranscription;
using Vonage;
using Vonage.Common.Failures;
using Vonage.Messages;
using Vonage.Messages.Sms;
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

    public async Task<ErrorOr<TranscriptionResult>> DownloadTranscriptionAsync(string transcriptionUrl, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("VonageService: Downloading transcription from {TranscriptionUrl}", transcriptionUrl);

        try
        {
            // Generate JWT token for authentication
            var jwt = new Jwt();
            var tokenResult = jwt.GenerateToken(_vonageClient.Credentials);

            if (tokenResult.IsFailure)
            {
                _logger.LogError("VonageService: Failed to generate JWT token");
                return Error.Failure("Vonage.JwtGenerationFailed", "Failed to generate JWT token");
            }

            var token = tokenResult.Match(
                success => success,
                failure => throw new InvalidOperationException("Token generation failed"));

            // Create request with JWT authentication
            using var request = new HttpRequestMessage(HttpMethod.Get, transcriptionUrl);
            request.Headers.Add("Authorization", $"Bearer {token}");

            // Execute request
            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "VonageService: Failed to download transcription from {TranscriptionUrl}. Status: {StatusCode}",
                    transcriptionUrl,
                    response.StatusCode);

                return Error.Failure(
                    "Vonage.TranscriptionDownloadFailed",
                    $"Failed to download transcription. Status: {response.StatusCode}");
            }

            // Read and parse JSON
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(json))
            {
                _logger.LogError("VonageService: Empty transcription response received");
                return Error.Failure("Vonage.EmptyTranscription", "Empty transcription response received");
            }

            var transcription = JsonSerializer.Deserialize<TranscriptionResult>(json);

            if (transcription is null)
            {
                _logger.LogError("VonageService: Failed to deserialize transcription JSON");
                return Error.Failure("Vonage.DeserializationFailed", "Failed to deserialize transcription JSON");
            }

            if (transcription.Channels is null || transcription.Channels.Count == 0)
            {
                _logger.LogError("VonageService: Invalid transcription format - no channels found");
                return Error.Failure("Vonage.InvalidFormat", "Invalid transcription format: no channels found");
            }

            _logger.LogInformation(
                "VonageService: Transcription downloaded successfully. Channels: {ChannelCount}",
                transcription.Channels.Count);

            return transcription;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "VonageService: Network error downloading transcription from {TranscriptionUrl}", transcriptionUrl);
            return Error.Failure("Vonage.NetworkError", $"Network error: {ex.Message}");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "VonageService: JSON parsing error for transcription from {TranscriptionUrl}", transcriptionUrl);
            return Error.Failure("Vonage.JsonError", $"JSON parsing error: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "VonageService: Unexpected error downloading transcription from {TranscriptionUrl}", transcriptionUrl);
            return Error.Failure("Vonage.UnexpectedError", $"Unexpected error: {ex.Message}");
        }
    }

    public async Task<ErrorOr<string>> SendSmsAsync(string toPhoneNumber, string messageText, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "VonageService: Sending SMS to {PhoneNumber}. Message length: {Length} characters",
            toPhoneNumber,
            messageText.Length);

        try
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(toPhoneNumber))
            {
                _logger.LogError("VonageService: Invalid phone number - null or empty");
                return Error.Validation("Vonage.InvalidPhoneNumber", "Phone number cannot be null or empty");
            }

            if (string.IsNullOrWhiteSpace(messageText))
            {
                _logger.LogError("VonageService: Invalid message text - null or empty");
                return Error.Validation("Vonage.InvalidMessageText", "Message text cannot be null or empty");
            }

            // Ensure we have a configured FromNumber for SMS
            if (string.IsNullOrWhiteSpace(_settings.FromNumber))
            {
                _logger.LogError("VonageService: FromNumber is not configured in VonageSettings");
                return Error.Failure("Vonage.ConfigurationError", "SMS sender number (FromNumber) is not configured");
            }

            // Create SMS request using Vonage SDK
            var smsRequest = new SmsRequest
            {
                From = _settings.FromNumber,
                To = toPhoneNumber,
                Text = messageText
            };

            // Send SMS using Vonage Messages API
            var messagesClient = _vonageClient.MessagesClient;
            var response = await messagesClient.SendAsync(smsRequest);

            // The Vonage SDK returns a SendMessageResponse
            // Extract the message UUID which serves as the message ID
            var messageId = response.MessageUuid.ToString();

            _logger.LogInformation(
                "VonageService: SMS sent successfully to {PhoneNumber}. Message ID: {MessageId}",
                toPhoneNumber,
                messageId);

            return messageId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "VonageService: Error sending SMS to {PhoneNumber}", toPhoneNumber);
            return Error.Failure("Vonage.SmsSendFailed", $"Failed to send SMS: {ex.Message}");
        }
    }

    public async Task<ErrorOr<CallInfo>> GetCallByConversationUuidAsync(string conversationUuid, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("VonageService: Retrieving call information for conversation {ConversationUuid}", conversationUuid);

        try
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(conversationUuid))
            {
                _logger.LogError("VonageService: Invalid conversation UUID - null or empty");
                return Error.Validation("Vonage.InvalidConversationUuid", "Conversation UUID cannot be null or empty");
            }

            // Search for calls by conversation UUID using Vonage Voice API
            var searchFilter = new CallSearchFilter
            {
                ConversationUuid = conversationUuid
            };

            var callsResponse = await _vonageClient.VoiceClient.GetCallsAsync(searchFilter);

            // Check if any calls were found
            if (callsResponse?.Embedded?.Calls == null || callsResponse.Embedded.Calls.Count == 0)
            {
                _logger.LogWarning(
                    "VonageService: No calls found for conversation {ConversationUuid}",
                    conversationUuid);

                return Error.NotFound(
                    "Vonage.CallNotFound",
                    $"No call found for conversation UUID: {conversationUuid}");
            }

            // Get the first call (there should typically only be one per conversation UUID)
            var call = callsResponse.Embedded.Calls.First();

            // Extract call information
            var toPhoneNumber = call.To?.Number ?? string.Empty;
            var fromPhoneNumber = call.From?.Number;

            // Duration is a string in the Vonage SDK (e.g., "30") - parse to int
            var durationSeconds = 0;
            if (!string.IsNullOrWhiteSpace(call.Duration) && int.TryParse(call.Duration, out var parsedDuration))
            {
                durationSeconds = parsedDuration;
            }

            if (string.IsNullOrWhiteSpace(toPhoneNumber))
            {
                _logger.LogError(
                    "VonageService: Call found but 'To' phone number is missing for conversation {ConversationUuid}",
                    conversationUuid);

                return Error.Failure(
                    "Vonage.MissingPhoneNumber",
                    "Call record found but recipient phone number is missing");
            }

            var callInfo = new CallInfo(toPhoneNumber, fromPhoneNumber, durationSeconds);

            _logger.LogInformation(
                "VonageService: Call information retrieved successfully. To: {ToNumber}, Duration: {Duration}s",
                toPhoneNumber,
                durationSeconds);

            return callInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "VonageService: Error retrieving call for conversation {ConversationUuid}", conversationUuid);
            return Error.Failure("Vonage.GetCallFailed", $"Failed to retrieve call information: {ex.Message}");
        }
    }
}
