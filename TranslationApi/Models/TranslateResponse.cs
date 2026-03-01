using System.Text.Json.Serialization;

namespace TranslationApi.Models;

public class TranslateResponse
{
    [JsonPropertyName("source")]
    public string Source { get; set; } = "";

    [JsonPropertyName("translations")]
    public Dictionary<string, string> Translations { get; set; } = new();
}