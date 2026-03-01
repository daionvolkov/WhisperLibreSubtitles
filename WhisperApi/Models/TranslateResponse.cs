using System.Text.Json.Serialization;

namespace WhisperApi.Models;

public sealed class TranslateResponse
{
    [JsonPropertyName("source")] 
    public string Source { get; set; } = "auto";
    
    [JsonPropertyName("translations")] 
    public Dictionary<string, string> Translations { get; set; } = new();
}