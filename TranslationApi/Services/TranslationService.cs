using System.Collections.Concurrent;
using System.Text.Json;
using TranslationApi.Models;

namespace TranslationApi.Services;

public sealed class TranslationService
{
    private readonly IHttpClientFactory _factory;
    private readonly ILogger<TranslationService> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public TranslationService(IHttpClientFactory factory, ILogger<TranslationService> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public async Task<SubtitleTranslationDocument> TranslateSubtitlesAsync(
        string subtitlesJson,
        string[] targets,
        CancellationToken ct)
    {
        var document = ParseDocument(subtitlesJson);
        var normalizedTargets = targets
            .Select(language => language.Trim().ToLowerInvariant())
            .Where(language => !string.IsNullOrWhiteSpace(language))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (document.Subtitles.Count == 0)
        {
            return document;
        }

        var cache = new ConcurrentDictionary<string, Task<string>>(StringComparer.Ordinal);

        foreach (var subtitle in document.Subtitles)
        {
            if (subtitle.Text.Count == 0)
            {
                continue;
            }

            var sourceLanguage = ResolveSourceLanguage(subtitle, document.Languages);
            var sourceText = ResolveSourceText(subtitle, sourceLanguage);

            if (string.IsNullOrWhiteSpace(sourceLanguage) || string.IsNullOrWhiteSpace(sourceText))
            {
                continue;
            }

            var pendingTranslations = new List<(string Target, Task<string> Task)>();

            foreach (var target in normalizedTargets)
            {
                if (subtitle.Text.ContainsKey(target))
                {
                    continue;
                }

                if (string.Equals(target, sourceLanguage, StringComparison.OrdinalIgnoreCase))
                {
                    subtitle.Text[target] = sourceText;
                    continue;
                }

                pendingTranslations.Add((target, TranslateCachedAsync(cache, sourceText, sourceLanguage, target, ct)));
            }

            foreach (var translation in pendingTranslations)
            {
                subtitle.Text[translation.Target] = await translation.Task;
            }
        }

        ExtendLanguageMetadata(document, normalizedTargets);

        return document;
    }

    private static SubtitleTranslationDocument ParseDocument(string subtitlesJson)
    {
        var document = JsonSerializer.Deserialize<SubtitleTranslationDocument>(subtitlesJson, JsonOptions);

        return document ?? throw new JsonException("The subtitles JSON payload is empty.");
    }

    private async Task<string> TranslateCachedAsync(
        ConcurrentDictionary<string, Task<string>> cache,
        string text,
        string source,
        string target,
        CancellationToken ct)
    {
        var cacheKey = $"{source}\u001f{target}\u001f{text}";

        return await cache.GetOrAdd(cacheKey, _ => TranslateTextAsync(text, source, target, ct));
    }

    private async Task<string> TranslateTextAsync(string text, string source, string target, CancellationToken ct)
    {
        var http = _factory.CreateClient("libre");
        var libreReq = new LibreTranslateRequest
        {
            Q = text,
            Source = source,
            Target = target,
            Format = "text"
        };

        using var resp = await http.PostAsJsonAsync("/translate", libreReq, ct);

        if (!resp.IsSuccessStatusCode)
        {
            var rawError = await resp.Content.ReadAsStringAsync(ct);
            var parsedError = TryParseLibreError(rawError);

            _logger.LogError(
                "LibreTranslate returned {StatusCode} for source '{Source}' to target '{Target}'. Response: {ResponseBody}",
                (int)resp.StatusCode,
                source,
                target,
                rawError);

            throw new InvalidOperationException(
                $"LibreTranslate error for {source}->{target}: {parsedError}");
        }

        var libreResp = await resp.Content.ReadFromJsonAsync<LibreTranslateResponse>(cancellationToken: ct);
        return libreResp?.TranslatedText ?? text;
    }

    private static string TryParseLibreError(string rawError)
    {
        if (string.IsNullOrWhiteSpace(rawError))
        {
            return "empty error response";
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<LibreTranslateErrorResponse>(rawError, JsonOptions);
            return string.IsNullOrWhiteSpace(parsed?.Error) ? rawError : parsed.Error;
        }
        catch (JsonException)
        {
            return rawError;
        }
    }

    private static string ResolveSourceLanguage(SubtitleItem subtitle, IReadOnlyDictionary<string, string> languages)
    {
        if (languages.TryGetValue(subtitle.SpeakerId.ToString(), out var speakerLanguage) &&
            subtitle.Text.ContainsKey(speakerLanguage))
        {
            return speakerLanguage;
        }

        return subtitle.Text.Keys.FirstOrDefault() ?? string.Empty;
    }

    private static string ResolveSourceText(SubtitleItem subtitle, string sourceLanguage)
    {
        if (!string.IsNullOrWhiteSpace(sourceLanguage) &&
            subtitle.Text.TryGetValue(sourceLanguage, out var text) &&
            !string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        return subtitle.Text.Values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }

    private static void ExtendLanguageMetadata(SubtitleTranslationDocument document, IEnumerable<string> targets)
    {
        var existingLanguages = new HashSet<string>(document.Languages.Values, StringComparer.OrdinalIgnoreCase);
        var nextSpeakerId = GetNextSpeakerId(document);

        foreach (var target in targets)
        {
            if (!existingLanguages.Add(target))
            {
                continue;
            }

            var speakerKey = nextSpeakerId.ToString();
            document.Speakers[speakerKey] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [target] = string.Empty
            };
            document.Languages[speakerKey] = target;
            nextSpeakerId++;
        }
    }

    private static int GetNextSpeakerId(SubtitleTranslationDocument document)
    {
        var keys = document.Speakers.Keys
            .Concat(document.Languages.Keys)
            .Select(key => int.TryParse(key, out var id) ? id : 0);

        return keys.DefaultIfEmpty(0).Max() + 1;
    }
}
