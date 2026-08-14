using System.Text.Json;

namespace PersonalTools.Data.CSMatches
{
    public interface IMapPoolSuggestionData
    {
        Task<string?> GetMapsArticleExtract();
    }

    public class MapPoolSuggestionData : IMapPoolSuggestionData
    {
        private readonly HttpClient _httpClient;

        public MapPoolSuggestionData(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<string?> GetMapsArticleExtract()
        {
            try
            {
                using HttpResponseMessage response = await _httpClient.GetAsync("w/api.php?action=query&prop=extracts&explaintext=1&titles=List_of_competitive_Counter-Strike_maps&format=json");

                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                using Stream stream = await response.Content.ReadAsStreamAsync();
                using JsonDocument document = await JsonDocument.ParseAsync(stream);

                JsonElement pages = document.RootElement.GetProperty("query").GetProperty("pages");

                foreach (JsonProperty page in pages.EnumerateObject())
                {
                    if (page.Value.TryGetProperty("extract", out JsonElement extract))
                    {
                        return extract.GetString();
                    }
                }

                return null;
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
            {
                return null;
            }
        }
    }
}
