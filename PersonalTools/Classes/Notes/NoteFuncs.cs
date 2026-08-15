using PersonalTools.Data;
using PersonalTools.Entities.Notes;

namespace PersonalTools.Classes.Notes;

public interface INoteFuncs
{
    Task<List<NoteObj>> GetNotes(long userId, CancellationToken cancellationToken = default);
    Task CreateNote(long userId, string title, string body, CancellationToken cancellationToken = default);
    Task UpdateNote(long userId, Guid noteId, string title, string body, CancellationToken cancellationToken = default);
    Task DeleteNote(long userId, Guid noteId, CancellationToken cancellationToken = default);
    Task UpdateOrder(long userId, IReadOnlyList<Guid> noteIds, CancellationToken cancellationToken = default);
}

public sealed class NoteFuncs : INoteFuncs
{
    private readonly INotesData _data;
    public NoteFuncs(INotesData data) => _data = data;

    public Task<List<NoteObj>> GetNotes(long userId, CancellationToken cancellationToken = default) =>
        _data.GetNotes(userId, cancellationToken);

    public Task CreateNote(long userId, string title, string body, CancellationToken cancellationToken = default)
    {
        Validate(title, body);
        return _data.CreateNote(userId, new NoteObj { NoteId = Guid.NewGuid(), Title = title.Trim(), Body = body.Trim() }, cancellationToken);
    }

    public Task UpdateNote(long userId, Guid noteId, string title, string body, CancellationToken cancellationToken = default)
    {
        ValidateId(noteId);
        Validate(title, body);
        return _data.UpdateNote(userId, new NoteObj { NoteId = noteId, Title = title.Trim(), Body = body.Trim() }, cancellationToken);
    }

    public Task DeleteNote(long userId, Guid noteId, CancellationToken cancellationToken = default)
    {
        ValidateId(noteId);
        return _data.DeleteNote(userId, noteId, cancellationToken);
    }

    public Task UpdateOrder(long userId, IReadOnlyList<Guid> noteIds, CancellationToken cancellationToken = default)
    {
        if (noteIds.Count > 1000 || noteIds.Any(id => id == Guid.Empty))
            throw new InvalidOperationException("The note order was invalid.");
        return _data.UpdateOrder(userId, noteIds.Distinct().ToList(), cancellationToken);
    }

    private static void Validate(string title, string body)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 200)
            throw new InvalidOperationException("Enter a note title up to 200 characters.");
        if (string.IsNullOrWhiteSpace(body) || body.Trim().Length > 100_000)
            throw new InvalidOperationException("Enter note content up to 100,000 characters.");
    }

    private static void ValidateId(Guid noteId)
    {
        if (noteId == Guid.Empty) throw new InvalidOperationException("The note identifier was invalid.");
    }
}
