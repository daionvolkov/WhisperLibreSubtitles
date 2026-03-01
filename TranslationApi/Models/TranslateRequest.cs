using System.Text.Json.Serialization;

namespace TranslationApi.Models;

public sealed class TranslateRequest
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = "";

    // "auto" | "en" | "ru" ...
    [JsonPropertyName("source")]
    public string? Source { get; set; }

    [JsonPropertyName("targets")]
    public string[] Targets { get; set; } = [];
}