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
    public async Task<ActionResult<TranslateResponse>> Translate([FromBody] TranslateRequest request,
        CancellationToken ct)
    {
        _logger.LogInformation("Incoming language param: {Language}", request.Source);
        if (string.IsNullOrWhiteSpace(request.Text))
            return BadRequest("Text is required");

        if (request.Targets.Length == 0)
            return BadRequest("Targets are required");

        var (sourceLang, dict) = await _service.TranslateManyAsync(
            request.Text,
            request.Source ?? "auto",
            request.Targets,
            ct);

        return Ok(new TranslateResponse
        {
            Source = sourceLang,
            Translations = dict
        });
    }
}