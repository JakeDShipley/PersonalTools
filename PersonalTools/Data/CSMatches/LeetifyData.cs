using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Memory;
using PersonalTools.Entities.CSMatches;

namespace PersonalTools.Data.CSMatches
{
    public interface ILeetifyData
    {
        Task<List<LeetifyMatchModel>> GetMatches(string steam64Id);
    }

    public class LeetifyData : ILeetifyData
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;

        public LeetifyData(HttpClient httpClient, IMemoryCache cache)
        {
            _httpClient = httpClient;
            _cache = cache;
        }

        public async Task<List<LeetifyMatchModel>> GetMatches(string steam64Id)
        {
            return await _cache.GetOrCreateAsync($"leetify-matches-{steam64Id}", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2);

                try
                {
                    List<LeetifyMatchModel>? matches = await _httpClient.GetFromJsonAsync<List<LeetifyMatchModel>>($"v3/profile/matches?steam64_id={Uri.EscapeDataString(steam64Id)}");
                    return matches ?? new List<LeetifyMatchModel>();
                }
                catch (Exception ex) when (ex is HttpRequestException or System.Text.Json.JsonException or TaskCanceledException)
                {
                    throw new InvalidOperationException("Leetify could not be reached right now. Please try again.");
                }
            }) ?? new List<LeetifyMatchModel>();
        }
    }
}
