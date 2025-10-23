namespace SSW_x_Vonage_Clean_Architecture.Application.Common.Interfaces;

public interface IMcpService
{
    Task<ErrorOr<string>> ProcessTranscriptWithMcpAsync(string transcript, CancellationToken cancellationToken = default);
}