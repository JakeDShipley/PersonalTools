using PersonalTools.Data.Tracker;
using PersonalTools.Entities.Tracker;

namespace PersonalTools.Classes.Tracker
{
    public interface ITrackerFuncs
    {
        Task<List<TrackerItemObj>> GetItems();
        Task<List<TrackerItemObj>> GetClosedItems();
        Task<Guid> CreateItem(Guid userId, string type, string title, string description, string area, Guid? assignedToUserId, bool showOnDashboard);
        Task UpdateItem(Guid itemId, string type, string title, string description, string area, string status, Guid? assignedToUserId, bool showOnDashboard);
        Task MoveItem(Guid itemId, string status, IReadOnlyList<Guid> orderedItemIds);
        Task SetStatus(Guid itemId, string status);
        Task DeleteItem(Guid itemId);
        Task<List<TrackerAssigneeObj>> GetAssignees();
        Task<TrackerSettingsObj> GetSettings();
        Task UpdateSettings(int autoCloseAfterDays);
        Task AutoCloseResolvedItems();
    }

    public class TrackerFuncs : ITrackerFuncs
    {
        // Mirrors the nav's actual sections so filtering by area stays meaningful.
        public static readonly string[] Areas =
        {
            "Dashboard", "Steam Inventory", "CS2 Player Stats", "CS2 Skin Tracker", "Notes", "Grand Exchange",
            "Media Extractor", "Audio Studio", "CS Match Tracker", "Server Monitor", "Database Monitor",
            "Account", "Settings", "General"
        };

        public static readonly string[] Types = { "Bug", "Feature" };

        // Closed items are excluded from the board entirely (sp_tracker_items_get filters them
        // out) - they're reached only via auto-close or by manually setting Status to Closed.
        public static readonly string[] Statuses = { "Open", "InProgress", "Resolved", "Closed", "WontFix" };

        private readonly ITrackerItemsData _data;

        public TrackerFuncs(ITrackerItemsData data)
        {
            _data = data;
        }

        public Task<List<TrackerItemObj>> GetItems() => _data.GetItems();

        public Task<List<TrackerItemObj>> GetClosedItems() => _data.GetClosedItems();

        public async Task<Guid> CreateItem(Guid userId, string type, string title, string description, string area, Guid? assignedToUserId, bool showOnDashboard)
        {
            Validate(type, title, area, null);
            Guid itemId = Guid.NewGuid();
            await _data.CreateItem(itemId, type, title.Trim(), (description ?? string.Empty).Trim(), area, userId, NormaliseAssignee(assignedToUserId), showOnDashboard);
            return itemId;
        }

        public Task UpdateItem(Guid itemId, string type, string title, string description, string area, string status, Guid? assignedToUserId, bool showOnDashboard)
        {
            Validate(type, title, area, status);
            return _data.UpdateItem(itemId, type, title.Trim(), (description ?? string.Empty).Trim(), area, status!, NormaliseAssignee(assignedToUserId), showOnDashboard);
        }

        public Task MoveItem(Guid itemId, string status, IReadOnlyList<Guid> orderedItemIds)
        {
            if (!Statuses.Contains(status)) throw new InvalidOperationException("That status isn't valid.");
            if (itemId == Guid.Empty || orderedItemIds.Count == 0 || orderedItemIds.Count > 500 || orderedItemIds.Any(id => id == Guid.Empty))
                throw new InvalidOperationException("The board order was invalid.");

            return _data.MoveItem(itemId, status, orderedItemIds.Distinct().ToList());
        }

        public Task SetStatus(Guid itemId, string status)
        {
            if (!Statuses.Contains(status)) throw new InvalidOperationException("That status isn't valid.");
            if (itemId == Guid.Empty) throw new InvalidOperationException("That item isn't valid.");

            return _data.SetStatus(itemId, status);
        }

        public Task DeleteItem(Guid itemId) => itemId == Guid.Empty
            ? Task.FromException(new InvalidOperationException("That item isn't valid."))
            : _data.DeleteItem(itemId);

        public Task<List<TrackerAssigneeObj>> GetAssignees() => _data.GetAssignees();

        public Task<TrackerSettingsObj> GetSettings() => _data.GetSettings();

        public Task UpdateSettings(int autoCloseAfterDays)
        {
            if (autoCloseAfterDays < 1 || autoCloseAfterDays > 365) throw new InvalidOperationException("Enter a number of days between 1 and 365.");
            return _data.SetSettings(autoCloseAfterDays);
        }

        public async Task AutoCloseResolvedItems()
        {
            TrackerSettingsObj settings = await _data.GetSettings();
            await _data.AutoCloseResolvedItems(settings.AutoCloseAfterDays);
        }

        // A nullable GUID keeps an unassigned item explicit and avoids accepting arbitrary
        // browser strings as identifiers anywhere beyond the controller model binder.
        private static Guid? NormaliseAssignee(Guid? assignedToUserId) => assignedToUserId == Guid.Empty ? null : assignedToUserId;

        private static void Validate(string type, string title, string area, string? status)
        {
            if (!Types.Contains(type)) throw new InvalidOperationException("Choose whether this is a bug or a feature.");
            if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 200) throw new InvalidOperationException("Enter a title up to 200 characters.");
            if (!Areas.Contains(area)) throw new InvalidOperationException("Choose a valid area.");
            if (status is not null && !Statuses.Contains(status)) throw new InvalidOperationException("That status isn't valid.");
        }
    }
}
