using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using PersonalTools.Data;

namespace PersonalTools.Classes;

public interface ISteamOpenIdFuncs
{
    string CreateState();
    Uri CreateSignInUri(Uri requestBaseUri, string state);
    Task<bool> CompleteLink(Guid userId, string suppliedState, string savedState, IReadOnlyDictionary<string, string> parameters, CancellationToken cancellationToken = default);
}

/// <summary>
/// Coordinates a Steam OpenID linking transaction without exposing provider verification details
/// to the browser. The state comparison prevents a forged callback from linking the wrong account.
/// </summary>
public sealed class SteamOpenIdFuncs(ISteamOpenIdData steamOpenIdData, IAuthFuncs auth) : ISteamOpenIdFuncs
{
    private static readonly Regex SteamIdentityPattern = new(
        @"^https://steamcommunity\.com/openid/id/(\d{17})$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly ISteamOpenIdData _steamOpenIdData = steamOpenIdData;
    private readonly IAuthFuncs _auth = auth;

    /// <summary>
    /// Generates a high-entropy, opaque value for the short-lived browser-only callback cookie.
    /// It is never persisted in MariaDB because it exists solely to bind this redirect to this browser.
    /// </summary>
    public string CreateState() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    public Uri CreateSignInUri(Uri requestBaseUri, string state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(state);

        string callback = new Uri(requestBaseUri, "auth/steam/callback").ToString();
        Dictionary<string, string> values = new()
        {
            ["openid.ns"] = "http://specs.openid.net/auth/2.0",
            ["openid.mode"] = "checkid_setup",
            ["openid.return_to"] = callback + "?state=" + Uri.EscapeDataString(state),
            ["openid.realm"] = requestBaseUri.ToString(),
            ["openid.identity"] = "http://specs.openid.net/auth/2.0/identifier_select",
            ["openid.claimed_id"] = "http://specs.openid.net/auth/2.0/identifier_select",
        };

        string query = string.Join("&", values.Select(pair =>
            Uri.EscapeDataString(pair.Key) + "=" + Uri.EscapeDataString(pair.Value)));

        return new Uri("https://steamcommunity.com/openid/login?" + query);
    }

    public async Task<bool> CompleteLink(
        Guid userId,
        string suppliedState,
        string savedState,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || !StateMatches(suppliedState, savedState))
        {
            return false;
        }

        if (!parameters.TryGetValue("openid.identity", out string? identity))
        {
            return false;
        }

        Match identityMatch = SteamIdentityPattern.Match(identity);
        if (!identityMatch.Success || !await _steamOpenIdData.VerifyAuthentication(parameters, cancellationToken))
        {
            return false;
        }

        await _auth.LinkSteam(userId, identityMatch.Groups[1].Value);
        return true;
    }

    private static bool StateMatches(string suppliedState, string savedState)
    {
        if (string.IsNullOrWhiteSpace(suppliedState) || string.IsNullOrWhiteSpace(savedState))
        {
            return false;
        }

        // Constant-time comparison avoids making the correct state easier to infer from timing.
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(suppliedState),
            Encoding.UTF8.GetBytes(savedState));
    }
}
