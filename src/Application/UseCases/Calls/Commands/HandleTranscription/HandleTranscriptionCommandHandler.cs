using System.Globalization;
using System.Text;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;
using SSW_x_Vonage_Clean_Architecture.Application.Common.Interfaces;

namespace SSW_x_Vonage_Clean_Architecture.Application.UseCases.Calls.Commands.HandleTranscription;

/// <summary>
/// Handler for processing transcription callbacks from Vonage.
/// Downloads the transcription from Vonage and saves it to OneDrive for Business.
/// </summary>
internal sealed class HandleTranscriptionCommandHandler : IRequestHandler<HandleTranscriptionCommand, ErrorOr<Success>>
{
    private readonly IVonageService _vonageService;
    private readonly IOneDriveService _oneDriveService;
    private readonly ILogger<HandleTranscriptionCommandHandler> _logger;

    public HandleTranscriptionCommandHandler(
        IVonageService vonageService,
        IOneDriveService oneDriveService,
        ILogger<HandleTranscriptionCommandHandler> logger)
    {
        _vonageService = vonageService;
        _oneDriveService = oneDriveService;
        _logger = logger;
    }

    public async Task<ErrorOr<Success>> Handle(HandleTranscriptionCommand request, CancellationToken cancellationToken)
    {
        var webhookRequest = request.Request;

        _logger.LogInformation(
            "Processing transcription callback for conversation {ConversationUuid}. Recording UUID: {RecordingUuid}",
            webhookRequest.ConversationUuid,
            webhookRequest.RecordingUuid);

        // Step 1: Download transcription from Vonage
        _logger.LogInformation("Downloading transcription from {TranscriptionUrl}", webhookRequest.TranscriptionUrl);

        var downloadResult = await _vonageService.DownloadTranscriptionAsync(
            webhookRequest.TranscriptionUrl,
            cancellationToken);

        if (downloadResult.IsError)
        {
            _logger.LogError(
                "Failed to download transcription for {RecordingUuid}: {Error}",
                webhookRequest.RecordingUuid,
                downloadResult.FirstError.Description);

            return downloadResult.FirstError;
        }

        var transcriptionResult = downloadResult.Value;

        // Step 2: Extract transcript text from the first channel
        var transcriptText = transcriptionResult.Channels[0].ExtractTranscript();

        if (string.IsNullOrWhiteSpace(transcriptText))
        {
            _logger.LogWarning(
                "No transcription text available for recording {RecordingUuid}",
                webhookRequest.RecordingUuid);

            return Error.Validation(
                "Transcription.NoText",
                "No transcription text available to save");
        }

        _logger.LogInformation(
            "Transcription downloaded successfully: {TextLength} characters",
            transcriptText.Length);

        // Step 3: Generate filename with timestamp and recording UUID
        var fileName = $"transcription_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{webhookRequest.RecordingUuid}.txt";
        var folderPath = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        // Step 4: Build transcription content with metadata
        var contentBuilder = new StringBuilder();
        contentBuilder.AppendLine("=== Vonage Call Transcription ===");
        contentBuilder.AppendLine(CultureInfo.InvariantCulture, $"Recording UUID: {webhookRequest.RecordingUuid}");
        contentBuilder.AppendLine(CultureInfo.InvariantCulture, $"Conversation UUID: {webhookRequest.ConversationUuid}");
        contentBuilder.AppendLine(CultureInfo.InvariantCulture, $"Duration: {transcriptionResult.Channels[0].Duration}s");
        contentBuilder.AppendLine();
        contentBuilder.AppendLine("=== Transcription Text ===");
        contentBuilder.Append(transcriptText);

        // Step 5: Convert text to stream
        var textBytes = Encoding.UTF8.GetBytes(contentBuilder.ToString());
        await using var textStream = new MemoryStream(textBytes);

        _logger.LogInformation(
            "Uploading transcription to OneDrive: {FileName} in folder {FolderPath}",
            fileName,
            folderPath);

        // Step 6: Upload to OneDrive
        var uploadResult = await _oneDriveService.UploadFileFromStreamAsync(
            textStream,
            fileName,
            folderPath,
            cancellationToken);

        if (uploadResult.IsError)
        {
            _logger.LogError(
                "Failed to upload transcription {RecordingUuid} to OneDrive: {Error}",
                webhookRequest.RecordingUuid,
                uploadResult.FirstError.Description);

            return uploadResult.FirstError;
        }

        _logger.LogInformation(
            "Transcription {RecordingUuid} uploaded successfully to OneDrive with file ID: {FileId}",
            webhookRequest.RecordingUuid,
            uploadResult.Value);

        return Result.Success;
    }
}
