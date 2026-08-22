using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalTools.Classes.PasteBin;
using PersonalTools.Entities.PasteBin;

namespace PersonalTools.Controllers;

[Authorize]
[ApiController]
[Route("api/paste-bin")]
public sealed class PasteBinController : ControllerBase
{
    private readonly IPasteBinFuncs _pasteBin;
    private readonly ILogger<PasteBinController> _logger;

    public PasteBinController(IPasteBinFuncs pasteBin, ILogger<PasteBinController> logger)
    {
        _pasteBin = pasteBin;
        _logger = logger;
    }

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string DisplayName => User.Identity?.Name ?? "Personal Tools user";

    [HttpGet("pastes")]
    public async Task<ActionResult<List<PasteBinPasteObj>>> GetPasteBinPastes(CancellationToken cancellationToken)
    {
        try { return Ok(await _pasteBin.GetPasteBinPastes(UserId, cancellationToken)); }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Paste Bin list failed for user {UserId}.", UserId);
            return StatusCode(500, new ApiResponse(false, "The Paste Bin could not be loaded. Please try again."));
        }
    }

    [HttpGet("pastes/{shortCode}")]
    public async Task<ActionResult<PasteBinPasteObj>> GetPasteByShortCode(string shortCode, CancellationToken cancellationToken)
    {
        try { return Ok(await _pasteBin.GetPasteByShortCode(UserId, shortCode, cancellationToken)); }
        catch (PasteBinAccessException exception) { return StatusCode(exception.StatusCode, new ApiResponse(false, exception.Message)); }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Paste Bin entry {ShortCode} failed for user {UserId}.", shortCode, UserId);
            return StatusCode(500, new ApiResponse(false, "The paste could not be loaded. Please try again."));
        }
    }

    [HttpPost("pastes")]
    [RequestSizeLimit(67_108_864)]
    public async Task<ActionResult<PasteBinCreateResult>> CreatePaste([FromForm] PasteBinCreateRequest request, IFormFile? attachment, CancellationToken cancellationToken)
    {
        try { return Ok(await _pasteBin.CreatePaste(UserId, DisplayName, request, attachment, cancellationToken)); }
        catch (PasteBinAccessException exception) { return StatusCode(exception.StatusCode, new ApiResponse(false, exception.Message)); }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Paste Bin creation failed for user {UserId}.", UserId);
            return StatusCode(500, new ApiResponse(false, "The paste could not be created. Please try again."));
        }
    }

    [HttpPost("pastes/{shortCode}/unlock")]
    public async Task<ActionResult<ApiResponse>> UnlockPaste(string shortCode, [FromBody] PasteBinUnlockRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await _pasteBin.UnlockPaste(UserId, shortCode, request.Password, cancellationToken);
            return Ok(new ApiResponse(true, "Paste unlocked for 20 minutes."));
        }
        catch (PasteBinAccessException exception) { return StatusCode(exception.StatusCode, new ApiResponse(false, exception.Message)); }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Paste Bin unlock failed for user {UserId} and short code {ShortCode}.", UserId, shortCode);
            return StatusCode(500, new ApiResponse(false, "The paste could not be unlocked. Please try again."));
        }
    }

    [HttpDelete("pastes/{pasteId:guid}")]
    public async Task<ActionResult<ApiResponse>> DeletePaste(Guid pasteId, CancellationToken cancellationToken)
    {
        try
        {
            await _pasteBin.DeletePaste(UserId, pasteId, cancellationToken);
            return Ok(new ApiResponse(true, "Paste deleted."));
        }
        catch (PasteBinAccessException exception) { return StatusCode(exception.StatusCode, new ApiResponse(false, exception.Message)); }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Paste Bin deletion failed for user {UserId} and paste {PasteId}.", UserId, pasteId);
            return StatusCode(500, new ApiResponse(false, "The paste could not be deleted. Please try again."));
        }
    }

    [HttpGet("pastes/{shortCode}/file/download")]
    [IgnoreAntiforgeryToken]
    public Task<IActionResult> DownloadFile(string shortCode, CancellationToken cancellationToken) => ReturnFile(shortCode, preview: false, cancellationToken);

    [HttpGet("pastes/{shortCode}/file/preview")]
    [IgnoreAntiforgeryToken]
    public Task<IActionResult> PreviewFile(string shortCode, CancellationToken cancellationToken) => ReturnFile(shortCode, preview: true, cancellationToken);

    [HttpGet("settings")]
    public async Task<ActionResult<PasteBinSettingsDbModel>> GetPasteBinSettings(CancellationToken cancellationToken)
    {
        try { return Ok(await _pasteBin.GetPasteBinSettings(cancellationToken)); }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Paste Bin settings could not be loaded for user {UserId}.", UserId);
            return StatusCode(500, new ApiResponse(false, "The Paste Bin upload setting could not be loaded. Please try again."));
        }
    }

    [HttpPut("settings")]
    public async Task<ActionResult<ApiResponse>> UpdatePasteBinSettings([FromBody] PasteBinSettingsRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await _pasteBin.UpdatePasteBinSettings(request.MaximumUploadSizeMb, cancellationToken);
            return Ok(new ApiResponse(true, "Paste Bin upload limit saved."));
        }
        catch (PasteBinAccessException exception) { return BadRequest(new ApiResponse(false, exception.Message)); }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Paste Bin settings update failed for user {UserId}.", UserId);
            return StatusCode(500, new ApiResponse(false, "The Paste Bin upload setting could not be saved. Please try again."));
        }
    }

    private async Task<IActionResult> ReturnFile(string shortCode, bool preview, CancellationToken cancellationToken)
    {
        try
        {
            (PasteBinPasteDbModel paste, FileStream stream) = await _pasteBin.OpenPasteFile(UserId, shortCode, cancellationToken);
            PasteBinFileDbModel file = paste.File!;
            if (preview && !file.FileExtension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) &&
                !file.FileExtension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) &&
                !file.FileExtension.Equals(".png", StringComparison.OrdinalIgnoreCase) &&
                !file.FileExtension.Equals(".gif", StringComparison.OrdinalIgnoreCase) &&
                !file.FileExtension.Equals(".webp", StringComparison.OrdinalIgnoreCase) &&
                !file.ContentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) &&
                !file.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
            {
                await stream.DisposeAsync();
                return BadRequest(new ApiResponse(false, "This attachment type can only be downloaded."));
            }

            Response.Headers.CacheControl = "private, no-store";
            Response.Headers.XContentTypeOptions = "nosniff";
            return File(stream, file.ContentType, preview ? null : file.OriginalFileName, enableRangeProcessing: true);
        }
        catch (PasteBinAccessException exception) { return StatusCode(exception.StatusCode, new ApiResponse(false, exception.Message)); }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Paste Bin file access failed for user {UserId} and short code {ShortCode}.", UserId, shortCode);
            return StatusCode(500, new ApiResponse(false, "The attachment could not be opened. Please try again."));
        }
    }
}

public sealed record PasteBinUnlockRequest(string Password);
public sealed record PasteBinSettingsRequest(int MaximumUploadSizeMb);
