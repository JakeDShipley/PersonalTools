namespace PersonalTools.Entities.Notes
{
    public class NoteObj
    {
        public Guid NoteId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public DateTime Created { get; set; }
        public DateTime Updated { get; set; }
    }
}
