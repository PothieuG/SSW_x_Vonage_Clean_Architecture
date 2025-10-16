using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;

namespace SSW_x_Vonage_Clean_Architecture.Application.UseCases.Calls.Commands.HandleRecording;

/// <summary>
/// Handler for processing recording callbacks from Vonage.
/// </summary>
internal sealed class HandleRecordingCommandHandler : IRequestHandler<HandleRecordingCommand, ErrorOr<Success>>
{
    private readonly ILogger<HandleRecordingCommandHandler> _logger;

    public HandleRecordingCommandHandler(ILogger<HandleRecordingCommandHandler> logger)
    {
        _logger = logger;
    }

    public async Task<ErrorOr<Success>> Handle(HandleRecordingCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Received recording callback for conversation {ConversationUuid}. Recording UUID: {RecordingUuid}, URL: {RecordingUrl}, Size: {Size} bytes",
            request.Request.ConversationUuid,
            request.Request.RecordingUuid,
            request.Request.RecordingUrl,
            request.Request.Size);

        // TODO: Implement recording processing logic:

        _logger.LogInformation(
            "Recording callback processed successfully for conversation {ConversationUuid}",
            request.Request.ConversationUuid);

        // Simulate async operation
        await Task.CompletedTask;

        return Result.Success;
    }
}
