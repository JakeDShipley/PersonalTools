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
        return Execute(() => _caseOpening.GetCaseOpeningCases(UserId, cancellationToken), "load catalogue", "curated");
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

    [HttpGet("cases/{caseKey}/collection")]
    public Task<ActionResult<CaseOpeningCollectionObj>> GetCollection(string caseKey, CancellationToken cancellationToken)
    {
        return Execute(
            () => _caseOpening.GetCaseOpeningCollection(UserId, caseKey, cancellationToken),
            "load collection",
            caseKey);
    }

    [HttpGet("progress")]
    public Task<ActionResult<CaseOpeningProgressObj>> GetProgress(CancellationToken cancellationToken)
    {
        return Execute(() => _caseOpening.GetCaseOpeningProgress(UserId, cancellationToken), "load progress", "all");
    }

    [HttpPost("upgrades/{upgradeKey}/unlock")]
    public Task<ActionResult<CaseOpeningProgressObj>> UnlockUpgrade(string upgradeKey, CancellationToken cancellationToken)
    {
        return Execute(() => _caseOpening.UnlockCaseOpeningUpgrade(UserId, upgradeKey, cancellationToken), "unlock upgrade", upgradeKey);
    }

    [HttpPost("cases/{caseKey}/unlock")]
    public Task<ActionResult<CaseOpeningProgressObj>> UnlockCase(string caseKey, CancellationToken cancellationToken)
    {
        return Execute(() => _caseOpening.UnlockCaseOpeningCase(UserId, caseKey, cancellationToken), "unlock case", caseKey);
    }

    [HttpPost("inventory/sell")]
    public Task<ActionResult<CaseOpeningSellResultObj>> SellInventory(
        [FromBody] CaseOpeningSellRequestObj request,
        CancellationToken cancellationToken)
    {
        return Execute(
            () => _caseOpening.SellCaseOpeningInventory(UserId, request?.OpeningIds ?? [], cancellationToken),
            "sell inventory",
            "selected");
    }

    [HttpGet("bots")]
    public Task<ActionResult<CaseOpeningBotProgressObj>> GetBots(CancellationToken cancellationToken)
    {
        return Execute(() => _caseOpening.GetCaseOpeningBotProgress(UserId, cancellationToken), "load bots", "all");
    }

    [HttpPost("bots/servers")]
    public Task<ActionResult<CaseOpeningBotProgressObj>> PurchaseBotServer(CancellationToken cancellationToken)
    {
        return Execute(() => _caseOpening.PurchaseCaseOpeningBotServer(UserId, cancellationToken), "purchase bot server", "all");
    }

    [HttpPost("bots")]
    public Task<ActionResult<CaseOpeningBotProgressObj>> PurchaseBot(CancellationToken cancellationToken)
    {
        return Execute(() => _caseOpening.PurchaseCaseOpeningBot(UserId, cancellationToken), "purchase bot", "all");
    }

    [HttpPost("bots/{botId:guid}/open")]
    public Task<ActionResult<CaseOpeningResultObj>> OpenBotCase(
        Guid botId,
        [FromBody] CaseOpeningBotOpenRequestObj request,
        CancellationToken cancellationToken)
    {
        return Execute(
            () => _caseOpening.OpenCaseWithBot(UserId, botId, request?.CaseKey ?? string.Empty, cancellationToken),
            "open bot case",
            request?.CaseKey ?? string.Empty);
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
    public Task<ActionResult<CaseOpeningOpenBatchResultObj>> OpenCase(
        string caseKey,
        [FromBody] CaseOpeningOpenRequestObj? request,
        CancellationToken cancellationToken)
    {
        return Execute(() => _caseOpening.OpenCases(UserId, caseKey, request?.Quantity ?? 1, cancellationToken), "open", caseKey);
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
