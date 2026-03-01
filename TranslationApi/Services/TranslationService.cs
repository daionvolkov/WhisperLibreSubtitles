using System.Text.Json.Serialization;
using TranslationApi.Models;

namespace TranslationApi.Services;

public sealed class TranslationService
{
    private readonly IHttpClientFactory _factory;

    public TranslationService(IHttpClientFactory factory)
    {
        _factory = factory;
    }
    
    public async Task<(string Source, Dictionary<string, string> Translations)> TranslateManyAsync(
        string text,
        string source,
        string[] targets,
        CancellationToken ct)
    {
        var http = _factory.CreateClient("libre");

        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var target in targets)
        {
            if (string.Equals(target, source, StringComparison.OrdinalIgnoreCase))
            {
                dict[target] = text;
                continue;
            }

            var libreReq = new LibreTranslateRequest
            {
                Q = text,
                Source = source,
                Target = target,
                Format = "text"
            };

            using var resp = await http.PostAsJsonAsync("/translate", libreReq, ct);
            resp.EnsureSuccessStatusCode();

            var libreResp = await resp.Content.ReadFromJsonAsync<LibreTranslateResponse>(cancellationToken: ct);
            dict[target] = libreResp?.TranslatedText ?? text;
        }

        return (source, dict);
    }
}