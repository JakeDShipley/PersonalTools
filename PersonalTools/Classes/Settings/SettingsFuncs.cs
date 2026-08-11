namespace PersonalTools.Classes.Settings
{
    public interface ISettingsFuncs
    {
        Task DeleteAllData();
    }

    public class SettingsFuncs : ISettingsFuncs
    {
        private readonly IWebHostEnvironment _env;

        public SettingsFuncs(IWebHostEnvironment env)
        {
            _env = env;
        }

        public Task DeleteAllData()
        {
            string appDataPath = Path.Combine(_env.ContentRootPath, "App_Data");

            if (!Directory.Exists(appDataPath))
            {
                return Task.CompletedTask;
            }

            foreach (string file in Directory.EnumerateFiles(appDataPath, "*.json"))
            {
                File.Delete(file);
            }

            return Task.CompletedTask;
        }
    }
}


