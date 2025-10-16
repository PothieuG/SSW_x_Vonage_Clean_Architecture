using System.Text.Json.Serialization;

namespace SSW_x_Vonage_Clean_Architecture.Application.UseCases.Calls.Commands.HandleTranscription;

/// <summary>
/// Represents the webhook payload sent by Vonage when a transcription is completed.
/// Based on Vonage Voice API transcription webhook reference.
/// </summary>
public sealed record TranscriptionCallbackRequest
{
    /// <summary>
    /// The time the recording started (ISO 8601 format).
    /// </summary>
    [JsonPropertyName("start_time")]
    public string? StartTime { get; init; }

    /// <summary>
    /// The URL to the recording that was transcribed.
    /// </summary>
    [JsonPropertyName("recording_url")]
    public string? RecordingUrl { get; init; }

    /// <summary>
    /// The size of the recording file in bytes.
    /// </summary>
    [JsonPropertyName("size")]
    public int? Size { get; init; }

    /// <summary>
    /// The unique identifier for the recording.
    /// </summary>
    [JsonPropertyName("recording_uuid")]
    public required string RecordingUuid { get; init; }

    /// <summary>
    /// The time the recording ended (ISO 8601 format).
    /// </summary>
    [JsonPropertyName("end_time")]
    public string? EndTime { get; init; }

    /// <summary>
    /// The unique identifier for the conversation/call.
    /// </summary>
    [JsonPropertyName("conversation_uuid")]
    public required string ConversationUuid { get; init; }

    /// <summary>
    /// The timestamp when this webhook was sent (ISO 8601 format).
    /// </summary>
    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; init; }

    /// <summary>
    /// The transcribed text from the recording.
    /// </summary>
    [JsonPropertyName("text")]
    public string? Text { get; init; }

    /// <summary>
    /// The language code used for transcription (e.g., "fr-FR").
    /// </summary>
    [JsonPropertyName("language")]
    public string? Language { get; init; }

    /// <summary>
    /// Confidence score for the transcription (0.0 to 1.0).
    /// </summary>
    [JsonPropertyName("confidence")]
    public decimal? Confidence { get; init; }

    /// <summary>
    /// Sentiment analysis results (if enabled).
    /// </summary>
    [JsonPropertyName("sentiment")]
    public SentimentData? Sentiment { get; init; }
}

/// <summary>
/// Sentiment analysis data from transcription.
/// </summary>
public sealed record SentimentData
{
    /// <summary>
    /// Overall sentiment score (-1.0 to 1.0, where -1 is negative, 0 is neutral, 1 is positive).
    /// </summary>
    [JsonPropertyName("score")]
    public decimal? Score { get; init; }

    /// <summary>
    /// Sentiment label (e.g., "positive", "negative", "neutral").
    /// </summary>
    [JsonPropertyName("label")]
    public string? Label { get; init; }
}
