using System.Globalization;
using System.Text;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;
using SSW_x_Vonage_Clean_Architecture.Application.Common.Interfaces;

namespace SSW_x_Vonage_Clean_Architecture.Application.UseCases.Calls.Commands.HandleTranscription;

internal sealed class HandleTranscriptionCommandHandler : IRequestHandler<HandleTranscriptionCommand, ErrorOr<Success>>
{
    private readonly IVonageService _vonageService;
    private readonly IOneDriveService _oneDriveService;
    private readonly IMcpService _mcpService;
    private readonly ILogger<HandleTranscriptionCommandHandler> _logger;

    public HandleTranscriptionCommandHandler(
        IVonageService vonageService,
        IOneDriveService oneDriveService,
        IMcpService mcpService,
        ILogger<HandleTranscriptionCommandHandler> logger)
    {
        _vonageService = vonageService;
        _oneDriveService = oneDriveService;
        _mcpService = mcpService;
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

        // Step 3: NOUVEAU - Traitement intelligent avec MCP
        _logger.LogInformation("Starting intelligent MCP processing of transcript");
        
        var processingResult = await _mcpService.ProcessTranscriptWithMcpAsync(transcriptText, cancellationToken);
        
        if (processingResult.IsError)
        {
            _logger.LogWarning(
                "MCP processing failed for {RecordingUuid}: {Error}. Saving original transcript only.",
                webhookRequest.RecordingUuid,
                processingResult.FirstError.Description);
            
            // On sauvegarde quand même le transcript original en cas d'erreur
            return await SaveTranscriptOnlyAsync(webhookRequest, transcriptionResult, transcriptText, cancellationToken);
        }

        var processedContent = processingResult.Value;

        // Step 4: Generate filenames with timestamp and recording UUID
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        var originalFileName = $"transcription_{timestamp}_{webhookRequest.RecordingUuid}.txt";
        var processedFileName = $"processed_{timestamp}_{webhookRequest.RecordingUuid}.txt";
        var folderPath = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        // Step 5: Build file contents
        var originalContent = BuildOriginalContent(webhookRequest, transcriptionResult, transcriptText);
        var processedContentWithMetadata = BuildProcessedContent(webhookRequest, transcriptionResult, processedContent);

        // Step 6: Upload both files to OneDrive
        var uploadTasks = new[]
        {
            UploadToOnedriveAsync(originalContent, originalFileName, folderPath, cancellationToken),
            UploadToOnedriveAsync(processedContentWithMetadata, processedFileName, folderPath, cancellationToken)
        };

        var uploadResults = await Task.WhenAll(uploadTasks);

        // Vérifier les résultats d'upload
        foreach (var uploadResult in uploadResults)
        {
            if (uploadResult.IsError)
            {
                _logger.LogError(
                    "Failed to upload file for {RecordingUuid}: {Error}",
                    webhookRequest.RecordingUuid,
                    uploadResult.FirstError.Description);

                return uploadResult.FirstError;
            }
        }

        _logger.LogInformation(
            "Transcription and processed content for {RecordingUuid} uploaded successfully to OneDrive",
            webhookRequest.RecordingUuid);

        return Result.Success;
    }

    private async Task<ErrorOr<Success>> SaveTranscriptOnlyAsync(
        TranscriptionCallbackRequest webhookRequest,
        TranscriptionResult transcriptionResult,
        string transcriptText,
        CancellationToken cancellationToken)
    {
        var fileName = $"transcription_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{webhookRequest.RecordingUuid}.txt";
        var folderPath = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var content = BuildOriginalContent(webhookRequest, transcriptionResult, transcriptText);
        return await UploadToOnedriveAsync(content, fileName, folderPath, cancellationToken);
    }

    private static string BuildOriginalContent(
        TranscriptionCallbackRequest webhookRequest,
        TranscriptionResult transcriptionResult,
        string transcriptText)
    {
        var contentBuilder = new StringBuilder();
        contentBuilder.AppendLine("=== Vonage Call Transcription ===");
        contentBuilder.AppendLine(CultureInfo.InvariantCulture, $"Recording UUID: {webhookRequest.RecordingUuid}");
        contentBuilder.AppendLine(CultureInfo.InvariantCulture, $"Conversation UUID: {webhookRequest.ConversationUuid}");
        contentBuilder.AppendLine(CultureInfo.InvariantCulture, $"Duration: {GetDuration(transcriptionResult)}s");
        contentBuilder.AppendLine();
        contentBuilder.AppendLine("=== Original Transcription Text ===");
        contentBuilder.Append(transcriptText);

        return contentBuilder.ToString();
    }

    private static string BuildProcessedContent(
        TranscriptionCallbackRequest webhookRequest,
        TranscriptionResult transcriptionResult,
        string processedContent)
    {
        var contentBuilder = new StringBuilder();
        contentBuilder.AppendLine("=== Processed Transcription (AI Summary/Translation) ===");
        contentBuilder.AppendLine(CultureInfo.InvariantCulture, $"Recording UUID: {webhookRequest.RecordingUuid}");
        contentBuilder.AppendLine(CultureInfo.InvariantCulture, $"Conversation UUID: {webhookRequest.ConversationUuid}");
        contentBuilder.AppendLine(CultureInfo.InvariantCulture, $"Original Duration: {GetDuration(transcriptionResult)}s");
        contentBuilder.AppendLine(CultureInfo.InvariantCulture, $"Processed At: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        contentBuilder.AppendLine();
        contentBuilder.AppendLine("=== AI Processed Content ===");
        contentBuilder.Append(processedContent);

        return contentBuilder.ToString();
    }

    private async Task<ErrorOr<Success>> UploadToOnedriveAsync(
        string content,
        string fileName,
        string folderPath,
        CancellationToken cancellationToken)
    {
        var textBytes = Encoding.UTF8.GetBytes(content);
        await using var textStream = new MemoryStream(textBytes);

        var uploadResult = await _oneDriveService.UploadFileFromStreamAsync(
            textStream,
            fileName,
            folderPath,
            cancellationToken);

        if (uploadResult.IsError)
        {
            return uploadResult.Errors;
        }

        _logger.LogInformation("File {FileName} uploaded successfully to OneDrive with ID: {FileId}", fileName, uploadResult.Value);
        return Result.Success;
    }

    private static string GetDuration(TranscriptionResult transcriptionResult)
    {
        // Extract duration from the first channel (in milliseconds, convert to seconds)
        var durationMs = transcriptionResult.Channels.Count > 0 ? transcriptionResult.Channels[0].Duration : 0;
        var durationSeconds = durationMs / 1000.0;
        return durationSeconds.ToString("F2", CultureInfo.InvariantCulture);
    }
}