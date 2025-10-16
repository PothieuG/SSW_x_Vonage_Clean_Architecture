using ErrorOr;
using MediatR;

namespace SSW_x_Vonage_Clean_Architecture.Application.UseCases.Calls.Commands.HandleRecording;

/// <summary>
/// Command to handle recording callback from Vonage.
/// </summary>
public sealed record HandleRecordingCommand(RecordingCallbackRequest Request) : IRequest<ErrorOr<Success>>;
