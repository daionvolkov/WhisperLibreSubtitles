using System.Text.Json.Serialization;

namespace TranslationApi.Models;

public sealed class TranslateRequest
{
    [JsonPropertyName("subtitlesJson")]
    public string SubtitlesJson { get; set; } = "";

    [JsonPropertyName("targetLanguages")]
    public string[] TargetLanguages { get; set; } = [];

    // Backward-compatible alias in case the client still sends the old field name.
    [JsonPropertyName("targets")]
    public string[]? Targets { get; set; }

    public string[] GetRequestedTargetLanguages()
        => TargetLanguages.Length > 0 ? TargetLanguages : Targets ?? [];
}
