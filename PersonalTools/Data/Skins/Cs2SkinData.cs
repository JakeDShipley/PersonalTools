using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using PersonalTools.Entities.Skins;

namespace PersonalTools.Data.Skins
{
    public interface ICs2SkinData
    {
        Task<List<Cs2ApiSkinObj>> GetApiSkins();
        Task SaveLocalSkins(List<Cs2LocalSkinObj> skins);
        Task<List<Cs2LocalSkinObj>> SearchLocalSkins(string searchTerm, int take = 30);
    }

    public class Cs2SkinData : ICs2SkinData
    {
        private const string ApiUrl = "https://raw.githubusercontent.com/ByMykel/CSGO-API/main/public/api/en/skins_not_grouped.json";
        private const string LocalFileName = "cs2-skins.json";
        private readonly IWebHostEnvironment _environment;
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;

        public Cs2SkinData(IWebHostEnvironment environment, HttpClient httpClient, IMemoryCache cache)
        {
            _environment = environment;
            _httpClient = httpClient;
            _cache = cache;
        }

        public async Task<List<Cs2ApiSkinObj>> GetApiSkins()
        {
            string json = await _httpClient.GetStringAsync(ApiUrl);
            return JsonSerializer.Deserialize<List<Cs2ApiSkinObj>>(json) ?? new List<Cs2ApiSkinObj>();
        }

        public async Task SaveLocalSkins(List<Cs2LocalSkinObj> skins)
        {
            string folderPath = Path.Combine(_environment.WebRootPath, "data");
            Directory.CreateDirectory(folderPath);
            string filePath = Path.Combine(folderPath, LocalFileName);
            string json = JsonSerializer.Serialize(skins, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json);
            _cache.Remove(LocalFileName);
        }

        public async Task<List<Cs2LocalSkinObj>> SearchLocalSkins(string searchTerm, int take = 30)
        {
            List<Cs2LocalSkinObj> skins = await _cache.GetOrCreateAsync(LocalFileName, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6);
                string filePath = Path.Combine(_environment.WebRootPath, "data", LocalFileName);
                if (!File.Exists(filePath)) return new List<Cs2LocalSkinObj>();
                return JsonSerializer.Deserialize<List<Cs2LocalSkinObj>>(await File.ReadAllTextAsync(filePath)) ?? new List<Cs2LocalSkinObj>();
            }) ?? new List<Cs2LocalSkinObj>();

            string term = searchTerm?.Trim() ?? string.Empty;
            return skins.Where(s => string.IsNullOrEmpty(term) || s.MarketHashName.Contains(term, StringComparison.OrdinalIgnoreCase))
                .OrderBy(s => s.MarketHashName).Take(Math.Clamp(take, 1, 50)).ToList();
        }
    }
}
