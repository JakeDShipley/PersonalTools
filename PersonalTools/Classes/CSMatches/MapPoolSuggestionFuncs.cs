using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using PersonalTools.Data.CSMatches;

namespace PersonalTools.Classes.CSMatches
{
    public interface IMapPoolSuggestionFuncs
    {
        Task<List<string>?> GetPendingSuggestion();
    }

    public class MapPoolSuggestionFuncs : IMapPoolSuggestionFuncs
    {
        private const string CacheKey = "csmatches-active-duty-suggestion";

        private static readonly Dictionary<string, string> MapNameAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            ["dust ii"] = "Dust2",
            ["dust 2"] = "Dust2",
            ["dust2"] = "Dust2",
        };

        private readonly IMapPoolSuggestionData _data;
        private readonly ICSMatchReferenceData _referenceData;
        private readonly IMemoryCache _cache;

        public MapPoolSuggestionFuncs(IMapPoolSuggestionData data, ICSMatchReferenceData referenceData, IMemoryCache cache)
        {
            _data = data;
            _referenceData = referenceData;
            _cache = cache;
        }

        public async Task<List<string>?> GetPendingSuggestion()
        {
            if (_cache.TryGetValue(CacheKey, out List<string>? cached))
            {
                return cached;
            }

            List<string>? suggestion = await CheckForSuggestion();

            _cache.Set(CacheKey, suggestion, TimeSpan.FromHours(24));

            return suggestion;
        }

        private async Task<List<string>?> CheckForSuggestion()
        {
            string? extract = await _data.GetMapsArticleExtract();

            if (string.IsNullOrWhiteSpace(extract))
            {
                return null;
            }

            List<string>? parsed = ParseActiveDutyMaps(extract);

            if (parsed is null || parsed.Count == 0)
            {
                return null;
            }

            List<string> currentPool = await _referenceData.GetActiveDutyPool();
            HashSet<string> currentSet = new(currentPool, StringComparer.OrdinalIgnoreCase);

            return new HashSet<string>(parsed, StringComparer.OrdinalIgnoreCase).SetEquals(currentSet) ? null : parsed;
        }

        private static List<string>? ParseActiveDutyMaps(string articleText)
        {
            Match match = Regex.Match(articleText, @"Active Duty maps? in Counter-Strike 2 (?:is|are)\s+([^.\n]+)\.", RegexOptions.IgnoreCase);

            if (!match.Success)
            {
                return null;
            }

            string list = match.Groups[1].Value.Replace(" and ", ", ", StringComparison.OrdinalIgnoreCase);

            List<string> names = list
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(NormalizeMapName)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return names.Count > 0 ? names : null;
        }

        private static string NormalizeMapName(string rawName)
        {
            string trimmed = rawName.Trim().Trim('.');

            if (MapNameAliases.TryGetValue(trimmed, out string? alias))
            {
                return alias;
            }

            return trimmed.Length == 0 ? trimmed : char.ToUpperInvariant(trimmed[0]) + trimmed[1..];
        }
    }
}
