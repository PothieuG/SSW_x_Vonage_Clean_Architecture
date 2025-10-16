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

        // Optional validation for confidence score
        When(x => x.Request.Confidence.HasValue, () =>
        {
            RuleFor(x => x.Request.Confidence!.Value)
                .InclusiveBetween(0m, 1m)
                .WithMessage("Confidence score must be between 0 and 1");
        });

        // Optional validation for sentiment score
        When(x => x.Request.Sentiment?.Score.HasValue == true, () =>
        {
            RuleFor(x => x.Request.Sentiment!.Score!.Value)
                .InclusiveBetween(-1m, 1m)
                .WithMessage("Sentiment score must be between -1 and 1");
        });
    }
}
