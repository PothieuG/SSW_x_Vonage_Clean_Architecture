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

    public VonageService(
        ILogger<VonageService> logger,
        IOptions<VonageSettings> settings)
    {
        _logger = logger;
        _settings = settings.Value;

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
}
