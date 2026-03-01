using Microsoft.AspNetCore.Mvc;
using WhisperApi.Services;

namespace WhisperApi.Controllers;

[Route("")]
[ApiController]
public class TranscribeController : ControllerBase
{
    private readonly SubtitleTranscriptionService _transcriber;
    private readonly ILogger<TranscribeController> _logger;

    public TranscribeController(SubtitleTranscriptionService transcriber, ILogger<TranscribeController> logger)
    {
        _transcriber = transcriber;
        _logger = logger;
    }


    [HttpPost("/transcribe")]
    [Produces("application/json")]
    public async Task<IActionResult> Transcribe([FromQuery] string url, [FromQuery] string? language, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(url))
            return JsonResult("{\"error\":\"url is required\"}", 400);

        try
        {
            var json = await _transcriber.TranscribeUrlToSubtitlesJsonAsync(url, language ?? "auto", ct);
            return JsonResult(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Transcribe failed");
            return JsonResult("{\"error\":\"transcribe failed\"}", 503);
        }
    }
    
    private static IActionResult JsonResult(string json, int statusCode = 200)
    {
        return new ContentResult
        {
            Content = json,
            ContentType = "application/json; charset=utf-8",
            StatusCode = statusCode
        };
    }
}