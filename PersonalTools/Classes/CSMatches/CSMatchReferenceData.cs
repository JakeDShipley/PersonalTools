using System.Text.Json;
using global::PersonalTools.Entities.CSMatches;

namespace PersonalTools.Classes.CSMatches
{
    public interface ICSMatchReferenceData
    {
        Task<List<CSMapObj>> GetMaps();
        Task<List<string>> GetGameTypes();
        Task AddMap(CSMapObj map);
    }

    public class CSMatchReferenceData : ICSMatchReferenceData
    {
        private const string MapsFileName = "Maps.json";
        private const string GameTypesFileName = "GameType.json";

        private readonly IWebHostEnvironment _env;

        public CSMatchReferenceData(IWebHostEnvironment env)
        {
            _env = env;
        }

        private string FolderPath => Path.Combine(_env.ContentRootPath, "Data", "CSMatches");

        public async Task<List<CSMapObj>> GetMaps()
        {
            string filePath = Path.Combine(FolderPath, MapsFileName);

            if (!File.Exists(filePath))
            {
                return new List<CSMapObj>();
            }

            string json = await File.ReadAllTextAsync(filePath);

            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<CSMapObj>();
            }

            return JsonSerializer.Deserialize<List<CSMapObj>>(json) ?? new List<CSMapObj>();
        }

        public async Task<List<string>> GetGameTypes()
        {
            string filePath = Path.Combine(FolderPath, GameTypesFileName);

            if (!File.Exists(filePath))
            {
                return new List<string>();
            }

            string json = await File.ReadAllTextAsync(filePath);

            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<string>();
            }

            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }

        public async Task AddMap(CSMapObj map)
        {
            List<CSMapObj> maps = await GetMaps();
            maps.Add(map);

            string filePath = Path.Combine(FolderPath, MapsFileName);
            string json = JsonSerializer.Serialize(maps, new JsonSerializerOptions { WriteIndented = true });

            await File.WriteAllTextAsync(filePath, json);
        }
    }
}