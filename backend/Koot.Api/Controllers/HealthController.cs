using Microsoft.AspNetCore.Mvc;

namespace Koot.Api.Controllers;

/// <summary>
/// Basic health endpoint. The minimal API in Program.cs also exposes /api/health,
/// this controller is a more structured stand-in if controllers are preferred.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet("ping")]
    public IActionResult Ping() => Ok(new { status = "ok", service = "koot-api" });
}
