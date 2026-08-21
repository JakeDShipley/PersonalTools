using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using PersonalTools.Entities.CSStats;

namespace PersonalTools.Data.CSStats;

public interface ILeetifyProfileData
{
    Task<LeetifyProfileModel> GetProfile(string steam64Id, CancellationToken cancellationToken = default);
}

public sealed class LeetifyProfileData : ILeetifyProfileData
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;

    public LeetifyProfileData(HttpClient httpClient, IMemoryCache cache)
    {
        _httpClient = httpClient;
        _cache = cache;
    }

    public async Task<LeetifyProfileModel> GetProfile(string steam64Id, CancellationToken cancellationToken = default)
    {
        try
        {
            LeetifyProfileModel? profile = await _cache.GetOrCreateAsync($"leetify-profile-{steam64Id}", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                using HttpResponseMessage response = await _httpClient.GetAsync($"v3/profile?steam64_id={Uri.EscapeDataString(steam64Id)}", cancellationToken);

                if (response.StatusCode == HttpStatusCode.NotFound)
                    throw new InvalidOperationException("No Leetify profile was found for that Steam account.");
                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    throw new InvalidOperationException("Leetify is receiving too many requests. Please wait a moment and try again.");
                if (!response.IsSuccessStatusCode)
                    throw new InvalidOperationException("Leetify could not load that profile right now. Please try again shortly.");

                LeetifyProfileModel? result = await response.Content.ReadFromJsonAsync<LeetifyProfileModel>(cancellationToken: cancellationToken);
                return result ?? throw new InvalidOperationException("Leetify returned an empty profile.");
            });

            return profile ?? throw new InvalidOperationException("Leetify returned an empty profile.");
        }
        catch (HttpRequestException) { throw new InvalidOperationException("Leetify could not be reached right now. Please try again shortly."); }
        catch (JsonException) { throw new InvalidOperationException("Leetify returned profile data in an unexpected format."); }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { throw new InvalidOperationException("Leetify took too long to respond. Please try again."); }
    }
}
