using MySqlConnector;
using PersonalTools.Entities.Tracker;

namespace PersonalTools.Data.Tracker
{
    public interface ITrackerItemsData
    {
        Task<List<TrackerItemObj>> GetItems();
        Task<List<TrackerItemObj>> GetClosedItems();
        Task CreateItem(string itemId, string type, string title, string description, string area, Guid createdByUserId, string? assignedToUserId, bool showOnDashboard);
        Task UpdateItem(string itemId, string type, string title, string description, string area, string status, string? assignedToUserId, bool showOnDashboard);
        Task MoveItem(string itemId, string status, IReadOnlyList<string> orderedItemIds);
        Task SetStatus(string itemId, string status);
        Task DeleteItem(string itemId);
        Task<List<TrackerAssigneeObj>> GetAssignees();
        Task<TrackerSettingsObj> GetSettings();
        Task SetSettings(int autoCloseAfterDays);
        Task AutoCloseResolvedItems(int autoCloseAfterDays);
    }

    public class TrackerItemsData : ITrackerItemsData
    {
        private readonly IMariaDbDataAccess _database;

        public TrackerItemsData(IMariaDbDataAccess database)
        {
            _database = database;
        }

        public Task<List<TrackerItemObj>> GetItems() => _database.GetBulkDataSP("sp_tracker_items_get", Map);

        public Task<List<TrackerItemObj>> GetClosedItems() => _database.GetBulkDataSP("sp_tracker_items_get_closed", Map);

        public Task CreateItem(string itemId, string type, string title, string description, string area, Guid createdByUserId, string? assignedToUserId, bool showOnDashboard) =>
            _database.ExecuteSP("sp_tracker_items_create", Parameters(
                ("p_item_id", itemId), ("p_type", type), ("p_title", title), ("p_description", description), ("p_area", area),
                ("p_created_by_user_id", createdByUserId), ("p_assigned_to_user_id", assignedToUserId ?? string.Empty), ("p_show_on_dashboard", showOnDashboard)));

        public Task UpdateItem(string itemId, string type, string title, string description, string area, string status, string? assignedToUserId, bool showOnDashboard) =>
            _database.ExecuteSP("sp_tracker_items_update", Parameters(
                ("p_item_id", itemId), ("p_type", type), ("p_title", title), ("p_description", description), ("p_area", area),
                ("p_status", status), ("p_assigned_to_user_id", assignedToUserId ?? string.Empty), ("p_show_on_dashboard", showOnDashboard)));

        public Task MoveItem(string itemId, string status, IReadOnlyList<string> orderedItemIds) =>
            _database.ExecuteSP("sp_tracker_items_move", Parameters(
                ("p_item_id", itemId), ("p_status", status), ("p_item_ids", System.Text.Json.JsonSerializer.Serialize(orderedItemIds))));

        public Task SetStatus(string itemId, string status) =>
            _database.ExecuteSP("sp_tracker_items_set_status", Parameters(("p_item_id", itemId), ("p_status", status)));

        public Task DeleteItem(string itemId) => _database.ExecuteSP("sp_tracker_items_delete", Parameters(("p_item_id", itemId)));

        public Task<List<TrackerAssigneeObj>> GetAssignees() => _database.GetBulkDataSP("sp_tracker_assignees_get", MapAssignee);

        public async Task<TrackerSettingsObj> GetSettings() =>
            await _database.GetDataSP("sp_tracker_settings_get", MapSettings) ?? new TrackerSettingsObj { AutoCloseAfterDays = 5 };

        public Task SetSettings(int autoCloseAfterDays) =>
            _database.ExecuteSP("sp_tracker_settings_set", Parameters(("p_auto_close_after_days", autoCloseAfterDays)));

        public Task AutoCloseResolvedItems(int autoCloseAfterDays) =>
            _database.ExecuteSP("sp_tracker_items_auto_close", Parameters(("p_days", autoCloseAfterDays)));

        private static MySqlParameter[] Parameters(params (string Name, object Value)[] values) =>
            values.Select(value => new MySqlParameter(value.Name, value.Value is Guid id ? id.ToString("D") : value.Value)).ToArray();

        // CHAR(36) columns are auto-detected as Guid by MySqlConnector, so GetString throws here -
        // matches the GetGuid pattern already used for NoteId/SkinId elsewhere in the app.
        private static TrackerItemObj Map(MySqlDataReader reader) => new()
        {
            ItemId = reader.GetGuid("ItemId").ToString(),
            Type = reader.GetString("Type"),
            Title = reader.GetString("Title"),
            Description = reader.GetString("Description"),
            Area = reader.GetString("Area"),
            Status = reader.GetString("Status"),
            SortOrder = reader.GetInt32("SortOrder"),
            CreatedByDisplayName = reader.IsDBNull(reader.GetOrdinal("CreatedByDisplayName")) ? null : reader.GetString("CreatedByDisplayName"),
            AssignedToUserId = reader.IsDBNull(reader.GetOrdinal("AssignedToUserId")) ? null : reader.GetGuid("AssignedToUserId").ToString(),
            AssignedToDisplayName = reader.IsDBNull(reader.GetOrdinal("AssignedToDisplayName")) ? null : reader.GetString("AssignedToDisplayName"),
            ShowOnDashboard = reader.GetBoolean("ShowOnDashboard"),
            CreatedUtc = reader.GetDateTime("CreatedUtc"),
            UpdatedUtc = reader.GetDateTime("UpdatedUtc")
        };

        private static TrackerAssigneeObj MapAssignee(MySqlDataReader reader) => new()
        {
            UserId = reader.GetGuid("UserId").ToString(),
            DisplayName = reader.GetString("DisplayName")
        };

        private static TrackerSettingsObj MapSettings(MySqlDataReader reader) => new()
        {
            AutoCloseAfterDays = reader.GetInt32("AutoCloseAfterDays")
        };
    }
}
