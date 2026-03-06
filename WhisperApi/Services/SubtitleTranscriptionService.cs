using System.Text.Json;
using System.Text.Json.Nodes;
using System.Diagnostics;
using WhisperApi.Models;
using WhisperApi.Utils;

namespace WhisperApi.Services;

public sealed class SubtitleTranscriptionService
{
    private readonly IConfiguration _cfg;
    private readonly AudioDownloadService _downloader;
    private readonly ILogger<SubtitleTranscriptionService> _logger;
    private readonly TranslationApiClient _translationApi;

    public SubtitleTranscriptionService(IConfiguration cfg, AudioDownloadService downloader, 
        ILogger<SubtitleTranscriptionService> logger, TranslationApiClient translationApi)
    {
        _cfg = cfg;
        _downloader = downloader;
        _logger = logger;
        _translationApi = translationApi;
        _logger = logger;
    }

   public async Task<string> TranscribeUrlToSubtitlesJsonAsync(string mediaUrl, string language, CancellationToken ct)
    {
    var tempRoot = _cfg["Transcription:TempDir"] ?? "/tmp/whisper-api";
    Directory.CreateDirectory(tempRoot);

    var jobDir = Path.Combine(tempRoot, Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(jobDir);

    try
    {
        var wavPath = await _downloader.DownloadAndConvertToWav16KMonoAsync(mediaUrl, jobDir, ct);

        var whisperExe = _cfg["Transcription:WhisperCppExePath"] ?? "/app/whisper.cpp/main";
        var modelPath  = _cfg["Transcription:WhisperModelPath"] ?? "/app/whisper.cpp/models/ggml-base.bin";

        var threads = ParseInt(_cfg["Transcription:WhisperThreads"], 4);
        var beam    = ParseInt(_cfg["Transcription:WhisperBeamSize"], 5);
        var bestOf  = ParseInt(_cfg["Transcription:WhisperBestOf"], 5);

        var outBase = Path.Combine(jobDir, "out");
        var outJson = outBase + ".json";
        
        var requested = (language ?? "auto")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.ToLowerInvariant())
            .Distinct()
            .ToList();

        if (requested.Count == 0)
            requested.Add("auto");

        var whisperLang = "auto";

        var args = new List<string>
        {
            "-m", modelPath,
            "-f", wavPath,
            "-of", outBase,
            "-oj",
            "-t", threads.ToString()
        };

        if (!string.Equals(whisperLang, "auto", StringComparison.OrdinalIgnoreCase))
        {
            args.Add("--detect-language");
        }
        else
        {
            args.Add("-l");
            args.Add(whisperLang);
        }

        args.Add("--beam-size"); args.Add(beam.ToString());
        args.Add(beam.ToString());

        args.Add("--best-of");   args.Add(bestOf.ToString());
        args.Add(bestOf.ToString());
        args.Add("--max-context");
        args.Add("0");


        _logger.LogInformation("Running whisper.cpp...");
        await RunProcessAsync(whisperExe, args, jobDir, ct);

        if (!File.Exists(outJson))
        {
            outJson = Directory.GetFiles(jobDir, "*.json")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault()
                ?? throw new InvalidOperationException("whisper.cpp finished but no JSON output was found.");
        }

        var whisperJson = await File.ReadAllTextAsync(outJson, ct);

        var (segments, detectedLang) = ExtractSegments(whisperJson, whisperLang);
        
        var targets = requested.Contains("auto")
            ? new List<string> { detectedLang }
            : requested;

        if (!targets.Contains(detectedLang))
            targets.Insert(0, detectedLang);

        var subtitles = new List<SubtitleItem>(segments.Count);

        foreach (var s in segments)
        {
            var textMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [detectedLang] = s.Text
            };

            var otherTargets = targets
                .Where(t => !string.Equals(t, detectedLang, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (otherTargets.Count > 0)
            {
                try
                {
                    var translations = await _translationApi.TranslateManyAsync(
                        s.Text,
                        detectedLang,
                        otherTargets,
                        ct);

                    foreach (var kv in translations)
                        textMap[kv.Key.ToLowerInvariant()] = kv.Value;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Translation failed. detectedLang={DetectedLang}, targets={Targets}",
                        detectedLang,
                        string.Join(",", otherTargets));
                }
            }

            subtitles.Add(new SubtitleItem
            {
                Start = TimeFormat.ToHhMmSsMmm(s.StartSeconds),
                End   = TimeFormat.ToHhMmSsMmm(s.EndSeconds),
                SpeakerId = 1,
                Text = textMap.ToDictionary(k => k.Key.ToLowerInvariant(), v => v.Value)
            });
        }

        var response = new SubtitleResponse
        {
            Speakers = new Dictionary<string, Dictionary<string, string>>
            {
                ["1"] = new Dictionary<string, string> { [detectedLang] = "" }
            },
            Languages = new Dictionary<string, List<string>>
            {
                ["1"] = targets
            },
            Subtitles = subtitles
        };

        return JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = null,
            WriteIndented = false
        });
    }
    finally
    {
        try { Directory.Delete(jobDir, true); }
        catch { }
    }
    }

    private static int ParseInt(string? s, int fallback)
        => int.TryParse(s, out var v) && v > 0 ? v : fallback;

    private sealed record Segment(double StartSeconds, double EndSeconds, string Text);

    private static (List<Segment> Segments, string Language) ExtractSegments(string whisperJson, 
        string requestedLanguage)
    {
        JsonNode? node = null;
        try
        {
            node = JsonNode.Parse(whisperJson);
        }
        catch
        {
            throw new InvalidOperationException("Whisper output JSON has no 'segments' or 'transcription' array.");
        }

        var lang =
            ReadString(node?["language"]) ??
            ReadString(node?["result"]?["language"]) ??
            ReadString(node?["params"]?["language"]) ??
            (string.Equals(requestedLanguage, "auto", StringComparison.OrdinalIgnoreCase) ? "en" : requestedLanguage);

        if (node?["segments"] is JsonArray segsArray)
            return (ParseSegmentsArray(segsArray), lang);

        if (node?["transcription"] is JsonArray trArray)
            return (ParseTranscriptionArray(trArray), lang);

        var text =
            ReadString(node?["text"]) ??
            ReadString(node?["result"]?["text"]) ??
            ReadString(node?["transcript"]) ??
            ReadString(node?["transcript_text"]) ??
            whisperJson;

        return (new List<Segment> { new Segment(0, 0, text.Trim()) }, lang);
    }

    private async Task RunProcessAsync(string fileName, IEnumerable<string> args, string workDir, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (var a in args)
            psi.ArgumentList.Add(a);

        using var p = new Process();
        p.StartInfo = psi;
        p.EnableRaisingEvents = true;
        p.Start();

        var stdoutTask = p.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = p.StandardError.ReadToEndAsync(ct);

        await p.WaitForExitAsync(ct);

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (!string.IsNullOrWhiteSpace(stdout))
            _logger.LogInformation("whisper.cpp stdout: {Stdout}", Trim(stdout));
        if (!string.IsNullOrWhiteSpace(stderr))
            _logger.LogInformation("whisper.cpp stderr: {Stderr}", Trim(stderr));

        if (p.ExitCode != 0)
            throw new InvalidOperationException($"{fileName} exited with code {p.ExitCode}. stderr: {Trim(stderr)}");
    }

    private static string Trim(string s)
    {
        s = s.Trim();
        return s.Length <= 3000 ? s : s[..3000] + "…";
    }

    
    private static List<Segment> ParseSegmentsArray(JsonArray segsArray)
    {
        var list = new List<Segment>();

        foreach (var sNode in segsArray)
        {
            if (sNode is not JsonObject s) continue;

            var text = (ReadString(s["text"]) ?? "").Trim();
            if (string.IsNullOrEmpty(text)) continue;

            var start = ReadDoubleSafe(s["start"]);
            var end   = ReadDoubleSafe(s["end"]);

            if (start is null && s["t0"] is not null) start = ReadIntSafe(s["t0"]) / 100.0;
            if (end   is null && s["t1"] is not null) end   = ReadIntSafe(s["t1"]) / 100.0;

            list.Add(new Segment(start ?? 0, end ?? (start ?? 0), text));
        }

        return list; 
    }

    private static List<Segment> ParseTranscriptionArray(JsonArray trArray)
    {
        var list = new List<Segment>();

        foreach (var tNode in trArray)
        {
            if (tNode is not JsonObject t) continue;

            var text = (ReadString(t["text"]) ?? "").Trim();
            if (string.IsNullOrEmpty(text)) continue;

            double? start = null, end = null;
            if (t["offsets"] is JsonObject offs)
            {
                var fromMs = ReadIntSafe(offs["from"]);
                var toMs   = ReadIntSafe(offs["to"]);
                start = fromMs / 1000.0;
                end   = toMs   / 1000.0;

                if (end < start) end = start;
            }
            if ((start is null || end is null) && t["timestamps"] is JsonObject ts)
            {
                start ??= ParseTimestampToSeconds(ReadString(ts["from"]));
                end   ??= ParseTimestampToSeconds(ReadString(ts["to"]));

                if (start is not null && end is not null && end < start)
                    end = start;
            }

            list.Add(new Segment(start ?? 0, end ?? (start ?? 0), text));
        }
        return list;
    }

    private static double? ParseTimestampToSeconds(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;

        s = s.Trim().Replace(',', '.'); 

        var parts = s.Split(':');
        if (parts.Length != 3) return null;

        if (!int.TryParse(parts[0], out var hh)) return null;
        if (!int.TryParse(parts[1], out var mm)) return null;

        if (!double.TryParse(parts[2], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var ss))
            return null;

        return (hh * 3600) + (mm * 60) + ss;
    }

    private static string? ReadString(JsonNode? n)
    {
        if (n is null) return null;
        if (n is JsonValue v && v.TryGetValue<string>(out var s)) return s;
        if (n is JsonValue) return n.ToString();
        return null;
    }

    private static double? ReadDoubleSafe(JsonNode? n)
    {
        if (n is null) return null;
        if (n is JsonValue v)
        {
            if (v.TryGetValue<double>(out var d)) return d;
            if (v.TryGetValue<long>(out var l)) return l;
            if (v.TryGetValue<int>(out var i)) return i;

            if (double.TryParse(n.ToString(), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var parsed))
                return parsed;
        }
        return null;
    }

    private static int ReadIntSafe(JsonNode? n)
    {
        if (n is null) return 0;
        if (n is JsonValue v)
        {
            if (v.TryGetValue<int>(out var i)) return i;
            if (v.TryGetValue<long>(out var l)) return (int)l;
            if (int.TryParse(n.ToString(), out var parsed)) return parsed;
        }
        return 0;
    }
}