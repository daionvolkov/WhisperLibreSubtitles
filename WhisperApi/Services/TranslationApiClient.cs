using System.Text.Json.Serialization;
using WhisperApi.Models;

namespace WhisperApi.Services;

public sealed class TranslationApiClient
{
    private readonly HttpClient _http;

    public TranslationApiClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<Dictionary<string, string>> TranslateManyAsync(
        string text,
        string source,
        IReadOnlyList<string> targets,
        CancellationToken ct)
    {
        if (targets.Count == 0) return new Dictionary<string, string>();

        var req = new TranslateRequest
        {
            Text = text,
            Source = source,
            Targets = targets.ToArray()
        };

        using var resp = await _http.PostAsJsonAsync("/translate", req, ct);
        resp.EnsureSuccessStatusCode();

        var dto = await resp.Content.ReadFromJsonAsync<TranslateResponse>(cancellationToken: ct);
        return dto?.Translations ?? new Dictionary<string, string>();
    }
}