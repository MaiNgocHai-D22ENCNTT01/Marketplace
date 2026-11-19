using Microsoft.AspNetCore.Mvc;

namespace Marketplace.WebAPI.Controllers;

[ApiController]
[Route("api/status")]
public sealed class StatusController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { status = "ready" });
}
