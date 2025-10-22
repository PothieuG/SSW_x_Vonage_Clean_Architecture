using System.Text;
using System.Text.Json.Serialization;

namespace SSW_x_Vonage_Clean_Architecture.Application.UseCases.Calls.Commands.HandleTranscription;

/// <summary>
/// Root object returned by Vonage transcription API
/// </summary>
public sealed record TranscriptionResult
{
    [JsonPropertyName("ver")]
    public required string Version { get; init; }

    [JsonPropertyName("request_id")]
    public required string RequestId { get; init; }

    [JsonPropertyName("channels")]
    public required IReadOnlyList<Channel> Channels { get; init; }
}

/// <summary>
/// Represents an audio channel in the transcription
/// </summary>
public sealed record Channel
{
    [JsonPropertyName("transcript")]
    public required IReadOnlyList<Transcript> Transcript { get; init; }

    [JsonPropertyName("duration")]
    public required int Duration { get; init; }

    /// <summary>
    /// Extracts the full transcript text from all sentences
    /// </summary>
    public string ExtractTranscript()
    {
        var builder = new StringBuilder();
        foreach (var transcript in Transcript)
        {
            builder.AppendLine(transcript.Sentence);
        }
        return builder.ToString();
    }
}

/// <summary>
/// Represents a single transcript sentence with metadata
/// </summary>
public sealed record Transcript
{
    [JsonPropertyName("sentence")]
    public required string Sentence { get; init; }

    [JsonPropertyName("raw_sentence")]
    public required string RawSentence { get; init; }

    [JsonPropertyName("duration")]
    public required int Duration { get; init; }

    [JsonPropertyName("timestamp")]
    public required int Timestamp { get; init; }

    [JsonPropertyName("words")]
    public required IReadOnlyList<Word> Words { get; init; }
}

/// <summary>
/// Represents a single word in the transcription with timing and confidence
/// </summary>
public sealed record Word
{
    [JsonPropertyName("word")]
    public required string WordText { get; init; }

    [JsonPropertyName("start_time")]
    public required int StartTime { get; init; }

    [JsonPropertyName("end_time")]
    public required int EndTime { get; init; }

    [JsonPropertyName("confidence")]
    public required double Confidence { get; init; }
}
