using System.Security.Cryptography;
using System.Text;
using Mapster;
using Microsoft.Extensions.Caching.Memory;
using PersonalTools.Data.PasteBin;
using PersonalTools.Entities.PasteBin;

namespace PersonalTools.Classes.PasteBin;

public interface IPasteBinFuncs
{
    Task<PasteBinSettingsDbModel> GetPasteBinSettings(CancellationToken cancellationToken = default);
    Task UpdatePasteBinSettings(int maximumUploadSizeMb, CancellationToken cancellationToken = default);
    Task<List<PasteBinPasteObj>> GetPasteBinPastes(Guid userId, CancellationToken cancellationToken = default);
    Task<PasteBinPasteObj> GetPasteByShortCode(Guid userId, string shortCode, CancellationToken cancellationToken = default);
    Task<PasteBinCreateResult> CreatePaste(Guid userId, string displayName, PasteBinCreateRequest request, IFormFile? attachment, CancellationToken cancellationToken = default);
    Task UnlockPaste(Guid userId, string shortCode, string password, CancellationToken cancellationToken = default);
    Task DeletePaste(Guid userId, Guid pasteId, CancellationToken cancellationToken = default);
    Task<(PasteBinPasteDbModel Paste, FileStream Stream)> OpenPasteFile(Guid userId, string shortCode, CancellationToken cancellationToken = default);
}

public sealed class PasteBinFuncs : IPasteBinFuncs
{
    public const int AbsoluteMaximumUploadSizeMb = 50;
    private const string ShortCodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
    private static readonly HashSet<string> Languages = new(StringComparer.OrdinalIgnoreCase)
    {
        "text", "json", "sql", "csharp", "javascript", "html", "css", "xml", "bash", "powershell"
    };
    private static readonly HashSet<string> InlineExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".mp3", ".wav", ".ogg", ".m4a", ".aac", ".flac", ".mp4", ".webm"
    };
    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg", [".png"] = "image/png", [".gif"] = "image/gif", [".webp"] = "image/webp",
        [".mp3"] = "audio/mpeg", [".wav"] = "audio/wav", [".ogg"] = "audio/ogg", [".m4a"] = "audio/mp4", [".aac"] = "audio/aac", [".flac"] = "audio/flac",
        [".mp4"] = "video/mp4", [".webm"] = "video/webm", [".pdf"] = "application/pdf", [".txt"] = "text/plain", [".log"] = "text/plain",
        [".csv"] = "text/csv", [".json"] = "application/json", [".xml"] = "application/xml", [".zip"] = "application/zip", [".gz"] = "application/gzip"
    };

    private readonly IPasteBinData _data;
    private readonly IPasteBinFileStorage _storage;
    private readonly IMemoryCache _cache;
    private readonly ILogger<PasteBinFuncs> _logger;

    public PasteBinFuncs(IPasteBinData data, IPasteBinFileStorage storage, IMemoryCache cache, ILogger<PasteBinFuncs> logger)
    {
        _data = data;
        _storage = storage;
        _cache = cache;
        _logger = logger;
    }

    public Task<PasteBinSettingsDbModel> GetPasteBinSettings(CancellationToken cancellationToken = default) => _data.GetPasteBinSettings(cancellationToken);

    public Task UpdatePasteBinSettings(int maximumUploadSizeMb, CancellationToken cancellationToken = default)
    {
        if (maximumUploadSizeMb is < 1 or > AbsoluteMaximumUploadSizeMb)
            throw new PasteBinAccessException("Enter a Paste Bin upload limit between 1 and 50 MB.");
        return _data.UpdatePasteBinSettings(maximumUploadSizeMb, cancellationToken);
    }

    public async Task<List<PasteBinPasteObj>> GetPasteBinPastes(Guid userId, CancellationToken cancellationToken = default) =>
        (await _data.GetPasteBinPastes(cancellationToken)).Select(paste => ToPublicPaste(paste, userId, includeContent: false)).ToList();

    public async Task<PasteBinPasteObj> GetPasteByShortCode(Guid userId, string shortCode, CancellationToken cancellationToken = default)
    {
        PasteBinPasteDbModel paste = await FindActivePaste(shortCode, cancellationToken);
        bool unlocked = CanReadProtectedPaste(userId, paste);
        return ToPublicPaste(paste, userId, includeContent: unlocked);
    }

    public async Task<PasteBinCreateResult> CreatePaste(Guid userId, string displayName, PasteBinCreateRequest request, IFormFile? attachment, CancellationToken cancellationToken = default)
    {
        string content = request.Content ?? string.Empty;
        if (string.IsNullOrWhiteSpace(content) && attachment is null)
            throw new PasteBinAccessException("Enter some paste content or choose a file to upload.");
        if (content.Length > 1_000_000)
            throw new PasteBinAccessException("Paste content must be no more than 1,000,000 characters.");

        string title = request.Title?.Trim() ?? string.Empty;
        if (title.Length is < 1 or > 200)
            throw new PasteBinAccessException("Enter a paste title up to 200 characters.");
        string language = request.Language?.Trim().ToLowerInvariant() ?? "text";
        if (!Languages.Contains(language))
            throw new PasteBinAccessException("Choose a supported paste language.");
        if (!string.IsNullOrEmpty(request.Password) && request.Password.Length is < 8 or > 128)
            throw new PasteBinAccessException("Use a paste password between 8 and 128 characters.");

        PasteBinSettingsDbModel settings = await _data.GetPasteBinSettings(cancellationToken);
        long maximumBytes = Math.Min(settings.MaximumUploadSizeMb, AbsoluteMaximumUploadSizeMb) * 1024L * 1024L;
        PasteBinFileDbModel? file = null;

        if (attachment is not null)
        {
            if (attachment.Length == 0)
                throw new PasteBinAccessException("The selected file is empty.");
            if (attachment.Length > maximumBytes)
                throw new PasteBinAccessException($"The selected file exceeds the current {settings.MaximumUploadSizeMb} MB Paste Bin upload limit.");

            string originalName = SanitiseFileName(attachment.FileName);
            string extension = Path.GetExtension(originalName).ToLowerInvariant();
            if (extension.Length > 20)
                extension = string.Empty;
            await using Stream upload = attachment.OpenReadStream();
            (string storedFileName, long fileSizeBytes) = await _storage.StoreFile(upload, maximumBytes, cancellationToken);
            file = new PasteBinFileDbModel
            {
                PasteFileId = Guid.NewGuid(),
                OriginalFileName = originalName,
                StoredFileName = storedFileName,
                FileExtension = extension,
                ContentType = ContentTypes.GetValueOrDefault(extension, "application/octet-stream"),
                FileSizeBytes = fileSizeBytes
            };
        }

        PasteBinPasteDbModel paste = new()
        {
            PasteId = Guid.NewGuid(),
            CreatedByUserId = userId,
            CreatedByDisplayName = displayName,
            ShortCode = await GenerateShortCode(cancellationToken),
            Title = title,
            Language = language,
            Content = string.IsNullOrEmpty(content) ? null : content,
            PasswordHash = string.IsNullOrEmpty(request.Password) ? null : HashPassword(request.Password),
            ExpiresUtc = ExpiryUtc(request.Expiry),
            File = file
        };
        if (file is not null)
            file.PasteId = paste.PasteId;

        try
        {
            await _data.CreatePaste(paste, cancellationToken);
        }
        catch
        {
            // The database owns the metadata. A failed insert must not leave an unreachable file.
            if (file is not null)
            {
                try { await _storage.DeleteFile(file.StoredFileName); }
                catch (Exception cleanupException)
                {
                    _logger.LogError(cleanupException, "Failed to remove attachment {PasteFileId} after paste {PasteId} could not be created.", file.PasteFileId, paste.PasteId);
                }
            }
            throw;
        }

        RememberUnlock(userId, paste.PasteId);
        return new PasteBinCreateResult(paste.PasteId, paste.ShortCode);
    }

    public async Task UnlockPaste(Guid userId, string shortCode, string password, CancellationToken cancellationToken = default)
    {
        PasteBinPasteDbModel paste = await FindActivePaste(shortCode, cancellationToken);
        if (string.IsNullOrEmpty(paste.PasswordHash))
        {
            RememberUnlock(userId, paste.PasteId);
            return;
        }

        string throttleKey = ThrottleKey(userId, paste.PasteId);
        FailedAttempts attempts = _cache.Get<FailedAttempts>(throttleKey) ?? new();
        if (attempts.LockedUntilUtc > DateTime.UtcNow)
            throw new PasteBinAccessException("Too many incorrect password attempts. Try again in a few minutes.", StatusCodes.Status429TooManyRequests);

        if (!VerifyPassword(password ?? string.Empty, paste.PasswordHash))
        {
            attempts.Count++;
            if (attempts.Count >= 5)
                attempts.LockedUntilUtc = DateTime.UtcNow.AddMinutes(5);
            _cache.Set(throttleKey, attempts, TimeSpan.FromMinutes(6));
            throw new PasteBinAccessException(attempts.Count >= 5
                ? "Too many incorrect password attempts. Try again in a few minutes."
                : "The password for this paste is incorrect.", attempts.Count >= 5 ? StatusCodes.Status429TooManyRequests : StatusCodes.Status401Unauthorized);
        }

        _cache.Remove(throttleKey);
        RememberUnlock(userId, paste.PasteId);
    }

    public async Task DeletePaste(Guid userId, Guid pasteId, CancellationToken cancellationToken = default)
    {
        PasteBinDeleteResult? result = await _data.DeletePaste(pasteId, userId, cancellationToken);
        if (result is null)
            throw new PasteBinAccessException("The paste could not be deleted because it does not exist or was created by another user.", StatusCodes.Status404NotFound);

        try
        {
            await _storage.DeleteFile(result.StoredFileName);
        }
        catch (Exception exception)
        {
            // DB deletion has already succeeded. Hourly orphan cleanup will retry the physical file.
            _logger.LogWarning(exception, "Paste {PasteId} was deleted by user {UserId}, but attachment cleanup will need retrying.", pasteId, userId);
        }
    }

    public async Task<(PasteBinPasteDbModel Paste, FileStream Stream)> OpenPasteFile(Guid userId, string shortCode, CancellationToken cancellationToken = default)
    {
        PasteBinPasteDbModel paste = await FindActivePaste(shortCode, cancellationToken);
        if (!CanReadProtectedPaste(userId, paste))
            throw new PasteBinAccessException("Unlock this paste before opening its attachment.", StatusCodes.Status403Forbidden);
        if (paste.File is null)
            throw new PasteBinAccessException("This paste does not have an attachment.", StatusCodes.Status404NotFound);

        try
        {
            return (paste, _storage.OpenRead(paste.File.StoredFileName));
        }
        catch (FileNotFoundException)
        {
            throw new PasteBinAccessException("The stored attachment could not be found.", StatusCodes.Status404NotFound);
        }
    }

    private async Task<PasteBinPasteDbModel> FindActivePaste(string shortCode, CancellationToken cancellationToken)
    {
        string clean = shortCode?.Trim() ?? string.Empty;
        if (clean.Length is < 6 or > 16 || clean.Any(character => !ShortCodeAlphabet.Contains(character)))
            throw new PasteBinAccessException("The paste could not be found. It may have expired or been deleted.", StatusCodes.Status404NotFound);
        return await _data.GetPasteByShortCode(clean, cancellationToken) ??
            throw new PasteBinAccessException("The paste could not be found. It may have expired or been deleted.", StatusCodes.Status404NotFound);
    }

    private PasteBinPasteObj ToPublicPaste(PasteBinPasteDbModel paste, Guid userId, bool includeContent)
    {
        PasteBinPasteObj result = paste.Adapt<PasteBinPasteObj>();
        result.IsProtected = !string.IsNullOrEmpty(paste.PasswordHash);
        result.IsUnlocked = CanReadProtectedPaste(userId, paste);
        result.CanDelete = paste.CreatedByUserId == userId;
        result.Content = includeContent ? paste.Content : null;
        if (result.File is not null)
            result.File.CanPreviewInline = InlineExtensions.Contains(result.File.FileExtension);
        return result;
    }

    private bool CanReadProtectedPaste(Guid userId, PasteBinPasteDbModel paste) =>
        string.IsNullOrEmpty(paste.PasswordHash) || _cache.TryGetValue(UnlockKey(userId, paste.PasteId), out _);

    private void RememberUnlock(Guid userId, Guid pasteId) => _cache.Set(UnlockKey(userId, pasteId), true, TimeSpan.FromMinutes(20));
    private static string UnlockKey(Guid userId, Guid pasteId) => $"paste-bin-unlock:{userId:D}:{pasteId:D}";
    private static string ThrottleKey(Guid userId, Guid pasteId) => $"paste-bin-attempts:{userId:D}:{pasteId:D}";

    private async Task<string> GenerateShortCode(CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            string code = new(Enumerable.Range(0, 8)
                .Select(_ => ShortCodeAlphabet[RandomNumberGenerator.GetInt32(ShortCodeAlphabet.Length)])
                .ToArray());
            if (!await _data.ShortCodeExists(code, cancellationToken))
                return code;
        }
        throw new InvalidOperationException("A unique Paste Bin short code could not be generated.");
    }

    private static DateTime? ExpiryUtc(string expiry) => expiry?.Trim().ToLowerInvariant() switch
    {
        "hour" => DateTime.UtcNow.AddHours(1),
        "day" => DateTime.UtcNow.AddDays(1),
        "week" => DateTime.UtcNow.AddDays(7),
        "never" => null,
        _ => throw new PasteBinAccessException("Choose a valid paste expiry.")
    };

    private static string SanitiseFileName(string fileName)
    {
        string normalisedName = new string((fileName ?? string.Empty).Where(character => !char.IsControl(character)).ToArray()).Replace('\\', '/');
        string clean = Path.GetFileName(normalisedName);
        clean = clean.Trim();
        if (string.IsNullOrWhiteSpace(clean))
            clean = "attachment";
        return clean.Length > 255 ? clean[..255] : clean;
    }

    private static string HashPassword(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 600_000, HashAlgorithmName.SHA512, 32);
        return $"PBKDF2-SHA512$600000${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    private static bool VerifyPassword(string password, string stored)
    {
        try
        {
            string[] parts = stored.Split('$');
            if (parts.Length != 4 || parts[0] != "PBKDF2-SHA512" || !int.TryParse(parts[1], out int iterations) || iterations < 1)
                return false;
            byte[] expected = Convert.FromBase64String(parts[3]);
            byte[] actual = Rfc2898DeriveBytes.Pbkdf2(password, Convert.FromBase64String(parts[2]), iterations, HashAlgorithmName.SHA512, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException) { return false; }
        catch (CryptographicException) { return false; }
    }

    private sealed class FailedAttempts
    {
        public int Count { get; set; }
        public DateTime LockedUntilUtc { get; set; }
    }
}
