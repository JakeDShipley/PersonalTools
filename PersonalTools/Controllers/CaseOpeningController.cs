using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalTools.Classes.CaseOpening;
using PersonalTools.Entities;
using PersonalTools.Entities.CaseOpening;
using PersonalTools.Security;

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
        return Execute(() => _caseOpening.GetCaseOpeningCase(UserId, caseKey, cancellationToken), "load", caseKey);
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

    [HttpGet("inventory-capacity")]
    public Task<ActionResult<CaseOpeningInventoryCapacityObj>> GetInventoryCapacity(CancellationToken cancellationToken)
    {
        return Execute(() => _caseOpening.GetCaseOpeningInventoryCapacity(UserId, cancellationToken), "load inventory capacity", "all");
    }

    [HttpGet("player-stats")]
    public Task<ActionResult<CaseOpeningPlayerStatsObj>> GetPlayerStats(CancellationToken cancellationToken)
    {
        return Execute(() => _caseOpening.GetCaseOpeningPlayerStats(UserId, cancellationToken), "load player stats", "all");
    }

    [HttpGet("achievements")]
    public Task<ActionResult<CaseOpeningAchievementSummaryObj>> GetAchievements(CancellationToken cancellationToken)
    {
        return Execute(() => _caseOpening.GetCaseOpeningAchievements(UserId, cancellationToken), "load achievements", "all");
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

    [HttpGet("inventory/upgrades")]
    public Task<ActionResult<CaseOpeningInventoryUpgradeObj>> GetInventoryUpgrades(CancellationToken cancellationToken)
    {
        return Execute(() => _caseOpening.GetCaseOpeningInventoryUpgrades(UserId, cancellationToken), "load inventory upgrades", "all");
    }

    [HttpPost("inventory/upgrades/{upgradeKey}/unlock")]
    public Task<ActionResult<CaseOpeningInventoryUpgradeObj>> UnlockInventoryUpgrade(string upgradeKey, CancellationToken cancellationToken)
    {
        return Execute(() => _caseOpening.UnlockCaseOpeningInventoryUpgrade(UserId, upgradeKey, cancellationToken), "unlock inventory upgrade", upgradeKey);
    }

    [HttpPut("inventory/auto-sell")]
    public Task<ActionResult<CaseOpeningInventoryUpgradeObj>> SetAutoSell([FromBody] CaseOpeningAutoSellPreferenceRequestObj request, CancellationToken cancellationToken)
    {
        return Execute(() => _caseOpening.SetCaseOpeningAutoSellPreference(UserId, request.RarityKey, request.Enabled, request.PreserveStatTrak, cancellationToken), "update auto sell", request.RarityKey);
    }

    [HttpGet("auto-buy/rules")]
    public Task<ActionResult<CaseOpeningAutoBuySummaryObj>> GetAutoBuyRules(CancellationToken cancellationToken)
    {
        return Execute(() => _caseOpening.GetCaseOpeningAutoBuyRules(UserId, cancellationToken), "load auto-buy rules", "all");
    }

    [HttpPut("auto-buy/rules/{caseKey}")]
    public Task<ActionResult<CaseOpeningAutoBuySummaryObj>> SetAutoBuyRule(
        string caseKey,
        [FromBody] CaseOpeningAutoBuyRuleRequestObj request,
        CancellationToken cancellationToken)
    {
        return Execute(() => _caseOpening.SetCaseOpeningAutoBuyRule(UserId, caseKey, request, cancellationToken), "save auto-buy rule", caseKey);
    }

    [HttpDelete("auto-buy/rules/{caseKey}")]
    public Task<ActionResult<CaseOpeningAutoBuySummaryObj>> DeleteAutoBuyRule(string caseKey, CancellationToken cancellationToken)
    {
        return Execute(() => _caseOpening.DeleteCaseOpeningAutoBuyRule(UserId, caseKey, cancellationToken), "delete auto-buy rule", caseKey);
    }

    [HttpPost("auto-buy/evaluate")]
    public Task<ActionResult<List<CaseOpeningCasePurchaseResultObj>>> EvaluateAutoBuy(CancellationToken cancellationToken)
    {
        return Execute(() => _caseOpening.EvaluateCaseOpeningAutoBuyRules(UserId, cancellationToken), "evaluate auto-buy", "all");
    }

    [HttpPost("cases/{caseKey}/purchase")]
    public Task<ActionResult<CaseOpeningCasePurchaseResultObj>> PurchaseCases(string caseKey, [FromBody] CaseOpeningCasePurchaseRequestObj? request, CancellationToken cancellationToken)
    {
        return Execute(() => _caseOpening.PurchaseCaseOpeningCases(UserId, caseKey, request?.Quantity ?? 1, cancellationToken), "purchase cases", caseKey);
    }

    [HttpPost("storage-containers")]
    public Task<ActionResult<CaseOpeningStoragePurchaseResultObj>> PurchaseStorageContainer(CancellationToken cancellationToken)
    {
        return Execute(() => _caseOpening.PurchaseCaseOpeningStorageContainer(UserId, cancellationToken), "purchase storage container", "inventory");
    }

    [HttpPost("trade-ups")]
    public Task<ActionResult<CaseOpeningTradeUpResultObj>> CreateTradeUp(
        [FromBody] CaseOpeningTradeUpRequestObj request,
        CancellationToken cancellationToken)
    {
        return Execute(
            () => _caseOpening.CreateCaseOpeningTradeUp(UserId, request?.OpeningIds ?? [], cancellationToken),
            "create trade up",
            "selected");
    }

    [HttpGet("trade-up-recipes")]
    public Task<ActionResult<CaseOpeningTradeUpRecipeSummaryObj>> GetTradeUpRecipes(CancellationToken cancellationToken)
    {
        return Execute(() => _caseOpening.GetCaseOpeningTradeUpRecipes(UserId, cancellationToken), "load auto trade-up recipes", "all");
    }

    [HttpPost("trade-up-recipes")]
    public Task<ActionResult<CaseOpeningTradeUpRecipeSummaryObj>> CreateTradeUpRecipe(
        [FromBody] CaseOpeningTradeUpRecipeRequestObj request,
        CancellationToken cancellationToken)
    {
        return Execute(() => _caseOpening.CreateCaseOpeningTradeUpRecipe(UserId, request, cancellationToken), "create auto trade-up recipe", "new");
    }

    [HttpPut("trade-up-recipes/{recipeId:guid}/active")]
    public Task<ActionResult<CaseOpeningTradeUpRecipeSummaryObj>> SetTradeUpRecipeActive(
        Guid recipeId,
        [FromBody] CaseOpeningTradeUpRecipeActiveRequestObj request,
        CancellationToken cancellationToken)
    {
        return Execute(
            () => _caseOpening.SetCaseOpeningTradeUpRecipeActive(UserId, recipeId, request?.IsActive ?? false, cancellationToken),
            "update auto trade-up recipe",
            recipeId.ToString());
    }

    [HttpDelete("trade-up-recipes/{recipeId:guid}")]
    public Task<ActionResult<CaseOpeningTradeUpRecipeSummaryObj>> DeleteTradeUpRecipe(Guid recipeId, CancellationToken cancellationToken)
    {
        return Execute(() => _caseOpening.DeleteCaseOpeningTradeUpRecipe(UserId, recipeId, cancellationToken), "delete auto trade-up recipe", recipeId.ToString());
    }

    [HttpPost("trade-up-recipes/holdings/{holdingId:guid}/collect")]
    public Task<ActionResult<CaseOpeningTradeUpRecipeSummaryObj>> CollectTradeUpHolding(Guid holdingId, CancellationToken cancellationToken)
    {
        return Execute(() => _caseOpening.CollectCaseOpeningTradeUpHolding(UserId, holdingId, cancellationToken), "collect held skin", holdingId.ToString());
    }

    [HttpPost("trade-up-recipes/slots/upgrade")]
    public Task<ActionResult<CaseOpeningInventoryUpgradeObj>> UpgradeTradeUpRecipeSlots(CancellationToken cancellationToken)
    {
        return Execute(() => _caseOpening.UpgradeCaseOpeningTradeUpRecipeSlots(UserId, cancellationToken), "upgrade auto trade-up recipe slots", "all");
    }

    [HttpPost("trade-up-recipes/{recipeId:guid}/holding/upgrade")]
    public Task<ActionResult<CaseOpeningTradeUpRecipeSummaryObj>> UpgradeTradeUpRecipeHolding(Guid recipeId, CancellationToken cancellationToken)
    {
        return Execute(() => _caseOpening.UpgradeCaseOpeningTradeUpRecipeHolding(UserId, recipeId, cancellationToken), "upgrade recipe holding capacity", recipeId.ToString());
    }

    [HttpPost("trade-up-recipes/evaluate")]
    public Task<ActionResult<List<CaseOpeningTradeUpResultObj>>> EvaluateTradeUpRecipes(CancellationToken cancellationToken)
    {
        return Execute(() => _caseOpening.EvaluateCaseOpeningTradeUpRecipes(UserId, cancellationToken), "evaluate auto trade-up recipes", "all");
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

    [HttpPost("bots/servers/{serverId:guid}/speed")]
    public Task<ActionResult<CaseOpeningBotProgressObj>> UpgradeBotServerSpeed(Guid serverId, CancellationToken cancellationToken)
    {
        return Execute(() => _caseOpening.UpgradeCaseOpeningBotServer(UserId, serverId, cancellationToken), "upgrade bot server speed", serverId.ToString());
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

    // ---------- Game settings + per-case settings (the variable-tweak modal's "Game settings" tab) ----------

    [HttpGet("settings")]
    [Authorize(Policy = AppAuthorizationPolicies.AdminOnly)]
    public Task<ActionResult<CaseOpeningGameSettingsObj>> GetGameSettings(CancellationToken cancellationToken)
    {
        return Execute(() => _caseOpening.GetGameSettings(cancellationToken), "load game settings", "all");
    }

    [HttpPut("settings")]
    [Authorize(Policy = AppAuthorizationPolicies.AdminOnly)]
    public Task<ActionResult<CaseOpeningGameSettingsObj>> UpdateGameSettings(
        [FromBody] CaseOpeningGameSettingsObj request,
        CancellationToken cancellationToken)
    {
        return Execute(() => _caseOpening.SetGameSettings(request, cancellationToken), "update game settings", "all");
    }

    [HttpGet("settings/cases")]
    [Authorize(Policy = AppAuthorizationPolicies.AdminOnly)]
    public Task<ActionResult<List<CaseOpeningCaseSettingsObj>>> GetCaseSettings(CancellationToken cancellationToken)
    {
        return Execute(() => _caseOpening.GetCaseSettings(cancellationToken), "load case settings", "all");
    }

    [HttpPut("settings/cases/{caseKey}")]
    [Authorize(Policy = AppAuthorizationPolicies.AdminOnly)]
    public async Task<ActionResult<ApiResponse>> UpdateCaseSettings(
        string caseKey,
        [FromBody] CaseOpeningCaseSettingsRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _caseOpening.SetCaseSettings(caseKey, request.UnlockCostStars, request.PurchaseCostStars, request.XpRequirement, cancellationToken);
            return Ok(new ApiResponse(true, "Case settings saved."));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new ApiResponse(false, exception.Message));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to update case settings for {CaseKey}.", caseKey);
            return StatusCode(502, new ApiResponse(false, "Case settings could not be saved. Please try again shortly."));
        }
    }

    [HttpGet("settings/xp-by-rarity")]
    [Authorize(Policy = AppAuthorizationPolicies.AdminOnly)]
    public Task<ActionResult<List<CaseOpeningXpByRarityObj>>> GetXpByRarity(CancellationToken cancellationToken)
    {
        return Execute(() => _caseOpening.GetXpByRarity(cancellationToken), "load xp by rarity", "all");
    }

    [HttpGet("settings/inventory-upgrades")]
    [Authorize(Policy = AppAuthorizationPolicies.AdminOnly)]
    public Task<ActionResult<List<CaseOpeningUpgradeDefinitionObj>>> GetInventoryUpgradeSettings(CancellationToken cancellationToken)
    {
        return Execute(() => _caseOpening.GetInventoryUpgradeSettings(cancellationToken), "load inventory upgrade settings", "all");
    }

    [HttpPut("settings/inventory-upgrades/{upgradeKey}")]
    [Authorize(Policy = AppAuthorizationPolicies.AdminOnly)]
    public async Task<ActionResult<ApiResponse>> UpdateInventoryUpgradeSettings(
        string upgradeKey,
        [FromBody] CaseOpeningInventoryUpgradeSettingsRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _caseOpening.SetInventoryUpgradeSettings(upgradeKey, request.CostStars, request.RequiredLevel, cancellationToken);
            return Ok(new ApiResponse(true, "Inventory upgrade settings saved."));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new ApiResponse(false, exception.Message));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to update inventory upgrade settings for {UpgradeKey}.", upgradeKey);
            return StatusCode(502, new ApiResponse(false, "The inventory upgrade settings could not be saved."));
        }
    }

    [HttpPut("settings/xp-by-rarity/{rarityKey}")]
    [Authorize(Policy = AppAuthorizationPolicies.AdminOnly)]
    public async Task<ActionResult<ApiResponse>> UpdateXpByRarity(
        string rarityKey,
        [FromBody] CaseOpeningXpByRarityRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _caseOpening.SetXpByRarity(rarityKey, request.XpAwarded, cancellationToken);
            return Ok(new ApiResponse(true, "XP reward saved."));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new ApiResponse(false, exception.Message));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to update XP by rarity for {RarityKey}.", rarityKey);
            return StatusCode(502, new ApiResponse(false, "This XP reward could not be saved. Please try again shortly."));
        }
    }

    // ---------- Testing overrides for your own account (the variable-tweak modal's "Your progress" tab) ----------

    [HttpPut("dev/progress")]
    [Authorize(Policy = AppAuthorizationPolicies.AdminOnly)]
    public Task<ActionResult<CaseOpeningProgressObj>> SetDevProgress(
        [FromBody] CaseOpeningDevProgressRequest request,
        CancellationToken cancellationToken)
    {
        return Execute(() => _caseOpening.SetDevProgress(UserId, request.Stars, request.Xp, cancellationToken), "set dev progress", "all");
    }

    [HttpPut("dev/upgrades")]
    [Authorize(Policy = AppAuthorizationPolicies.AdminOnly)]
    public Task<ActionResult<CaseOpeningProgressObj>> SetDevUpgrades(
        [FromBody] CaseOpeningDevUpgradesRequest request,
        CancellationToken cancellationToken)
    {
        return Execute(
            () => _caseOpening.SetDevUpgrades(UserId, request.SkipAnimationUnlocked, request.MultiOpenLevel, request.OpenSpeedLevel, cancellationToken),
            "set dev upgrades",
            "all");
    }

    [HttpPut("dev/cases/{caseKey}")]
    [Authorize(Policy = AppAuthorizationPolicies.AdminOnly)]
    public Task<ActionResult<CaseOpeningProgressObj>> SetDevCaseUnlock(
        string caseKey,
        [FromBody] CaseOpeningDevCaseUnlockRequest request,
        CancellationToken cancellationToken)
    {
        return Execute(() => _caseOpening.SetDevCaseUnlock(UserId, caseKey, request.Unlock, cancellationToken), "set dev case unlock", caseKey);
    }

    [HttpPost("dev/reset")]
    [Authorize(Policy = AppAuthorizationPolicies.AdminOnly)]
    public Task<ActionResult<CaseOpeningProgressObj>> ResetDevProgress(CancellationToken cancellationToken)
    {
        return Execute(() => _caseOpening.ResetDevProgress(UserId, cancellationToken), "reset dev progress", "all");
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

public sealed record CaseOpeningCaseSettingsRequest(int UnlockCostStars, int PurchaseCostStars, int XpRequirement);
public sealed record CaseOpeningInventoryUpgradeSettingsRequest(int CostStars, int RequiredLevel);
public sealed record CaseOpeningXpByRarityRequest(int XpAwarded);
public sealed record CaseOpeningDevProgressRequest(int Stars, int Xp);
public sealed record CaseOpeningDevUpgradesRequest(bool SkipAnimationUnlocked, int MultiOpenLevel, int OpenSpeedLevel);
public sealed record CaseOpeningDevCaseUnlockRequest(bool Unlock);
