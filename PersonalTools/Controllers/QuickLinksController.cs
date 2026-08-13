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
    private long UserId => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpGet] public async Task<ActionResult<List<QuickLink>>> Get() => Ok(await _quickLinks.GetQuickLinks(UserId));
    [HttpPost] public async Task<ActionResult<ApiResponse>> Create([FromBody] QuickLinkRequest request) { long id = await _quickLinks.CreateQuickLink(UserId, request.Title, request.Url, request.IconClass); return Ok(new ApiResponse(true, id.ToString())); }
    [HttpPut("{quickLinkId:long}")] public async Task<ActionResult<ApiResponse>> Update(long quickLinkId, [FromBody] QuickLinkRequest request) { await _quickLinks.UpdateQuickLink(UserId, quickLinkId, request.Title, request.Url, request.IconClass); return Ok(new ApiResponse(true, "Quick link updated.")); }
    [HttpDelete("{quickLinkId:long}")] public async Task<ActionResult<ApiResponse>> Delete(long quickLinkId) { await _quickLinks.DeleteQuickLink(UserId, quickLinkId); return Ok(new ApiResponse(true, "Quick link removed.")); }
}
public sealed record QuickLinkRequest(string Title, string Url, string? IconClass);
