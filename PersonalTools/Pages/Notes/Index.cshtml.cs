using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PersonalTools.Classes.Notes;
using PersonalTools.Entities.Notes;
using System.Security.Claims;

namespace PersonalTools.Pages.Notes
{
    public class IndexModel : PageModel
    {
        private readonly INoteFuncs _noteFuncs;

        public IndexModel(INoteFuncs noteFuncs)
        {
            _noteFuncs = noteFuncs;
        }

        public List<NoteObj> Notes { get; set; } = new();

        [BindProperty]
        public Guid NoteId { get; set; }

        [BindProperty]
        public string Title { get; set; } = string.Empty;

        [BindProperty]
        public string Body { get; set; } = string.Empty;

        public string ErrorMessage { get; set; } = string.Empty;
        public string SuccessMessage { get; set; } = string.Empty;

        public async Task OnGet()
        {
            Notes = await _noteFuncs.GetNotes(UserId);
        }

        public async Task<IActionResult> OnPostCreate()
        {
            if (string.IsNullOrWhiteSpace(Title))
            {
                ErrorMessage = "Please enter a note title.";
                Notes = await _noteFuncs.GetNotes(UserId);
                return Page();
            }

            if (string.IsNullOrWhiteSpace(Body))
            {
                ErrorMessage = "Please enter some note content.";
                Notes = await _noteFuncs.GetNotes(UserId);
                return Page();
            }

            await _noteFuncs.CreateNote(UserId, Title, Body);

            TempData["SuccessMessage"] = "Note added successfully.";

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDelete(Guid noteId)
        {
            if (noteId != Guid.Empty)
            {
                await _noteFuncs.DeleteNote(UserId, noteId);
                TempData["SuccessMessage"] = "Note deleted successfully.";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostEdit()
        {
            if (NoteId == Guid.Empty)
            {
                ErrorMessage = "Could not find the note to update.";
                Notes = await _noteFuncs.GetNotes(UserId);
                return Page();
            }

            if (string.IsNullOrWhiteSpace(Title))
            {
                ErrorMessage = "Please enter a note title.";
                Notes = await _noteFuncs.GetNotes(UserId);
                return Page();
            }

            if (string.IsNullOrWhiteSpace(Body))
            {
                ErrorMessage = "Please enter some note content.";
                Notes = await _noteFuncs.GetNotes(UserId);
                return Page();
            }

            await _noteFuncs.UpdateNote(UserId, NoteId, Title, Body);

            TempData["SuccessMessage"] = "Note updated successfully.";

            return RedirectToPage();
        }

        private long UserId => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }
}
