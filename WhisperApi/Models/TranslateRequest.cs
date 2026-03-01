using System.Text.Json.Serialization;

namespace WhisperApi.Models;

public class TranslateRequest
{
    [JsonPropertyName("text")] 
    public string Text { get; set; } = "";
    
    [JsonPropertyName("source")] 
    public string Source { get; set; } = "auto";
    
    [JsonPropertyName("targets")] 
    public string[] Targets { get; set; } = [];
}