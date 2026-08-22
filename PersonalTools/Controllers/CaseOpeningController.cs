using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalTools.Classes.CaseOpening;
using PersonalTools.Entities;
using PersonalTools.Entities.CaseOpening;

namespace PersonalTools.Controllers;

[Authorize]
[ApiController]
[Route("api/case-opening")]
public sealed class CaseOpeningController : ControllerBase
{
    private readonly ICaseOpeningFuncs _caseOpening;
    private readonly ILogger<CaseOpeningController> _logger;

    public CaseOpeningController(ICaseOpeningFuncs caseOpening, ILogger<CaseOpeningController> logger)
    {
        _caseOpening = caseOpening;
        _logger = logger;
    }

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("cases")]
    public Task<ActionResult<List<CaseOpeningCaseSummaryObj>>> GetCases(CancellationToken cancellationToken)
    {
        return Execute(() => _caseOpening.GetCaseOpeningCases(cancellationToken), "load catalogue", "curated");
    }

    [HttpGet("cases/{caseKey}")]
    public Task<ActionResult<CaseOpeningCaseObj>> GetCase(string caseKey, CancellationToken cancellationToken)
    {
        return Execute(() => _caseOpening.GetCaseOpeningCase(caseKey, cancellationToken), "load", caseKey);
    }

    [HttpGet("history")]
    public async Task<ActionResult<List<CaseOpeningHistoryObj>>> GetHistory(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _caseOpening.GetCaseOpeningHistory(UserId, cancellationToken));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Case-opening history failed for user {UserId}.", UserId);
            return StatusCode(500, new ApiResponse(false, "Your case-opening history could not be loaded."));
        }
    }

    [HttpGet("cases/{caseKey}/statistics")]
    public Task<ActionResult<CaseOpeningStatisticsObj>> GetStatistics(string caseKey, CancellationToken cancellationToken)
    {
        return Execute(
            () => _caseOpening.GetCaseOpeningStatistics(UserId, caseKey, cancellationToken),
            "load statistics",
            caseKey);
    }

    [HttpPost("cases/{caseKey}/open")]
    public Task<ActionResult<CaseOpeningResultObj>> OpenCase(string caseKey, CancellationToken cancellationToken)
    {
        return Execute(() => _caseOpening.OpenCase(UserId, caseKey, cancellationToken), "open", caseKey);
    }

    [HttpDelete("history")]
    public async Task<ActionResult<ApiResponse>> ClearHistory(CancellationToken cancellationToken)
    {
        try
        {
            await _caseOpening.ClearCaseOpeningHistory(UserId, cancellationToken);
            return Ok(new ApiResponse(true, "Case-opening history cleared."));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Case-opening history clear failed for user {UserId}.", UserId);
            return StatusCode(500, new ApiResponse(false, "Your case-opening history could not be cleared."));
        }
    }

    private async Task<ActionResult<T>> Execute<T>(Func<Task<T>> action, string operation, string caseKey)
    {
        try
        {
            return Ok(await action());
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new ApiResponse(false, exception.Message));
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Case-opening {Operation} failed for user {UserId} and case {CaseKey}.",
                operation,
                UserId,
                caseKey);
            return StatusCode(502, new ApiResponse(false, "The case service could not be reached. Please try again shortly."));
        }
    }
}
