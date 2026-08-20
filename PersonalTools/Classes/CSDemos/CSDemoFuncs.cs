using Mapster;
using PersonalTools.Data;
using PersonalTools.Data.CSDemos;
using PersonalTools.Data.CSMatches;
using PersonalTools.Entities;
using PersonalTools.Entities.CSDemos;
using PersonalTools.Entities.CSMatches;

namespace PersonalTools.Classes.CSDemos;

public interface ICSDemoFuncs
{
    Task<CSDemoLibraryObj> GetRecentDemos(Guid userId, string profileReference, CancellationToken cancellationToken = default);
    Task<CSDemoLibraryObj> RefreshRecentDemos(Guid userId, string profileReference, CancellationToken cancellationToken = default);
}

public sealed class CSDemoFuncs : ICSDemoFuncs
{
    private static readonly Dictionary<string, string> GameTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["matchmaking"] = "Premier",
        ["matchmaking_competitive"] = "Competitive",
        ["matchmaking_wingman"] = "Wingman",
        ["faceit"] = "FACEIT"
    };

    private readonly ISteamInventoryData _steamData;
    private readonly ILeetifyData _leetifyData;
    private readonly ICSDemoData _demoData;

    public CSDemoFuncs(
        ISteamInventoryData steamData,
        ILeetifyData leetifyData,
        ICSDemoData demoData)
    {
        _steamData = steamData;
        _leetifyData = leetifyData;
        _demoData = demoData;
    }

    public async Task<CSDemoLibraryObj> GetRecentDemos(
        Guid userId,
        string profileReference,
        CancellationToken cancellationToken = default)
    {
        string steam64Id = await ResolveSteamId(profileReference, cancellationToken);
        Task<SteamPublicProfile?> steamProfileTask = _steamData.GetPublicProfile(steam64Id, cancellationToken);
        List<CSDemoDbModel> cached = await _demoData.GetDemos(userId, steam64Id, cancellationToken);
        SteamPublicProfile? steamProfile = await steamProfileTask;

        // The first visit needs a provider fetch. Later visits should be instant and let the
        // user explicitly choose when a short-lived external replay URL is refreshed.
        if (cached.Count == 0)
        {
            return await RefreshRecentDemos(userId, steam64Id, cancellationToken, steamProfile);
        }

        return BuildLibrary(steam64Id, steamProfile, cached, false);
    }

    public Task<CSDemoLibraryObj> RefreshRecentDemos(
        Guid userId,
        string profileReference,
        CancellationToken cancellationToken = default)
    {
        return RefreshRecentDemos(userId, profileReference, cancellationToken, null);
    }

    private async Task<CSDemoLibraryObj> RefreshRecentDemos(
        Guid userId,
        string profileReference,
        CancellationToken cancellationToken,
        SteamPublicProfile? knownProfile)
    {
        string steam64Id = await ResolveSteamId(profileReference, cancellationToken);
        Task<SteamPublicProfile?>? profileTask = knownProfile is null
            ? _steamData.GetPublicProfile(steam64Id, cancellationToken)
            : null;

        List<LeetifyMatchModel> matches = await _leetifyData.GetMatches(steam64Id);
        List<CSDemoDbModel> catalogue = matches
            .Select(match => ToDbModel(match, steam64Id))
            .OrderByDescending(demo => demo.PlayedAtUtc)
            .ToList();

        await _demoData.SyncDemoCatalogue(userId, steam64Id, catalogue, cancellationToken);

        SteamPublicProfile? steamProfile = knownProfile ?? await profileTask!;
        List<CSDemoDbModel> stored = await _demoData.GetDemos(userId, steam64Id, cancellationToken);
        return BuildLibrary(steam64Id, steamProfile, stored, true);
    }

    private async Task<string> ResolveSteamId(string profileReference, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(profileReference))
        {
            throw new InvalidOperationException("Enter a Steam profile URL, custom name, or SteamID64.");
        }

        return await _steamData.ResolveSteamId(profileReference.Trim(), cancellationToken);
    }

    private static CSDemoLibraryObj BuildLibrary(
        string steam64Id,
        SteamPublicProfile? steamProfile,
        List<CSDemoDbModel> stored,
        bool wasRefreshed)
    {
        // Match history remains useful even after a provider link has expired. Return every
        // cached match and let the UI disable download actions only for unavailable replays.
        // This also avoids making a healthy Leetify response look like an empty library.
        List<CSDemoObj> recentDemos = stored
            .OrderByDescending(demo => demo.PlayedAtUtc)
            .Adapt<List<CSDemoObj>>();

        foreach (CSDemoObj demo in recentDemos)
        {
            demo.IsAvailable = demo.IsAvailable && IsSafeReplayUrl(demo.ReplayUrl);

            if (!demo.IsAvailable)
            {
                demo.ReplayUrl = string.Empty;
            }
        }

        return new CSDemoLibraryObj
        {
            Steam64Id = steam64Id,
            PlayerName = string.IsNullOrWhiteSpace(steamProfile?.DisplayName) ? "CS2 player" : steamProfile.DisplayName,
            AvatarUrl = steamProfile?.AvatarUrl ?? string.Empty,
            RecentMatchCount = stored.Count,
            AvailableDemoCount = recentDemos.Count(demo => demo.IsAvailable),
            LastRefreshedUtc = stored.Count == 0 ? null : stored.Max(demo => demo.RefreshedUtc),
            WasRefreshed = wasRefreshed,
            Demos = recentDemos
        };
    }

    private static CSDemoDbModel ToDbModel(LeetifyMatchModel match, string steam64Id)
    {
        LeetifyPlayerStatModel? player = match.Stats.FirstOrDefault(stat => stat.Steam64Id == steam64Id)
            ?? match.Stats.FirstOrDefault();
        int playerTeam = player?.InitialTeamNumber ?? 0;
        int teamScore = match.TeamScores.FirstOrDefault(score => score.TeamNumber == playerTeam)?.Score ?? 0;
        int opponentScore = match.TeamScores.FirstOrDefault(score => score.TeamNumber != playerTeam)?.Score ?? 0;
        bool available = IsSafeReplayUrl(match.ReplayUrl);

        return new CSDemoDbModel
        {
            DemoId = Guid.NewGuid(),
            Steam64Id = steam64Id,
            LeetifyMatchId = match.Id,
            MapName = FormatMapName(match.MapName),
            GameType = GameTypes.TryGetValue(match.DataSource, out string? gameType) ? gameType : FormatGameType(match.DataSource),
            TeamScore = teamScore,
            OpponentScore = opponentScore,
            IsWin = teamScore > opponentScore,
            ReplayUrl = available ? match.ReplayUrl! : string.Empty,
            IsAvailable = available,
            PlayedAtUtc = match.FinishedAt
        };
    }

    private static bool IsSafeReplayUrl(string? value)
    {
        // A replay link may leave the app, so refuse insecure or non-web schemes before it can
        // be cached and rendered into an anchor element.
        return Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
            && uri.Scheme == Uri.UriSchemeHttps;
    }

    private static string FormatMapName(string mapName)
    {
        string clean = mapName
            .Replace("de_", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("cs_", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace('_', ' ');

        return string.IsNullOrWhiteSpace(clean)
            ? "Unknown map"
            : System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(clean);
    }

    private static string FormatGameType(string dataSource)
    {
        if (string.IsNullOrWhiteSpace(dataSource))
        {
            return "Matchmaking";
        }

        return string.Join(' ', dataSource
            .Split('_', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => char.ToUpperInvariant(word[0]) + word[1..]));
    }
}
