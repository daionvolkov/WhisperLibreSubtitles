using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace WhisperApi.Controllers;
[Route("")]
[ApiController]
[EnableCors("AllowAnyOrigin")]

public class HealthController : ControllerBase
{
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new
        {
            status = "ok",
            service = "whisper-api",
            time = DateTime.UtcNow
        });
    }
}