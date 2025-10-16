namespace SSW_x_Vonage_Clean_Architecture.Infrastructure.Vonage;

/// <summary>
/// Configuration settings for Vonage API integration.
/// </summary>
public sealed class VonageSettings
{
    /// <summary>
    /// The configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "Vonage";

    /// <summary>
    /// Vonage Application ID.
    /// </summary>
    public required string ApplicationId { get; init; }

    /// <summary>
    /// Vonage Application Private Key (can be path to file or key content).
    /// </summary>
    public required string ApplicationKey { get; init; }

    /// <summary>
    /// The phone number that will initiate the call (must be a Vonage virtual number).
    /// </summary>
    public required string FromNumber { get; init; }

    /// <summary>
    /// The public URL where Vonage will send webhook callbacks (e.g., for recordings and transcriptions).
    /// Must be publicly accessible. For local development, use ngrok or similar tunneling service.
    /// </summary>
    public required string WebhookBaseUrl { get; init; }
}
