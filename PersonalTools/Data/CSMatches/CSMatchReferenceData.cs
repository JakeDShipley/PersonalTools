using System.Text.Json;
using MySqlConnector;
using PersonalTools.Data;
using global::PersonalTools.Entities.CSMatches;

namespace PersonalTools.Classes.CSMatches
{
    public interface ICSMatchReferenceData
    {
        Task<List<CSMapObj>> GetMaps();
        Task<List<string>> GetGameTypes();
        Task AddMap(CSMapObj map);
        Task<List<string>> GetActiveDutyPool();
        Task SetActiveDutyPool(List<string> mapNames);
    }

    public class CSMatchReferenceData : ICSMatchReferenceData
    {
        private const string MapsFileName = "Maps.json";
        private const string GameTypesFileName = "GameType.json";
        private readonly IWebHostEnvironment _env;
        private readonly IMariaDbDataAccess _database;

        public CSMatchReferenceData(IWebHostEnvironment env, IMariaDbDataAccess database)
        {
            _env = env;
            _database = database;
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

        public async Task<List<string>> GetActiveDutyPool()
        {
            return await _database.GetBulkDataSP(
                "sp_cs_active_duty_maps_get",
                reader => reader.GetString("MapName"));
        }

        public async Task SetActiveDutyPool(List<string> mapNames)
        {
            HashSet<string> knownMaps = (await GetMaps())
                .Select(map => map.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            List<string> selectedMaps = mapNames
                .Where(map => !string.IsNullOrWhiteSpace(map) && knownMaps.Contains(map))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            await _database.ExecuteSP(
                "sp_cs_active_duty_maps_set",
                [new MySqlParameter("p_map_names", JsonSerializer.Serialize(selectedMaps))]);
        }
    }
}
