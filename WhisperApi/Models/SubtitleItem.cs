using System.Text.Json.Serialization;

namespace WhisperApi.Models;

public sealed class SubtitleItem
{
    [JsonPropertyName("start")]
    public required string Start { get; set; }

    [JsonPropertyName("end")]
    public required string End { get; set; }

    [JsonPropertyName("speakerId")]
    public int SpeakerId { get; set; } = 1;

    [JsonPropertyName("text")]
    public Dictionary<string, string> Text { get; set; } = new();

}