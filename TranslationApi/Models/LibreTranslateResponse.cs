using System.Text.Json.Serialization;

namespace TranslationApi.Models;

public sealed class LibreTranslateResponse
{
    [JsonPropertyName("translatedText")]
    public string TranslatedText { get; set; } = "";
}