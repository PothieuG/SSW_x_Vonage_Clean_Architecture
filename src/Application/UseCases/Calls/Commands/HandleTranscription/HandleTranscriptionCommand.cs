using ErrorOr;
using MediatR;

namespace SSW_x_Vonage_Clean_Architecture.Application.UseCases.Calls.Commands.HandleTranscription;

/// <summary>
/// Command to handle transcription callback from Vonage.
/// </summary>
public sealed record HandleTranscriptionCommand(TranscriptionCallbackRequest Request) : IRequest<ErrorOr<Success>>;
