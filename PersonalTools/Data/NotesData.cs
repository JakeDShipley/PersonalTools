using MySqlConnector;
using PersonalTools.Entities.Notes;
using System.Text.Json;

namespace PersonalTools.Data;

public interface INotesData
{
    Task<List<NoteDbModel>> GetNotes(Guid userId, CancellationToken cancellationToken = default);
    Task CreateNote(Guid userId, NoteDbModel note, CancellationToken cancellationToken = default);
    Task UpdateNote(Guid userId, NoteDbModel note, CancellationToken cancellationToken = default);
    Task DeleteNote(Guid userId, Guid noteId, CancellationToken cancellationToken = default);
    Task UpdateOrder(Guid userId, IReadOnlyList<Guid> noteIds, CancellationToken cancellationToken = default);
}

public sealed class NotesData : INotesData
{
    private readonly IMariaDbDataAccess _database;
    public NotesData(IMariaDbDataAccess database) => _database = database;

    /// <summary>
    /// Reads only the requesting user's notes. The stored procedure repeats this ownership
    /// constraint so an identifier supplied by the browser can never cross user boundaries.
    /// </summary>
    public Task<List<NoteDbModel>> GetNotes(Guid userId, CancellationToken cancellationToken = default) =>
        _database.GetBulkDataSP("sp_notes_get", ReadDbModel, Parameters(("p_user_id", userId)), cancellationToken);

    public async Task CreateNote(Guid userId, NoteDbModel note, CancellationToken cancellationToken = default) =>
        await _database.ExecuteSP("sp_notes_create", Parameters(("p_user_id", userId), ("p_note_id", note.NoteId.ToString("D")), ("p_title", note.Title), ("p_body", note.Body)), cancellationToken);

    public async Task UpdateNote(Guid userId, NoteDbModel note, CancellationToken cancellationToken = default) =>
        await _database.ExecuteSP("sp_notes_update", Parameters(("p_user_id", userId), ("p_note_id", note.NoteId.ToString("D")), ("p_title", note.Title), ("p_body", note.Body)), cancellationToken);

    public async Task DeleteNote(Guid userId, Guid noteId, CancellationToken cancellationToken = default) =>
        await _database.ExecuteSP("sp_notes_delete", Parameters(("p_user_id", userId), ("p_note_id", noteId.ToString("D"))), cancellationToken);

    public Task UpdateOrder(Guid userId, IReadOnlyList<Guid> noteIds, CancellationToken cancellationToken = default) =>
        _database.ExecuteSP("sp_notes_set_order_bulk", Parameters(("p_user_id", userId), ("p_note_ids", JsonSerializer.Serialize(noteIds))), cancellationToken);

    private static MySqlParameter[] Parameters(params (string Name, object Value)[] values) =>
        values.Select(value => new MySqlParameter(value.Name, value.Value)).ToArray();

    /// <summary>
    /// Materialises the precise stored-procedure result shape. MySqlConnector requires typed
    /// column reads, so this is intentionally the sole low-level mapping in this module;
    /// Data never maps directly to the API model.
    /// </summary>
    private static NoteDbModel ReadDbModel(MySqlDataReader reader) => new()
    {
        NoteId = reader.GetGuid("NoteId"),
        Title = reader.GetString("Title"),
        Body = reader.GetString("Body"),
        SortOrder = reader.GetInt32("SortOrder"),
        Created = reader.GetDateTime("CreatedUtc"),
        Updated = reader.GetDateTime("UpdatedUtc")
    };
}
