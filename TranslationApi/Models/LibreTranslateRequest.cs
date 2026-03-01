using System.Text.Json.Serialization;

namespace TranslationApi.Models;

public sealed class LibreTranslateRequest
{
    [JsonPropertyName("q")] 
    public string Q { get; set; } = "";
    
    [JsonPropertyName("source")] 
    public string Source { get; set; } = "auto";
    
    [JsonPropertyName("target")] 
    public string Target { get; set; } = "en";
    
    [JsonPropertyName("format")] 
    public string Format { get; set; } = "text";
}