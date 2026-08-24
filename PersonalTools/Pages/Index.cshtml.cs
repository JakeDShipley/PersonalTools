using Microsoft.AspNetCore.Mvc.RazorPages;
using PersonalTools.Classes.Dashboard;
using PersonalTools.Entities.Dashboard;
using PersonalTools.Security;

namespace PersonalTools.Pages
{
    public class IndexModel : PageModel
    {
        private readonly IDashboardFuncs _dashboardFuncs;

        public IndexModel(IDashboardFuncs dashboardFuncs)
        {
            _dashboardFuncs = dashboardFuncs;
        }

        public List<DashboardToolObj> Tools { get; set; } = new();

        public void OnGet()
        {
            Tools = _dashboardFuncs.GetDashboardTools(User.IsUserAllowedHere(AppRole.Admin));
        }
    }
}
