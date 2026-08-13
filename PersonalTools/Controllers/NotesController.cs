using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalTools.Classes.Notes;
using PersonalTools.Entities.Notes;

namespace PersonalTools.Controllers;

[Authorize]
[ApiController]
[Route("api/notes")]
[AutoValidateAntiforgeryToken]
public sealed class NotesController : ControllerBase
{
    private readonly INoteFuncs _noteFuncs;
    public NotesController(INoteFuncs noteFuncs) => _noteFuncs = noteFuncs;

    [HttpGet]
    public async Task<ActionResult<List<NoteObj>>> Get(CancellationToken cancellationToken) => Ok(await _noteFuncs.GetNotes());

    [HttpPost]
    public async Task<ActionResult<ApiResponse>> Create([FromForm] string title, [FromForm] string body)
    {
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(body)) return BadRequest(new ApiResponse(false, "A title and note are required."));
        await _noteFuncs.CreateNote(title, body);
        return Ok(new ApiResponse(true, "Note added."));
    }

    [HttpPut("{noteId}")]
    public async Task<ActionResult<ApiResponse>> Update(string noteId, [FromForm] string title, [FromForm] string body)
    {
        if (string.IsNullOrWhiteSpace(noteId) || string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(body)) return BadRequest(new ApiResponse(false, "A title and note are required."));
        await _noteFuncs.UpdateNote(noteId, title, body);
        return Ok(new ApiResponse(true, "Note updated."));
    }

    [HttpDelete("{noteId}")]
    public async Task<ActionResult<ApiResponse>> Delete(string noteId)
    {
        if (string.IsNullOrWhiteSpace(noteId)) return BadRequest(new ApiResponse(false, "A note is required."));
        await _noteFuncs.DeleteNote(noteId);
        return Ok(new ApiResponse(true, "Note deleted."));
    }
}
