using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using TranslationApi.Models;
using TranslationApi.Services;

namespace TranslationApi.Controllers;

[ApiController]
[Route("")]
[EnableCors("AllowAnyOrigin")]
public class TranslationController : ControllerBase

{
    private readonly TranslationService _service;
    private readonly ILogger<TranslationController> _logger;

    public TranslationController(TranslationService service, ILogger<TranslationController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet("health")]
    public IActionResult Health()
        => Ok(new { ok = true });

    [HttpPost("translate")]
    public async Task<ActionResult<SubtitleTranslationDocument>> Translate([FromBody] TranslateRequest request,
        CancellationToken ct)
    {
        var targetLanguages = request.GetRequestedTargetLanguages();

        _logger.LogInformation("Incoming translation request for {TargetCount} target languages", targetLanguages.Length);

        if (string.IsNullOrWhiteSpace(request.SubtitlesJson))
            return BadRequest("subtitlesJson is required");

        if (targetLanguages.Length == 0)
            return BadRequest("targetLanguages is required");

        try
        {
            var translatedDocument = await _service.TranslateSubtitlesAsync(
                request.SubtitlesJson,
                targetLanguages,
                ct);

            return Ok(translatedDocument);
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid subtitles JSON payload");
            return BadRequest("subtitlesJson must contain valid JSON in the expected structure");
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Translation provider returned an error");
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                error = ex.Message
            });
        }
    }
}
