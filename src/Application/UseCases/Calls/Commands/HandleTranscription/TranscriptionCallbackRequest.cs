using System.Text.Json.Serialization;

namespace SSW_x_Vonage_Clean_Architecture.Application.UseCases.Calls.Commands.HandleTranscription;

/// <summary>
/// Represents the webhook payload sent by Vonage when a transcription is completed.
/// The webhook contains a URL to download the actual transcription JSON.
/// </summary>
public sealed record TranscriptionCallbackRequest
{
    /// <summary>
    /// The unique identifier for the conversation/call.
    /// </summary>
    [JsonPropertyName("conversation_uuid")]
    public required string ConversationUuid { get; init; }

    /// <summary>
    /// The type of event (usually "transcription").
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary>
    /// The unique identifier for the recording.
    /// </summary>
    [JsonPropertyName("recording_uuid")]
    public required string RecordingUuid { get; init; }

    /// <summary>
    /// The status of the transcription (e.g., "completed").
    /// </summary>
    [JsonPropertyName("status")]
    public string? Status { get; init; }

    /// <summary>
    /// The URL to download the transcription JSON (requires JWT authentication).
    /// </summary>
    [JsonPropertyName("transcription_url")]
    public required string TranscriptionUrl { get; init; }
}
