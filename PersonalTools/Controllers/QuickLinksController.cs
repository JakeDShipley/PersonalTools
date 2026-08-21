using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalTools.Classes;
using PersonalTools.Entities;

namespace PersonalTools.Controllers;

[Authorize]
[ApiController]
[Route("api/quick-links")]
public sealed class QuickLinksController : ControllerBase
{
    private readonly IQuickLinksFuncs _quickLinks;
    public QuickLinksController(IQuickLinksFuncs quickLinks) => _quickLinks = quickLinks;
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpGet] public async Task<ActionResult<List<QuickLink>>> Get() => Ok(await _quickLinks.GetQuickLinks(UserId));
    [HttpPost] public async Task<ActionResult<ApiResponse>> Create([FromBody] QuickLinkRequest request) { Guid id = await _quickLinks.CreateQuickLink(UserId, request.Title, request.Url, request.IconClass); return Ok(new ApiResponse(true, id.ToString("D"))); }
    [HttpPut("{quickLinkId:guid}")] public async Task<ActionResult<ApiResponse>> Update(Guid quickLinkId, [FromBody] QuickLinkRequest request) { await _quickLinks.UpdateQuickLink(UserId, quickLinkId, request.Title, request.Url, request.IconClass); return Ok(new ApiResponse(true, "Quick link updated.")); }
    [HttpDelete("{quickLinkId:guid}")] public async Task<ActionResult<ApiResponse>> Delete(Guid quickLinkId) { await _quickLinks.DeleteQuickLink(UserId, quickLinkId); return Ok(new ApiResponse(true, "Quick link removed.")); }
    [HttpPut("order")] public async Task<ActionResult<ApiResponse>> UpdateOrder([FromBody] QuickLinkOrderRequest request, CancellationToken cancellationToken) { await _quickLinks.UpdateOrder(UserId, request.QuickLinkIds ?? [], cancellationToken); return Ok(new ApiResponse(true, "Quick link order saved.")); }
}
public sealed record QuickLinkRequest(string Title, string Url, string? IconClass);
public sealed record QuickLinkOrderRequest(IReadOnlyList<Guid>? QuickLinkIds);
