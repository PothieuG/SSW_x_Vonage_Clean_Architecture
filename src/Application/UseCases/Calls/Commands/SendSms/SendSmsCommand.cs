using ErrorOr;
using MediatR;

namespace SSW_x_Vonage_Clean_Architecture.Application.UseCases.Calls.Commands.SendSms;

/// <summary>
/// Command to send an SMS message via Vonage API.
/// </summary>
/// <param name="PhoneNumber">Recipient phone number in E.164 format (e.g., +33612345678)</param>
/// <param name="Message">SMS message content</param>
public sealed record SendSmsCommand(
    string PhoneNumber,
    string Message) : IRequest<ErrorOr<string>>;
