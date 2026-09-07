using FreeResumeScanner.DTOs;
using FreeResumeScanner.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FreeResumeScanner.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public sealed class AnalysisController : ControllerBase
{
    private readonly IAnalysisService _service;
    private readonly ILogger<AnalysisController> _logger;

    public AnalysisController(
        IAnalysisService service,
        ILogger<AnalysisController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [Produces("application/json")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<AnalyzeResponse>> Analyze(
        [FromForm] AnalyzeRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _service.AnalyzeAsync(
                request.Resume!,
                request.JobDescription,
                cancellationToken);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogWarning(ex, "Local AI request timed out.");
            return StatusCode(StatusCodes.Status504GatewayTimeout, new
            {
                error = "Local AI processing timed out. Try a smaller model, fewer retrieved chunks, or increase the configured timeout."
            });
        }
        catch (OperationCanceledException)
        {
            return StatusCode(499, new { error = "Request cancelled by the client." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Resume analysis failed.");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
