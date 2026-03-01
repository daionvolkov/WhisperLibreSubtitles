using System.Text.Json.Serialization;

namespace WhisperApi.Models;

public sealed class SubtitleResponse
{
    [JsonPropertyName("speakers")]
    public Dictionary<string, Dictionary<string, string>> Speakers { get; set; } = new();

    [JsonPropertyName("languages")]
    public Dictionary<string, string> Languages { get; set; } = new();

    [JsonPropertyName("subtitles")]
    public List<SubtitleItem> Subtitles { get; set; } = new();

}