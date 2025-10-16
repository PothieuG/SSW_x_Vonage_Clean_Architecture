using Microsoft.Extensions.Logging;
using SSW_x_Vonage_Clean_Architecture.Application.Common.Interfaces;

namespace SSW_x_Vonage_Clean_Architecture.Infrastructure.Vonage;

internal sealed class VonageService(ILogger<VonageService> logger) : IVonageService
{
    public async Task<string> InitiateCallAsync(string phoneNumber, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("VonageService: Initiating call to {PhoneNumber}", phoneNumber);

        // TODO: Implement actual Vonage API call
        // For now, return a mock call ID
        await Task.Delay(100, cancellationToken); // Simulate API call

        var callId = Guid.NewGuid().ToString();
        logger.LogInformation("VonageService: Call initiated with ID {CallId}", callId);

        return callId;
    }
}
