using Microsoft.Extensions.Logging;
using SSW_x_Vonage_Clean_Architecture.Application.Common.Interfaces;

namespace SSW_x_Vonage_Clean_Architecture.Application.UseCases.Calls.Commands.InitiateCall;

public sealed record InitiateCallCommand(string CallRequest) : IRequest<ErrorOr<string>>;

internal sealed class InitiateCallCommandHandler(
    IVonageService vonageService,
    ILogger<InitiateCallCommandHandler> logger)
    : IRequestHandler<InitiateCallCommand, ErrorOr<string>>
{
    public async Task<ErrorOr<string>> Handle(InitiateCallCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Initiating call to {PhoneNumber}", request.CallRequest);

        var callId = await vonageService.InitiateCallAsync(request.CallRequest, cancellationToken);

        logger.LogInformation("Call initiated successfully with ID: {CallId}", callId);

        return callId;
    }
}

internal sealed class InitiateCallCommandValidator : AbstractValidator<InitiateCallCommand>
{
    public InitiateCallCommandValidator()
    {
        RuleFor(v => v.CallRequest)
            .NotEmpty()
            .WithMessage("Phone number is required");
    }
}
