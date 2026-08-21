using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalTools.Classes.Notes;
using PersonalTools.Entities.Notes;

namespace PersonalTools.Controllers;

[Authorize]
[ApiController]
[Route("api/notes")]
public sealed class NotesController : ControllerBase
{
    private readonly INoteFuncs _notes;
    public NotesController(INoteFuncs notes) => _notes = notes;
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<ActionResult<List<NoteObj>>> Get(CancellationToken cancellationToken) =>
        Ok(await _notes.GetNotes(UserId, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<ApiResponse>> Create([FromForm] string title, [FromForm] string body, CancellationToken cancellationToken)
    {
        await _notes.CreateNote(UserId, title, body, cancellationToken);
        return Ok(new ApiResponse(true, "Note added."));
    }

    [HttpPut("{noteId}")]
    public async Task<ActionResult<ApiResponse>> Update(Guid noteId, [FromForm] string title, [FromForm] string body, CancellationToken cancellationToken)
    {
        await _notes.UpdateNote(UserId, noteId, title, body, cancellationToken);
        return Ok(new ApiResponse(true, "Note updated."));
    }

    [HttpDelete("{noteId}")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid noteId, CancellationToken cancellationToken)
    {
        await _notes.DeleteNote(UserId, noteId, cancellationToken);
        return Ok(new ApiResponse(true, "Note deleted."));
    }

    [HttpPut("order")]
    public async Task<ActionResult<ApiResponse>> UpdateOrder([FromBody] NoteOrderRequest request, CancellationToken cancellationToken)
    {
        await _notes.UpdateOrder(UserId, request.NoteIds ?? [], cancellationToken);
        return Ok(new ApiResponse(true, "Note order saved."));
    }
}

public sealed record NoteOrderRequest(IReadOnlyList<Guid>? NoteIds);
