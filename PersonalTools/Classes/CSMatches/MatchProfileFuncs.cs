using System.Text.RegularExpressions;
using PersonalTools.Data.CSMatches;
using PersonalTools.Entities.CSMatches;

namespace PersonalTools.Classes.CSMatches
{
    public interface IMatchProfileFuncs
    {
        Task<List<MatchProfileObj>> GetProfiles(Guid userId);
        Task<MatchProfileObj?> GetProfile(Guid userId, string? profileId);
        Task<string> CreateProfile(Guid userId, string name, string steamId);
        Task UpdateProfile(Guid userId, string profileId, string name, string steamId);
        Task DeleteProfile(Guid userId, string profileId);
    }

    public class MatchProfileFuncs : IMatchProfileFuncs
    {
        private static readonly Regex SteamIdPattern = new("^7656\\d{13}$", RegexOptions.Compiled);

        private readonly IMatchProfilesData _data;
        public MatchProfileFuncs(IMatchProfilesData data) => _data = data;

        public Task<List<MatchProfileObj>> GetProfiles(Guid userId) => _data.GetProfiles(userId);

        public Task<MatchProfileObj?> GetProfile(Guid userId, string? profileId) =>
            string.IsNullOrWhiteSpace(profileId) ? Task.FromResult<MatchProfileObj?>(null) : _data.GetProfile(userId, profileId);

        public async Task<string> CreateProfile(Guid userId, string name, string steamId)
        {
            (string trimmedName, string trimmedSteamId) = Validate(name, steamId);
            string profileId = Guid.NewGuid().ToString();
            await _data.CreateProfile(userId, profileId, trimmedName, trimmedSteamId);
            return profileId;
        }

        public Task UpdateProfile(Guid userId, string profileId, string name, string steamId)
        {
            (string trimmedName, string trimmedSteamId) = Validate(name, steamId);
            return _data.UpdateProfile(userId, profileId, trimmedName, trimmedSteamId);
        }

        public Task DeleteProfile(Guid userId, string profileId) => _data.DeleteProfile(userId, profileId);

        private static (string Name, string SteamId) Validate(string name, string steamId)
        {
            string trimmedName = name.Trim();
            if (string.IsNullOrWhiteSpace(trimmedName) || trimmedName.Length > 100)
            {
                throw new InvalidOperationException("Enter a profile name up to 100 characters.");
            }

            string trimmedSteamId = steamId.Trim();
            if (!SteamIdPattern.IsMatch(trimmedSteamId))
            {
                throw new InvalidOperationException("Enter a valid 17-digit SteamID64.");
            }

            return (trimmedName, trimmedSteamId);
        }
    }
}
