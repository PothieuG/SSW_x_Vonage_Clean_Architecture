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

    /// <summary>
    /// Sends an SMS message via Vonage SMS API.
    /// </summary>
    /// <param name="toPhoneNumber">Recipient phone number in E.164 format (e.g., +33612345678)</param>
    /// <param name="messageText">SMS message content (max 160 characters for single SMS, longer messages are split)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success result with message ID on success, or error details on failure</returns>
    Task<ErrorOr<string>> SendSmsAsync(string toPhoneNumber, string messageText, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves call details from Vonage Voice API using conversation UUID.
    /// </summary>
    /// <param name="conversationUuid">The unique conversation identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Call information including phone numbers and duration</returns>
    Task<ErrorOr<CallInfo>> GetCallByConversationUuidAsync(string conversationUuid, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents essential call information retrieved from Vonage Voice API.
/// </summary>
public sealed record CallInfo(
    string ToPhoneNumber,
    string? FromPhoneNumber,
    int DurationSeconds);

