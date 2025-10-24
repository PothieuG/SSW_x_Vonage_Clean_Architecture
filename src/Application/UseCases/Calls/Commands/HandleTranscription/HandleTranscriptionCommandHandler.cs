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

        // Step 7: Retrieve OneDrive folder URL to include in SMS
        _logger.LogInformation("Retrieving OneDrive folder URL for folder {FolderPath}", folderPath);

        var folderUrlResult = await _oneDriveService.GetFolderWebUrlAsync(folderPath, cancellationToken);

        string? oneDriveFolderUrl = null;
        if (folderUrlResult.IsError)
        {
            _logger.LogWarning(
                "Failed to retrieve OneDrive folder URL: {Error}. SMS will not include folder link.",
                folderUrlResult.FirstError.Description);
        }
        else
        {
            oneDriveFolderUrl = folderUrlResult.Value;
            _logger.LogInformation("OneDrive folder URL retrieved: {FolderUrl}", oneDriveFolderUrl);
        }

        // Step 8: Retrieve call information to get the phone number
        _logger.LogInformation("Retrieving call information for conversation {ConversationUuid}", webhookRequest.ConversationUuid);

        var callInfoResult = await _vonageService.GetCallByConversationUuidAsync(
            webhookRequest.ConversationUuid,
            cancellationToken);

        if (callInfoResult.IsError)
        {
            _logger.LogWarning(
                "Failed to retrieve call information for {ConversationUuid}: {Error}. SMS will not be sent.",
                webhookRequest.ConversationUuid,
                callInfoResult.FirstError.Description);

            // Don't fail the entire operation if SMS fails - files were already uploaded successfully
            return Result.Success;
        }

        var callInfo = callInfoResult.Value;

        // Step 9: Build SMS message with summary and OneDrive link
        var smsMessage = BuildSmsMessage(callInfo.DurationSeconds, processedContent, oneDriveFolderUrl);

        // Step 10: Send SMS notification to the person who received the call
        _logger.LogInformation("Sending SMS notification to {PhoneNumber}", callInfo.ToPhoneNumber);

        var smsResult = await _vonageService.SendSmsAsync(
            callInfo.ToPhoneNumber,
            smsMessage,
            cancellationToken);

        if (smsResult.IsError)
        {
            _logger.LogWarning(
                "Failed to send SMS to {PhoneNumber}: {Error}",
                callInfo.ToPhoneNumber,
                smsResult.FirstError.Description);

            // Don't fail the entire operation if SMS fails - files were already uploaded successfully
            return Result.Success;
        }

        _logger.LogInformation(
            "SMS sent successfully to {PhoneNumber}. Message ID: {MessageId}",
            callInfo.ToPhoneNumber,
            smsResult.Value);

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

    /// <summary>
    /// Builds an SMS message with call summary and OneDrive folder link.
    /// SMS Cost Information (Vonage):
    /// - 1 SMS = 160 chars (GSM-7 encoding) or 70 chars (Unicode/Emoji)
    /// - Messages are concatenated automatically if longer
    /// - Typical costs: €0.05-0.10 per SMS segment
    ///
    /// Current configuration: ~640 chars max = 10 SMS segments ≈ €0.50-1.00 per message
    /// Adjust maxSummaryLength below to control costs vs. completeness.
    /// </summary>
    private static string BuildSmsMessage(int durationSeconds, string summary, string? oneDriveFolderUrl)
    {
        var messageBuilder = new StringBuilder();
        messageBuilder.AppendLine("📞 Nouveau message vocal");
        messageBuilder.AppendLine(CultureInfo.InvariantCulture, $"Durée: {durationSeconds}s");
        messageBuilder.AppendLine();

        // Vonage automatically concatenates long SMS messages
        // Each segment = 160 chars (GSM-7) or 70 chars (with emojis/Unicode)
        //
        // Recommended limits based on cost tolerance:
        // - 140 chars  = 1-2 SMS  (~€0.05-0.20)  - Very brief
        // - 300 chars  = 3-5 SMS  (~€0.15-0.50)  - Concise summary
        // - 640 chars  = 6-10 SMS (~€0.30-1.00)  - Detailed summary ⭐ Current setting
        // - 1000 chars = 10-15 SMS (~€0.50-1.50) - Full summary
        // - No limit   = Full AI output (could be expensive!)

        // Reserve space for OneDrive link if present (typically ~100 chars)
        var oneDriveLinkLength = !string.IsNullOrWhiteSpace(oneDriveFolderUrl) ? oneDriveFolderUrl.Length + 20 : 0;
        var maxSummaryLength = 640 - oneDriveLinkLength; // Adjust based on OneDrive link presence

        var truncatedSummary = summary.Length > maxSummaryLength
            ? summary[..maxSummaryLength] + "..."
            : summary;

        messageBuilder.Append(truncatedSummary);

        // Add OneDrive folder link at the end if available
        if (!string.IsNullOrWhiteSpace(oneDriveFolderUrl))
        {
            messageBuilder.AppendLine();
            messageBuilder.AppendLine();
            messageBuilder.Append("📁 Fichiers: ");
            messageBuilder.Append(oneDriveFolderUrl);
        }

        return messageBuilder.ToString();
    }
}