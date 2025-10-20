using Azure.Identity;
using ErrorOr;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Drives.Item.Items.Item.CreateUploadSession;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using SSW_x_Vonage_Clean_Architecture.Application.Common.Interfaces;

namespace SSW_x_Vonage_Clean_Architecture.Infrastructure.OneDrive;

/// <summary>
/// OneDrive service implementation using Microsoft Graph API
/// Handles file uploads to OneDrive for Business
/// </summary>
internal sealed class OneDriveService : IOneDriveService
{
    private readonly OneDriveSettings _settings;
    private readonly ILogger<OneDriveService> _logger;
    private readonly GraphServiceClient _graphClient;
    private readonly HttpClient _httpClient;

    public OneDriveService(
        IOptions<OneDriveSettings> settings,
        ILogger<OneDriveService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _settings = settings.Value;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();

        // Initialize Graph Client with Client Credentials authentication
        var credential = new ClientSecretCredential(
            _settings.TenantId,
            _settings.ClientId,
            _settings.ClientSecret);

        _graphClient = new GraphServiceClient(credential);
    }

    /// <inheritdoc />
    public async Task<ErrorOr<string>> UploadFileFromUrlAsync(
        string fileUrl,
        string fileName,
        string? folderPath = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Downloading file from URL: {FileUrl}", fileUrl);

            // Download file from URL
            using var response = await _httpClient.GetAsync(fileUrl, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to download file from {FileUrl}. Status: {StatusCode}",
                    fileUrl, response.StatusCode);
                return Error.Failure(
                    "OneDrive.DownloadFailed",
                    $"Failed to download file from URL. Status: {response.StatusCode}");
            }

            await using var fileStream = await response.Content.ReadAsStreamAsync(cancellationToken);

            // Upload to OneDrive
            return await UploadFileFromStreamAsync(fileStream, fileName, folderPath, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error downloading file from {FileUrl}", fileUrl);
            return Error.Failure("OneDrive.NetworkError", $"Network error: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error uploading file from URL to OneDrive");
            return Error.Failure("OneDrive.UnexpectedError", $"Unexpected error: {ex.Message}");
        }
    }

    /// <summary>
    /// Internal method to upload a file from a stream
    /// </summary>
    private async Task<ErrorOr<string>> UploadFileFromStreamAsync(
        Stream fileStream,
        string fileName,
        string? folderPath = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Build full path: /RootFolder/OptionalSubfolder/filename.ext
            var fullPath = string.IsNullOrWhiteSpace(folderPath)
                ? $"{_settings.UploadFolderPath}/{fileName}"
                : $"{_settings.UploadFolderPath}/{folderPath}/{fileName}";

            _logger.LogInformation("Uploading file to OneDrive path: {FullPath}", fullPath);

            // Get user's drive
            var drive = await GetUserDriveAsync(cancellationToken);
            if (drive.IsError)
            {
                return drive.Errors;
            }

            // Ensure folder exists
            var folder = await EnsureFolderExistsAsync(
                drive.Value,
                _settings.UploadFolderPath,
                folderPath,
                cancellationToken);

            if (folder.IsError)
            {
                return folder.Errors;
            }

            // Decide upload method based on file size
            const long maxSimpleUploadSize = 4 * 1024 * 1024; // 4MB threshold

            if (fileStream.Length < maxSimpleUploadSize)
            {
                // Simple upload for small files
                return await SimpleUploadAsync(drive.Value, fullPath, fileStream, cancellationToken);
            }
            else
            {
                // Chunked upload for large files
                return await ChunkedUploadAsync(drive.Value, fullPath, fileStream, cancellationToken);
            }
        }
        catch (ODataError ex)
        {
            _logger.LogError(ex, "Microsoft Graph API error: {ErrorCode} - {ErrorMessage}",
                ex.Error?.Code, ex.Error?.Message);
            return Error.Failure(
                $"OneDrive.GraphError.{ex.Error?.Code ?? "Unknown"}",
                ex.Error?.Message ?? ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error uploading file to OneDrive");
            return Error.Failure("OneDrive.UnexpectedError", $"Unexpected error: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the user's OneDrive
    /// </summary>
    private async Task<ErrorOr<Drive>> GetUserDriveAsync(CancellationToken cancellationToken)
    {
        try
        {
            var drive = await _graphClient.Users[_settings.UserId]
                .Drive
                .GetAsync(cancellationToken: cancellationToken);

            if (drive is null)
            {
                _logger.LogError("User {UserId} does not have a OneDrive", _settings.UserId);
                return Error.NotFound(
                    "OneDrive.UserDriveNotFound",
                    $"User {_settings.UserId} does not have a OneDrive");
            }

            _logger.LogDebug("Retrieved drive {DriveId} for user {UserId}", drive.Id, _settings.UserId);
            return drive;
        }
        catch (ODataError ex) when (ex.Error?.Code == "ResourceNotFound")
        {
            _logger.LogError("User {UserId} not found in Azure AD", _settings.UserId);
            return Error.NotFound("OneDrive.UserNotFound", $"User {_settings.UserId} not found");
        }
        catch (ODataError ex)
        {
            _logger.LogError(ex, "Failed to retrieve user drive: {ErrorCode}", ex.Error?.Code);
            return Error.Failure(
                $"OneDrive.GetDriveFailed.{ex.Error?.Code}",
                ex.Error?.Message ?? ex.Message);
        }
    }

    /// <summary>
    /// Ensures the folder structure exists in OneDrive, creating folders if necessary
    /// </summary>
    private async Task<ErrorOr<DriveItem>> EnsureFolderExistsAsync(
        Drive drive,
        string rootFolder,
        string? subFolder,
        CancellationToken cancellationToken)
    {
        try
        {
            // Create root folder if it doesn't exist
            var rootFolderItem = await GetOrCreateFolderAsync(drive.Id!, "root", rootFolder, cancellationToken);
            if (rootFolderItem.IsError)
            {
                return rootFolderItem.Errors;
            }

            // If no subfolder, return root folder
            if (string.IsNullOrWhiteSpace(subFolder))
            {
                return rootFolderItem.Value;
            }

            // Create subfolder
            var subFolderItem = await GetOrCreateFolderAsync(
                drive.Id!,
                rootFolderItem.Value.Id!,
                subFolder,
                cancellationToken);

            return subFolderItem;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure folder exists");
            return Error.Failure("OneDrive.FolderCreationFailed", ex.Message);
        }
    }

    /// <summary>
    /// Gets an existing folder or creates it if it doesn't exist
    /// </summary>
    private async Task<ErrorOr<DriveItem>> GetOrCreateFolderAsync(
        string driveId,
        string parentId,
        string folderName,
        CancellationToken cancellationToken)
    {
        try
        {
            // Try to get existing folder
            DriveItem? existingFolder;
            if (parentId == "root")
            {
                existingFolder = await _graphClient.Drives[driveId]
                    .Root
                    .ItemWithPath(folderName)
                    .GetAsync(cancellationToken: cancellationToken);
            }
            else
            {
                existingFolder = await _graphClient.Drives[driveId]
                    .Items[parentId]
                    .ItemWithPath(folderName)
                    .GetAsync(cancellationToken: cancellationToken);
            }

            if (existingFolder is not null)
            {
                _logger.LogDebug("Folder {FolderName} already exists", folderName);
                return existingFolder;
            }
        }
        catch (ODataError ex) when (ex.Error?.Code == "itemNotFound")
        {
            // Folder doesn't exist, create it
            _logger.LogDebug("Folder {FolderName} not found, creating...", folderName);
        }

        // Create folder
        var newFolder = new DriveItem
        {
            Name = folderName,
            Folder = new Folder(),
            AdditionalData = new Dictionary<string, object>
            {
                { "@microsoft.graph.conflictBehavior", "rename" }
            }
        };

        DriveItem? createdFolder;
        if (parentId == "root")
        {
            createdFolder = await _graphClient.Drives[driveId]
                .Items
                .PostAsync(newFolder, cancellationToken: cancellationToken);
        }
        else
        {
            createdFolder = await _graphClient.Drives[driveId]
                .Items[parentId]
                .Children
                .PostAsync(newFolder, cancellationToken: cancellationToken);
        }

        if (createdFolder is null)
        {
            return Error.Failure("OneDrive.FolderCreationFailed", $"Failed to create folder {folderName}");
        }

        _logger.LogInformation("Created folder {FolderName} with ID {FolderId}", folderName, createdFolder.Id);
        return createdFolder;
    }

    /// <summary>
    /// Simple upload for files smaller than 4MB
    /// </summary>
    private async Task<ErrorOr<string>> SimpleUploadAsync(
        Drive drive,
        string itemPath,
        Stream fileStream,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Using simple upload for file {ItemPath}", itemPath);

        var uploadedItem = await _graphClient.Drives[drive.Id]
            .Root
            .ItemWithPath(itemPath)
            .Content
            .PutAsync(fileStream, cancellationToken: cancellationToken);

        if (uploadedItem?.Id is null)
        {
            return Error.Failure("OneDrive.UploadFailed", "Failed to upload file");
        }

        _logger.LogInformation("File uploaded successfully with ID: {FileId}", uploadedItem.Id);
        return uploadedItem.Id;
    }

    /// <summary>
    /// Chunked upload for files larger than 4MB
    /// </summary>
    private async Task<ErrorOr<string>> ChunkedUploadAsync(
        Drive drive,
        string itemPath,
        Stream fileStream,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Using chunked upload for file {ItemPath} (size: {FileSize} bytes)",
            itemPath, fileStream.Length);

        // Create upload session
        var uploadSessionRequestBody = new CreateUploadSessionPostRequestBody
        {
            Item = new DriveItemUploadableProperties
            {
                AdditionalData = new Dictionary<string, object>
                {
                    { "@microsoft.graph.conflictBehavior", "replace" }
                }
            }
        };

        var uploadSession = await _graphClient.Drives[drive.Id]
            .Root
            .ItemWithPath(itemPath)
            .CreateUploadSession
            .PostAsync(uploadSessionRequestBody, cancellationToken: cancellationToken);

        if (uploadSession is null)
        {
            return Error.Failure("OneDrive.UploadSessionFailed", "Failed to create upload session");
        }

        // Max slice size must be a multiple of 320 KiB
        const int maxSliceSize = 320 * 1024 * 10; // 3.2 MB chunks
        var fileUploadTask = new LargeFileUploadTask<DriveItem>(uploadSession, fileStream, maxSliceSize);

        // Upload with progress tracking
        var totalLength = fileStream.Length;
        IProgress<long> progress = new Progress<long>(bytesUploaded =>
        {
            var percentage = bytesUploaded * 100 / totalLength;
            _logger.LogDebug("Upload progress: {Percentage}% ({BytesUploaded}/{TotalBytes} bytes)",
                percentage, bytesUploaded, totalLength);
        });

        try
        {
            var uploadResult = await fileUploadTask.UploadAsync(progress);

            if (uploadResult.UploadSucceeded && uploadResult.ItemResponse?.Id is not null)
            {
                _logger.LogInformation("Chunked upload completed successfully. File ID: {FileId}",
                    uploadResult.ItemResponse.Id);
                return uploadResult.ItemResponse.Id;
            }

            return Error.Failure("OneDrive.ChunkedUploadFailed", "Chunked upload did not complete successfully");
        }
        catch (ODataError ex)
        {
            _logger.LogError(ex, "Chunked upload failed: {ErrorCode}", ex.Error?.Code);
            return Error.Failure($"OneDrive.ChunkedUploadError.{ex.Error?.Code}", ex.Error?.Message ?? ex.Message);
        }
    }
}
