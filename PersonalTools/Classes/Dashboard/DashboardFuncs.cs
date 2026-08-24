using PersonalTools.Entities.Dashboard;

namespace PersonalTools.Classes.Dashboard
{
    public interface IDashboardFuncs
    {
        List<DashboardToolObj> GetDashboardTools(bool includeAdminTools);
    }

    public class DashboardFuncs : IDashboardFuncs
    {
        public List<DashboardToolObj> GetDashboardTools(bool includeAdminTools)
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
                    Title = "CS2 Demo Library",
                    Description = "Open or download the latest available CS2 replays directly to your device.",
                    IconClass = "fa-solid fa-film",
                    PageUrl = "/CSDemos",
                    ButtonText = "Browse demos",
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
                    Title = "CS2 Case Simulator",
                    Description = "Open a simulated Dreams & Nightmares case and collect the results without spending money.",
                    IconClass = "fa-solid fa-box-open",
                    PageUrl = "/CaseOpening",
                    ButtonText = "Open a case",
                    Category = "Workspace",
                },
                new DashboardToolObj
                {
                    Title = "Paste Bin",
                    Description = "Share text, code and files with signed-in Personal Tools users.",
                    IconClass = "fa-regular fa-paste",
                    PageUrl = "/PasteBin",
                    ButtonText = "Open Paste Bin",
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
                    Title = "CS Match Tracker",
                    Description = "Track your CS2 match results and stats.",
                    IconClass = "fa-solid fa-crosshairs",
                    PageUrl = "/CSMatches",
                    ButtonText = "Open",
                    Category = "Blits"
                },
            }.Where(tool => includeAdminTools || tool.PageUrl is not "/CSDemos" and not "/PasteBin" and not "/MediaExtractor").ToList();
        }
    }
}
