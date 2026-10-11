using CodeTrail.Api.Models;
using CodeTrail.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace CodeTrail.Api.Controllers;

[ApiController]
[Route("api")]
public class AnalysisController(AnalysisOrchestrator orchestrator, ILogger<AnalysisController> logger) : ControllerBase
{
    [HttpGet("health")]
    public IActionResult Health() => Ok(new { status = "ok", timestamp = DateTime.UtcNow });

    [HttpPost("analysis")]
    public async Task<IActionResult> Analyze([FromBody] AnalysisRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.RepositoryUrl))
            return BadRequest(new { error = "repositoryUrl is required." });
        if (string.IsNullOrWhiteSpace(request.CommitSha))
            return BadRequest(new { error = "commitSha is required." });

        try
        {
            var result = await orchestrator.AnalyzeAsync(request, ct);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning("Validation error: {Msg}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            logger.LogWarning("Not found: {Msg}", ex.Message);
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning("Operation error: {Msg}", ex.Message);
            return StatusCode(429, new { error = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during analysis");
            return StatusCode(500, new { error = "An unexpected error occurred. Please try again." });
        }
    }
}
