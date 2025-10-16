namespace SSW_x_Vonage_Clean_Architecture.Application.Common.Interfaces;

public interface IVonageService
{
    Task<string> InitiateCallAsync(string phoneNumber, CancellationToken cancellationToken = default);
}
