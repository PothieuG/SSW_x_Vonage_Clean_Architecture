using System.Text.Json.Serialization;

namespace SSW_x_Vonage_Clean_Architecture.Application.UseCases.Calls.Commands.HandleRecording;

/// <summary>
/// Represents the webhook payload sent by Vonage when a recording is completed.
/// Based on Vonage Voice API webhook reference.
/// </summary>
public sealed record RecordingCallbackRequest
{
    /// <summary>
    /// The time the recording started (ISO 8601 format).
    /// </summary>
    [JsonPropertyName("start_time")]
    public required string StartTime { get; init; }

    /// <summary>
    /// The URL to download the recording file.
    /// Requires authentication to access.
    /// </summary>
    [JsonPropertyName("recording_url")]
    public required string RecordingUrl { get; init; }

    /// <summary>
    /// The size of the recording file in bytes.
    /// </summary>
    [JsonPropertyName("size")]
    public required int Size { get; init; }

    /// <summary>
    /// The unique identifier for this recording.
    /// </summary>
    [JsonPropertyName("recording_uuid")]
    public required string RecordingUuid { get; init; }

    /// <summary>
    /// The time the recording ended (ISO 8601 format).
    /// </summary>
    [JsonPropertyName("end_time")]
    public required string EndTime { get; init; }

    /// <summary>
    /// The unique identifier for the conversation/call.
    /// </summary>
    [JsonPropertyName("conversation_uuid")]
    public required string ConversationUuid { get; init; }

    /// <summary>
    /// The timestamp when this webhook was sent (ISO 8601 format).
    /// </summary>
    [JsonPropertyName("timestamp")]
    public required string Timestamp { get; init; }
}
