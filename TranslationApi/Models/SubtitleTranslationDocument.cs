using System.Text.Json.Serialization;

namespace TranslationApi.Models;

public sealed class SubtitleTranslationDocument
{
    [JsonPropertyName("speakers")]
    public Dictionary<string, Dictionary<string, string>> Speakers { get; set; } = new();

    [JsonPropertyName("languages")]
    public Dictionary<string, string> Languages { get; set; } = new();

    [JsonPropertyName("subtitles")]
    public List<SubtitleItem> Subtitles { get; set; } = [];
}

public sealed class SubtitleItem
{
    [JsonPropertyName("end")]
    public string End { get; set; } = "";

    [JsonPropertyName("text")]
    public Dictionary<string, string> Text { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("start")]
    public string Start { get; set; } = "";

    [JsonPropertyName("speakerId")]
    public int SpeakerId { get; set; }
}
