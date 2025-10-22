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
    private readonly ITranscriptSummarizer _transcriptSummarizer;
    private readonly ILogger<HandleTranscriptionCommandHandler> _logger;

    public HandleTranscriptionCommandHandler(
        IVonageService vonageService,
        IOneDriveService oneDriveService,
        ITranscriptSummarizer transcriptSummarizer,
        ILogger<HandleTranscriptionCommandHandler> logger)
    {
        _vonageService = vonageService;
        _oneDriveService = oneDriveService;
        _transcriptSummarizer = transcriptSummarizer;
        _logger = logger;
    }

    public async Task<ErrorOr<Success>> Handle(HandleTranscriptionCommand request, CancellationToken cancellationToken)
    {
        var webhookRequest = request.Request;

        _logger.LogInformation(
            "Processing transcription callback for conversation {ConversationUuid}. Recording UUID: {RecordingUuid}",
            webhookRequest.ConversationUuid,
            webhookRequest.RecordingUuid);

        // Créer un timeout global de 2 minutes pour toute l'opération
        using var globalCts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, globalCts.Token);

        try
        {
            // Step 1: Download transcription from Vonage
            _logger.LogInformation("Downloading transcription from {TranscriptionUrl}", webhookRequest.TranscriptionUrl);

            var downloadResult = await _vonageService.DownloadTranscriptionAsync(
                webhookRequest.TranscriptionUrl,
                linkedCts.Token);

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

            // Step 3: Generate summary using Ollama (avec timeout séparé)
            string summaryText;
            var summaryTask = _transcriptSummarizer.SummarizeAsync(transcriptText, linkedCts.Token);
            
            if (await Task.WhenAny(summaryTask, Task.Delay(30000, linkedCts.Token)) == summaryTask)
            {
                var summaryResult = await summaryTask;
                if (summaryResult.IsError)
                {
                    _logger.LogWarning(
                        "Failed to generate summary for {RecordingUuid}: {Error}. Continuing without summary.",
                        webhookRequest.RecordingUuid,
                        summaryResult.FirstError.Description);
                    
                    summaryText = "Résumé non disponible - erreur lors de la génération";
                }
                else
                {
                    summaryText = summaryResult.Value;
                    _logger.LogInformation("Summary generated successfully: {SummaryLength} characters", summaryText.Length);
                }
            }
            else
            {
                _logger.LogWarning("Timeout lors de la génération du résumé. Continuing without summary.");
                summaryText = "Résumé non disponible - timeout lors de la génération";
            }

            // Step 4: Generate filenames
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            var transcriptFileName = $"transcription_{timestamp}_{webhookRequest.RecordingUuid}.txt";
            var summaryFileName = $"summary_{timestamp}_{webhookRequest.RecordingUuid}.txt";
            var folderPath = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            // Step 5: Build files content
            var transcriptContent = BuildTranscriptContent(webhookRequest, transcriptionResult, transcriptText, summaryText);
            var summaryContent = BuildSummaryContent(webhookRequest, transcriptionResult, summaryText, transcriptFileName);

            // Step 6: Upload files to OneDrive
            var uploadTasks = new List<Task<ErrorOr<string>>>();

            // Upload transcript
            var transcriptBytes = Encoding.UTF8.GetBytes(transcriptContent);
            await using var transcriptStream = new MemoryStream(transcriptBytes);
            
            uploadTasks.Add(_oneDriveService.UploadFileFromStreamAsync(
                transcriptStream,
                transcriptFileName,
                folderPath,
                linkedCts.Token));

            // Upload summary
            var summaryBytes = Encoding.UTF8.GetBytes(summaryContent);
            await using var summaryStream = new MemoryStream(summaryBytes);
            
            uploadTasks.Add(_oneDriveService.UploadFileFromStreamAsync(
                summaryStream,
                summaryFileName,
                folderPath,
                linkedCts.Token));

            // Attendre les uploads avec timeout
            var uploadResults = await Task.WhenAll(uploadTasks);

            // Vérifier les résultats
            var transcriptUploadResult = uploadResults[0];
            var summaryUploadResult = uploadResults[1];

            if (transcriptUploadResult.IsError)
            {
                _logger.LogError(
                    "Failed to upload transcription {RecordingUuid} to OneDrive: {Error}",
                    webhookRequest.RecordingUuid,
                    transcriptUploadResult.FirstError.Description);

                return transcriptUploadResult.FirstError;
            }

            if (summaryUploadResult.IsError)
            {
                _logger.LogWarning(
                    "Failed to upload summary {RecordingUuid} to OneDrive: {Error}. Transcript was uploaded successfully.",
                    webhookRequest.RecordingUuid,
                    summaryUploadResult.FirstError.Description);
            }

            _logger.LogInformation(
                "Successfully processed transcription and summary for recording {RecordingUuid}. Transcript file ID: {FileId}",
                webhookRequest.RecordingUuid,
                transcriptUploadResult.Value);

            return Result.Success;
        }
        catch (OperationCanceledException) when (globalCts.Token.IsCancellationRequested)
        {
            _logger.LogError("Global timeout reached for transcription processing");
            return Error.Failure("Processing.Timeout", "Traitement annulé - timeout global dépassé");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Transcription processing cancelled");
            return Error.Failure("Processing.Cancelled", "Traitement annulé");
        }
    }

    private static string BuildTranscriptContent(TranscriptionCallbackRequest webhookRequest, 
        TranscriptionResult transcriptionResult, string transcriptText, string summaryText)
    {
        var contentBuilder = new StringBuilder();
        contentBuilder.AppendLine("=== Vonage Call Transcription ===");
        contentBuilder.AppendLine(CultureInfo.InvariantCulture, $"Recording UUID: {webhookRequest.RecordingUuid}");
        contentBuilder.AppendLine(CultureInfo.InvariantCulture, $"Conversation UUID: {webhookRequest.ConversationUuid}");
        contentBuilder.AppendLine(CultureInfo.InvariantCulture, $"Duration: {transcriptionResult.Channels[0].Duration}s");
        contentBuilder.AppendLine(CultureInfo.InvariantCulture, $"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        contentBuilder.AppendLine();
        
        contentBuilder.AppendLine("=== Résumé de l'appel ===");
        contentBuilder.AppendLine(summaryText);
        contentBuilder.AppendLine();
        
        contentBuilder.AppendLine("=== Transcription Complète ===");
        contentBuilder.Append(transcriptText);

        return contentBuilder.ToString();
    }

    private static string BuildSummaryContent(TranscriptionCallbackRequest webhookRequest,
        TranscriptionResult transcriptionResult, string summaryText, string transcriptFileName)
    {
        var contentBuilder = new StringBuilder();
        contentBuilder.AppendLine("=== Résumé d'Appel Vonage ===");
        contentBuilder.AppendLine(CultureInfo.InvariantCulture, $"Recording UUID: {webhookRequest.RecordingUuid}");
        contentBuilder.AppendLine(CultureInfo.InvariantCulture, $"Conversation UUID: {webhookRequest.ConversationUuid}");
        contentBuilder.AppendLine(CultureInfo.InvariantCulture, $"Duration: {transcriptionResult.Channels[0].Duration}s");
        contentBuilder.AppendLine(CultureInfo.InvariantCulture, $"Généré le: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        contentBuilder.AppendLine();
        
        contentBuilder.AppendLine("=== RÉSUMÉ ===");
        contentBuilder.AppendLine(summaryText);
        contentBuilder.AppendLine();
        
        contentBuilder.AppendLine("=== INFORMATIONS SUPPLÉMENTAIRES ===");
        contentBuilder.AppendLine($"Transcription complète disponible dans: {transcriptFileName}");

        return contentBuilder.ToString();
    }
}