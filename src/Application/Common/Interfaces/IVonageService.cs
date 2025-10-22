using SSW_x_Vonage_Clean_Architecture.Application.UseCases.Calls.Commands.HandleTranscription;

namespace SSW_x_Vonage_Clean_Architecture.Application.Common.Interfaces;

public interface IVonageService
{
    Task<string> InitiateCallAsync(string phoneNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a recording file from Vonage with proper authentication.
    /// </summary>
    /// <param name="recordingUrl">The Vonage recording URL</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A stream containing the recording file</returns>
    Task<ErrorOr<Stream>> DownloadRecordingAsync(string recordingUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads and parses the transcription JSON from Vonage with proper authentication.
    /// </summary>
    /// <param name="transcriptionUrl">The Vonage transcription URL</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The parsed transcription result</returns>
    Task<ErrorOr<TranscriptionResult>> DownloadTranscriptionAsync(string transcriptionUrl, CancellationToken cancellationToken = default);
}
