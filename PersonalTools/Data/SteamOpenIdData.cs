using System.Net.Http.Headers;

namespace PersonalTools.Data;

public interface ISteamOpenIdData
{
    Task<bool> VerifyAuthentication(IReadOnlyDictionary<string, string> parameters, CancellationToken cancellationToken = default);
}

/// <summary>
/// The external-provider boundary for Steam OpenID verification.
/// No user/session state is stored here: this class only posts Steam's signed fields back to
/// Steam and returns whether the provider verified them.
/// </summary>
public sealed class SteamOpenIdData(HttpClient client) : ISteamOpenIdData
{
    private readonly HttpClient _client = client;

    public async Task<bool> VerifyAuthentication(IReadOnlyDictionary<string, string> parameters, CancellationToken cancellationToken = default)
    {
        using FormUrlEncodedContent content = new(parameters);
        using HttpResponseMessage response = await _client.PostAsync("openid/login", content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        return responseBody.Contains("is_valid:true", StringComparison.Ordinal);
    }
}
