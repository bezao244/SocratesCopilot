using CompanyCopilot.Application.Abstractions;
using CompanyCopilot.Application.Configuration;
using CompanyCopilot.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CompanyCopilot.Api.Controllers;

[ApiController]
public sealed class HealthController : ControllerBase
{
    private readonly CopilotDbContext _db;
    private readonly IOllamaClient _ollama;
    private readonly OllamaOptions _ollamaOptions;
    private readonly ILogger<HealthController> _logger;

    public HealthController(
        CopilotDbContext db,
        IOllamaClient ollama,
        IOptions<OllamaOptions> ollamaOptions,
        ILogger<HealthController> logger)
    {
        _db = db;
        _ollama = ollama;
        _ollamaOptions = ollamaOptions.Value;
        _logger = logger;
    }

    [HttpGet("/health/live")]
    public IActionResult Live() => Ok(new { status = "live" });

    [HttpGet("/health/ready")]
    public async Task<IActionResult> Ready(CancellationToken cancellationToken)
    {
        var databaseOk = false;
        try
        {
            databaseOk = await _db.Database.CanConnectAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Health check: banco indisponível");
        }

        var ollamaOk = await _ollama.IsAvailableAsync(cancellationToken);

        if (databaseOk && ollamaOk)
        {
            return Ok(new { status = "ready", database = true, ollama = true });
        }

        return StatusCode(
            StatusCodes.Status503ServiceUnavailable,
            new { status = "not_ready", database = databaseOk, ollama = ollamaOk });
    }
}
