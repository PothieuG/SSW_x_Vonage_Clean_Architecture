using System.Globalization;
using MediatR;
using Microsoft.Extensions.Logging;
using SSW_x_Vonage_Clean_Architecture.Application.Common.Interfaces;

namespace SSW_x_Vonage_Clean_Architecture.Application.UseCases.Calls.Commands.HandleRecording;

/// <summary>
/// Handler for processing recording callbacks from Vonage.
/// Downloads the recording and uploads it to OneDrive for Business.
/// </summary>
internal sealed class HandleRecordingCommandHandler : IRequestHandler<HandleRecordingCommand, ErrorOr<Success>>
{
    private readonly IOneDriveService _oneDriveService;
    private readonly ILogger<HandleRecordingCommandHandler> _logger;

    public HandleRecordingCommandHandler(
        IOneDriveService oneDriveService,
        ILogger<HandleRecordingCommandHandler> logger)
    {
        _oneDriveService = oneDriveService;
        _logger = logger;
    }

    public async Task<ErrorOr<Success>> Handle(HandleRecordingCommand request, CancellationToken cancellationToken)
    {
        var recordingRequest = request.Request;

        _logger.LogInformation(
            "Processing recording callback for conversation {ConversationUuid}. Recording UUID: {RecordingUuid}, Size: {Size} bytes",
            recordingRequest.ConversationUuid,
            recordingRequest.RecordingUuid,
            recordingRequest.Size);

        // Generate filename with timestamp and recording UUID
        var startTime = DateTime.Parse(recordingRequest.StartTime, CultureInfo.InvariantCulture);
        var fileName = $"recording_{startTime:yyyyMMdd_HHmmss}_{recordingRequest.RecordingUuid}.mp3";

        // Create dated subfolder (e.g., "2025-01-17")
        var folderPath = startTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        _logger.LogInformation(
            "Uploading recording to OneDrive: {FileName} in folder {FolderPath}",
            fileName,
            folderPath);

        // Upload recording to OneDrive
        var uploadResult = await _oneDriveService.UploadFileFromUrlAsync(
            recordingRequest.RecordingUrl,
            fileName,
            folderPath,
            cancellationToken);

        if (uploadResult.IsError)
        {
            _logger.LogError(
                "Failed to upload recording {RecordingUuid} to OneDrive: {Error}",
                recordingRequest.RecordingUuid,
                uploadResult.FirstError.Description);

            return uploadResult.FirstError;
        }

        _logger.LogInformation(
            "Recording {RecordingUuid} uploaded successfully to OneDrive with file ID: {FileId}",
            recordingRequest.RecordingUuid,
            uploadResult.Value);

        return Result.Success;
    }
}
