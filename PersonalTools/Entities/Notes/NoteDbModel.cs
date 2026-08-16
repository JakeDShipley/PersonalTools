namespace PersonalTools.Entities.Notes;

/// <summary>
/// Database transport shape returned by the Notes stored procedures.
///
/// This remains separate from <see cref="NoteObj"/> so database-column materialisation
/// stays in the Data layer and the public/API model remains owned by the application layer.
/// Mapster performs the identical-property conversion in <c>NoteFuncs</c>.
/// </summary>
public sealed class NoteDbModel
{
    public Guid NoteId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public DateTime Created { get; set; }
    public DateTime Updated { get; set; }
}
