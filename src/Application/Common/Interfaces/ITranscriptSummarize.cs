namespace SSW_x_Vonage_Clean_Architecture.Application.Common.Interfaces;

public interface ITranscriptSummarizer
{
    Task<ErrorOr<string>> SummarizeAsync(string transcript, CancellationToken cancellationToken = default);
}