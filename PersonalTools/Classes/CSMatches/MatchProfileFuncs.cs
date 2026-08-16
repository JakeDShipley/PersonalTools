using System.Text.RegularExpressions;
using PersonalTools.Classes;
using PersonalTools.Data.CSMatches;
using PersonalTools.Entities.CSMatches;
using Mapster;

namespace PersonalTools.Classes.CSMatches
{
    public interface IMatchProfileFuncs
    {
        Task<List<MatchProfileObj>> GetProfiles(Guid userId);
        Task<MatchProfileObj?> GetProfile(Guid userId, Guid? profileId);
        Task<Guid> CreateProfile(Guid userId, string name, string steamId);
        Task UpdateProfile(Guid userId, Guid profileId, string name, string steamId);
        Task DeleteProfile(Guid userId, Guid profileId);
    }

    public class MatchProfileFuncs : IMatchProfileFuncs
    {
        private static readonly Regex SteamIdPattern = new("^7656\\d{13}$", RegexOptions.Compiled);

        private readonly IMatchProfilesData _data;
        private readonly ISteamInventoryFuncs _steamLookup;
        public MatchProfileFuncs(IMatchProfilesData data, ISteamInventoryFuncs steamLookup)
        {
            _data = data;
            _steamLookup = steamLookup;
        }

        /// <summary>
        /// Converts persisted profiles after data access so the public model is independent from
        /// stored-procedure result columns.
        /// </summary>
        public async Task<List<MatchProfileObj>> GetProfiles(Guid userId) =>
            (await _data.GetProfiles(userId)).Adapt<List<MatchProfileObj>>();

        public async Task<MatchProfileObj?> GetProfile(Guid userId, Guid? profileId) =>
            profileId is null ? null : (await _data.GetProfile(userId, profileId.Value))?.Adapt<MatchProfileObj>();

        public async Task<Guid> CreateProfile(Guid userId, string name, string steamId)
        {
            (string trimmedName, string trimmedSteamId) = Validate(name, steamId);
            Guid profileId = Guid.NewGuid();
            string? avatarUrl = await TryResolveAvatar(trimmedSteamId);
            await _data.CreateProfile(userId, profileId, trimmedName, trimmedSteamId, avatarUrl);
            return profileId;
        }

        public async Task UpdateProfile(Guid userId, Guid profileId, string name, string steamId)
        {
            (string trimmedName, string trimmedSteamId) = Validate(name, steamId);
            string? avatarUrl = await TryResolveAvatar(trimmedSteamId);
            await _data.UpdateProfile(userId, profileId, trimmedName, trimmedSteamId, avatarUrl);
        }

        // The profile's SteamId is already format-validated, but the Steam lookup itself can fail
        // (private profile, Steam unreachable) - that's never a reason to block saving the profile,
        // so the avatar is just left null and the tab falls back to showing initials.
        private async Task<string?> TryResolveAvatar(string steamId)
        {
            try
            {
                return (await _steamLookup.LookupProfile(steamId)).AvatarUrl;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        public Task DeleteProfile(Guid userId, Guid profileId) => _data.DeleteProfile(userId, profileId);

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
