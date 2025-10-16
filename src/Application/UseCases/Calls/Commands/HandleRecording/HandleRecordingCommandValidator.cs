using FluentValidation;

namespace SSW_x_Vonage_Clean_Architecture.Application.UseCases.Calls.Commands.HandleRecording;

/// <summary>
/// Validator for HandleRecordingCommand.
/// </summary>
internal sealed class HandleRecordingCommandValidator : AbstractValidator<HandleRecordingCommand>
{
    public HandleRecordingCommandValidator()
    {
        RuleFor(x => x.Request.RecordingUuid)
            .NotEmpty()
            .WithMessage("Recording UUID is required");

        RuleFor(x => x.Request.RecordingUrl)
            .NotEmpty()
            .WithMessage("Recording URL is required")
            .Must(BeAValidUrl)
            .WithMessage("Recording URL must be a valid URL");

        RuleFor(x => x.Request.ConversationUuid)
            .NotEmpty()
            .WithMessage("Conversation UUID is required");

        RuleFor(x => x.Request.Size)
            .GreaterThan(0)
            .WithMessage("Recording size must be greater than 0");
    }

    private static bool BeAValidUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uriResult)
               && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
    }
}
