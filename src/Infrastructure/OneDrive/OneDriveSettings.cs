namespace SSW_x_Vonage_Clean_Architecture.Infrastructure.OneDrive;

/// <summary>
/// Configuration settings for OneDrive integration via Microsoft Graph API
/// </summary>
public sealed class OneDriveSettings
{
    /// <summary>
    /// Configuration section name in appsettings.json
    /// </summary>
    public const string SectionName = "OneDrive";

    /// <summary>
    /// Azure AD Tenant ID (Directory ID)
    /// </summary>
    public required string TenantId { get; init; }

    /// <summary>
    /// Azure AD Application (Client) ID
    /// </summary>
    public required string ClientId { get; init; }

    /// <summary>
    /// Root folder path in OneDrive for uploads
    /// Example: "CallRecordings" will create /CallRecordings/ folder
    /// </summary>
    public required string UploadFolderPath { get; init; }
}
