using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalTools.Classes.MediaExtractor;
using PersonalTools.Entities.MediaExtractor;
using PersonalTools.Security;

namespace PersonalTools.Controllers;

[Authorize(Policy = AppAuthorizationPolicies.AdminOnly)]
[ApiController]
[Route("api/media-extractor")]
public sealed class MediaExtractorController : ControllerBase
{
    private readonly IMediaExtractorFuncs _mediaExtractor;
    public MediaExtractorController(IMediaExtractorFuncs mediaExtractor) => _mediaExtractor = mediaExtractor;
    [HttpPost("parse")] public async Task<ActionResult<List<MediaItemObj>>> Parse([FromBody] MediaParseRequest request) => string.IsNullOrWhiteSpace(request.Source) ? BadRequest(new ApiResponse(false, "Paste a page URL or source.")) : Ok(await _mediaExtractor.Parse(request.Source));
}

public sealed record MediaParseRequest(string Source);
