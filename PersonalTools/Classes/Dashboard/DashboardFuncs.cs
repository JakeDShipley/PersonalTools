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
                    ButtonText = "Open inventory lookup"
                },
                new DashboardToolObj
                {
                    Title = "CS2 Skin Tracker",
                    Description = "Track skins, prices, purchase dates, notes and other useful details.",
                    IconClass = "fa-solid fa-gun",
                    PageUrl = "/Skins",
                    ButtonText = "Open skin tracker"
                },
                new DashboardToolObj
                {
                    Title = "Notes",
                    Description = "Write simple blog-style notes and display them as Bootstrap cards.",
                    IconClass = "fa-solid fa-note-sticky",
                    PageUrl = "/Notes",
                    ButtonText = "Open notes"
                },
                new DashboardToolObj
                {
                    Title = "Media Extractor",
                    Description = "Parse page source and extract images and videos.",
                    IconClass = "fa-solid fa-photo-film",
                    PageUrl = "/MediaExtractor",
                    ButtonText = "Open"
                },
                new DashboardToolObj
                {
                    Title = "Grand Exchange Price Checker",
                    Description = "Search realtime OSRS Grand Exchange prices.",
                    IconClass = "fa-solid fa-coins",
                    PageUrl = "/GrandExchange",
                    ButtonText = "Open"
                },
            };
        }
    }
}