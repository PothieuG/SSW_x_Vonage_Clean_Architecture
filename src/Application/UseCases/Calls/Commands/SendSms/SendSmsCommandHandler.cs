using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;
using SSW_x_Vonage_Clean_Architecture.Application.Common.Interfaces;

namespace SSW_x_Vonage_Clean_Architecture.Application.UseCases.Calls.Commands.SendSms;

/// <summary>
/// Handler for SendSmsCommand that orchestrates SMS sending via Vonage.
/// </summary>
internal sealed class SendSmsCommandHandler : IRequestHandler<SendSmsCommand, ErrorOr<string>>
{
    private readonly IVonageService _vonageService;
    private readonly ILogger<SendSmsCommandHandler> _logger;

    public SendSmsCommandHandler(
        IVonageService vonageService,
        ILogger<SendSmsCommandHandler> logger)
    {
        _vonageService = vonageService;
        _logger = logger;
    }

    public async Task<ErrorOr<string>> Handle(SendSmsCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Sending SMS to {PhoneNumber}. Message length: {MessageLength} characters",
            request.PhoneNumber,
            request.Message.Length);

        // Delegate to VonageService to send the SMS
        var result = await _vonageService.SendSmsAsync(
            request.PhoneNumber,
            request.Message,
            cancellationToken);

        if (result.IsError)
        {
            _logger.LogError(
                "Failed to send SMS to {PhoneNumber}: {Error}",
                request.PhoneNumber,
                result.FirstError.Description);

            return result.FirstError;
        }

        var messageId = result.Value;

        _logger.LogInformation(
            "SMS sent successfully to {PhoneNumber}. Message ID: {MessageId}",
            request.PhoneNumber,
            messageId);

        return messageId;
    }
}
