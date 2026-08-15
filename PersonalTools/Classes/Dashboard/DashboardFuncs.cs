using PersonalTools.Entities.Dashboard;

namespace PersonalTools.Classes.Dashboard
{
    public interface IDashboardFuncs
    {
        List<DashboardToolObj> GetDashboardTools();
    }

    public class DashboardFuncs : IDashboardFuncs
    {
        public List<DashboardToolObj> GetDashboardTools()
        {
            return new List<DashboardToolObj>
            {
                new DashboardToolObj
                {
                    Title = "Steam Inventory Lookup",
                    Description = "Look up Steam inventory information and inspect item data.",
                    IconClass = "fa-brands fa-steam",
                    PageUrl = "/Inventory",
                    ButtonText = "Open",
                    Category = "Workspace",
                },
                new DashboardToolObj
                {
                    Title = "CS2 Player Stats",
                    Description = "View ranks, match record and detailed Leetify performance signals for CS2 players.",
                    IconClass = "fa-solid fa-chart-simple",
                    PageUrl = "/CSStats",
                    ButtonText = "View stats",
                    Category = "Workspace",
                },
                new DashboardToolObj
                {
                    Title = "CS2 Skin Tracker",
                    Description = "Track skins, prices, purchase dates, notes and other useful details.",
                    IconClass = "fa-solid fa-gun",
                    PageUrl = "/Skins",
                    ButtonText = "Open",
                    Category = "Workspace",
                },
                new DashboardToolObj
                {
                    Title = "Notes",
                    Description = "Write simple blog-style notes and display them as Bootstrap cards.",
                    IconClass = "fa-solid fa-note-sticky",
                    PageUrl = "/Notes",
                    ButtonText = "Open",
                    Category = "Workspace",
                },
                new DashboardToolObj
                {
                    Title = "Media Extractor",
                    Description = "Parse page source and extract images and videos.",
                    IconClass = "fa-solid fa-photo-film",
                    PageUrl = "/MediaExtractor",
                    ButtonText = "Open",
                    Category = "Media"
                },
                new DashboardToolObj
                {
                    Title = "Audio Studio",
                    Description = "Extract, trim and export audio directly in your browser.",
                    IconClass = "fa-solid fa-wave-square",
                    PageUrl = "/AudioStudio",
                    ButtonText = "Open studio",
                    Category = "Media"
                },
                new DashboardToolObj
                {
                    Title = "Grand Exchange Price Checker",
                    Description = "Search realtime OSRS Grand Exchange prices.",
                    IconClass = "fa-solid fa-coins",
                    PageUrl = "/GrandExchange",
                    ButtonText = "Open",
                    Category = "Workspace"
                },
                new DashboardToolObj
                {
                    Title = "CS Match Tracker",
                    Description = "Track your CS2 match results and stats.",
                    IconClass = "fa-solid fa-crosshairs",
                    PageUrl = "/CSMatches",
                    ButtonText = "Open",
                    Category = "Blits"
                },
                new DashboardToolObj
                {
                    Title = "Server Monitor",
                    Description = "Review live server health and resource usage without exposing sensitive system details.",
                    IconClass = "fa-solid fa-server",
                    PageUrl = "/Monitoring/Server",
                    ButtonText = "View health",
                    Category = "System"
                },
                new DashboardToolObj
                {
                    Title = "Database Monitor",
                    Description = "Inspect safe MariaDB availability, workload and capacity indicators.",
                    IconClass = "fa-solid fa-database",
                    PageUrl = "/Monitoring/Database",
                    ButtonText = "View health",
                    Category = "System"
                },
            };
        }
    }
}
