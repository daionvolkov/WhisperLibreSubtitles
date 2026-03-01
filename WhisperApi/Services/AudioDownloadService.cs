using System.Diagnostics;

namespace WhisperApi.Services;

public sealed class AudioDownloadService
{
      private readonly IConfiguration _cfg;
    private readonly ILogger<AudioDownloadService> _logger;

    public AudioDownloadService(IConfiguration cfg, ILogger<AudioDownloadService> logger)
    {
        _cfg = cfg;
        _logger = logger;
    }

    public async Task<string> DownloadAndConvertToWav16KMonoAsync(string mediaUrl, 
        string workDir, CancellationToken ct)
    {
        Directory.CreateDirectory(workDir);

        var ytDlp = _cfg["Transcription:YtDlpPath"] ?? "yt-dlp";
        var ffmpeg = _cfg["Transcription:FfmpegPath"] ?? "ffmpeg";

        var rawPath = Path.Combine(workDir, "input.%(ext)s");
        var rawTemplate = rawPath.Replace("%(ext)s", "m4a"); 

        var ytArgs = new[]
        {
            "--no-playlist",
            "-f", "bestaudio/best",
            "-o", rawPath,
            mediaUrl
        };

        _logger.LogInformation("Downloading audio via yt-dlp...");
        await RunProcessAsync(ytDlp, ytArgs, workDir, ct);

        var downloaded = Directory.GetFiles(workDir, "input.*")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();

        if (downloaded is null)
            throw new InvalidOperationException("yt-dlp finished but no downloaded file was found.");
        
        var wavPath = Path.Combine(workDir, "audio.wav");
        var ffArgs = new[]
        {
            "-y",
            "-i", downloaded,
            "-ac", "1",
            "-ar", "16000",
            "-vn",
            wavPath
        };

        _logger.LogInformation("Converting audio with ffmpeg to {WavPath}", wavPath);
        await RunProcessAsync(ffmpeg, ffArgs, workDir, ct);

        if (!File.Exists(wavPath))
            throw new InvalidOperationException("ffmpeg finished but audio.wav was not created.");

        return wavPath;
    }

    private async Task RunProcessAsync(string fileName, IEnumerable<string> args, 
        string workDir, CancellationToken ct)
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
            _logger.LogInformation("{Exe} stdout: {Stdout}", fileName, Trim(stdout));
        if (!string.IsNullOrWhiteSpace(stderr))
            _logger.LogInformation("{Exe} stderr: {Stderr}", fileName, Trim(stderr));

        if (p.ExitCode != 0)
            throw new InvalidOperationException($"{fileName} exited with code {p.ExitCode}. stderr: {Trim(stderr)}");
    }

    private static string Trim(string s)
    {
        s = s.Trim();
        return s.Length <= 3000 ? s : s[..3000] + "…";
    }

}