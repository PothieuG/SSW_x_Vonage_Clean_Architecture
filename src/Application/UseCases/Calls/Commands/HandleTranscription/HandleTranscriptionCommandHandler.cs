using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;

namespace SSW_x_Vonage_Clean_Architecture.Application.UseCases.Calls.Commands.HandleTranscription;

/// <summary>
/// Handler for processing transcription callbacks from Vonage.
/// </summary>
internal sealed class HandleTranscriptionCommandHandler : IRequestHandler<HandleTranscriptionCommand, ErrorOr<Success>>
{
    private readonly ILogger<HandleTranscriptionCommandHandler> _logger;

    public HandleTranscriptionCommandHandler(ILogger<HandleTranscriptionCommandHandler> logger)
    {
        _logger = logger;
    }

    public async Task<ErrorOr<Success>> Handle(HandleTranscriptionCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Received transcription callback for conversation {ConversationUuid}. Recording UUID: {RecordingUuid}",
            request.Request.ConversationUuid,
            request.Request.RecordingUuid);

        if (!string.IsNullOrEmpty(request.Request.Text))
        {
            _logger.LogInformation(
                "Transcription text (Language: {Language}, Confidence: {Confidence}): {Text}",
                request.Request.Language ?? "unknown",
                request.Request.Confidence?.ToString("P") ?? "unknown",
                request.Request.Text);
        }

        // TODO: Implement transcription processing logic:

        _logger.LogInformation(
            "Transcription callback processed successfully for conversation {ConversationUuid}",
            request.Request.ConversationUuid);

        // Simulate async operation
        await Task.CompletedTask;

        return Result.Success;
    }
}
