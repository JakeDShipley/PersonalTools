namespace PersonalTools.Entities.Tracker
{
    public class TrackerItemObj
    {
        public Guid ItemId { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Area { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public string? CreatedByDisplayName { get; set; }
        public Guid? AssignedToUserId { get; set; }
        public string? AssignedToDisplayName { get; set; }
        public bool ShowOnDashboard { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime UpdatedUtc { get; set; }
    }
}
