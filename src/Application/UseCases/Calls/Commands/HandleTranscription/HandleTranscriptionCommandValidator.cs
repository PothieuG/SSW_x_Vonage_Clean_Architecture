using FluentValidation;

namespace SSW_x_Vonage_Clean_Architecture.Application.UseCases.Calls.Commands.HandleTranscription;

/// <summary>
/// Validator for HandleTranscriptionCommand.
/// </summary>
internal sealed class HandleTranscriptionCommandValidator : AbstractValidator<HandleTranscriptionCommand>
{
    public HandleTranscriptionCommandValidator()
    {
        RuleFor(x => x.Request.RecordingUuid)
            .NotEmpty()
            .WithMessage("Recording UUID is required");

        RuleFor(x => x.Request.ConversationUuid)
            .NotEmpty()
            .WithMessage("Conversation UUID is required");

        RuleFor(x => x.Request.TranscriptionUrl)
            .NotEmpty()
            .WithMessage("Transcription URL is required");
    }
}
