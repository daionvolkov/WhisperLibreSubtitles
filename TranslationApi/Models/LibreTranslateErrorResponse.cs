using System.Text.Json.Serialization;

namespace TranslationApi.Models;

public sealed class LibreTranslateErrorResponse
{
    [JsonPropertyName("error")]
    public string? Error { get; set; }
}
