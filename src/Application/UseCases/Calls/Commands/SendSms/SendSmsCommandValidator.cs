using FluentValidation;

namespace SSW_x_Vonage_Clean_Architecture.Application.UseCases.Calls.Commands.SendSms;

/// <summary>
/// Validator for SendSmsCommand that ensures proper phone number format and message content.
/// </summary>
internal sealed class SendSmsCommandValidator : AbstractValidator<SendSmsCommand>
{
    public SendSmsCommandValidator()
    {
        RuleFor(x => x.PhoneNumber)
            .NotEmpty()
            .WithMessage("Phone number is required")
            .Must(BeValidPhoneNumber)
            .WithMessage("Phone number must be in E.164 format (e.g., +33612345678)");

        RuleFor(x => x.Message)
            .NotEmpty()
            .WithMessage("SMS message cannot be empty")
            .MaximumLength(1600) // Vonage allows up to ~1600 chars (splits into multiple SMS if needed)
            .WithMessage("SMS message is too long. Maximum 1600 characters.");
    }

    /// <summary>
    /// Validates that the phone number is in E.164 format.
    /// E.164 format: + followed by 1-15 digits
    /// </summary>
    private static bool BeValidPhoneNumber(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return false;
        }

        // Must start with +
        if (!phoneNumber.StartsWith('+'))
        {
            return false;
        }

        // Remove the + and check remaining characters
        var digitsOnly = phoneNumber[1..];

        // Must be between 1 and 15 digits (E.164 standard)
        if (digitsOnly.Length is < 1 or > 15)
        {
            return false;
        }

        // All remaining characters must be digits
        return digitsOnly.All(char.IsDigit);
    }
}
