using ErrorOr;

namespace SSW_x_Vonage_Clean_Architecture.Application.Common.Interfaces;

/// <summary>
/// Service for uploading files to OneDrive for Business using Microsoft Graph API
/// </summary>
public interface IOneDriveService
{
    /// <summary>
    /// Uploads a file to OneDrive from a URL (e.g., Vonage recording URL)
    /// </summary>
    /// <param name="fileUrl">URL of the file to download and upload to OneDrive</param>
    /// <param name="fileName">Desired file name in OneDrive (e.g., "recording_20250115_093022.mp3")</param>
    /// <param name="folderPath">Optional subfolder path relative to the configured root folder (e.g., "2025-01-15")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>OneDrive file ID on success, or error if upload fails</returns>
    Task<ErrorOr<string>> UploadFileFromUrlAsync(
        string fileUrl,
        string fileName,
        string? folderPath = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads a file to OneDrive from a stream
    /// </summary>
    /// <param name="fileStream">Stream containing the file data</param>
    /// <param name="fileName">Desired file name in OneDrive (e.g., "recording_20250115_093022.mp3")</param>
    /// <param name="folderPath">Optional subfolder path relative to the configured root folder (e.g., "2025-01-15")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>OneDrive file ID on success, or error if upload fails</returns>
    Task<ErrorOr<string>> UploadFileFromStreamAsync(
        Stream fileStream,
        string fileName,
        string? folderPath = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the web URL for a OneDrive folder that can be shared with users
    /// </summary>
    /// <param name="folderPath">Optional subfolder path relative to the configured root folder (e.g., "2025-01-15")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Web URL to the folder on success, or error if retrieval fails</returns>
    Task<ErrorOr<string>> GetFolderWebUrlAsync(
        string? folderPath = null,
        CancellationToken cancellationToken = default);
}
