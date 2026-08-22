using MySqlConnector;
using PersonalTools.Entities.PasteBin;

namespace PersonalTools.Data.PasteBin;

public interface IPasteBinData
{
    Task<PasteBinSettingsDbModel> GetPasteBinSettings(CancellationToken cancellationToken = default);
    Task UpdatePasteBinSettings(int maximumUploadSizeMb, CancellationToken cancellationToken = default);
    Task<List<PasteBinPasteDbModel>> GetPasteBinPastes(CancellationToken cancellationToken = default);
    Task<PasteBinPasteDbModel?> GetPasteByShortCode(string shortCode, CancellationToken cancellationToken = default);
    Task<bool> ShortCodeExists(string shortCode, CancellationToken cancellationToken = default);
    Task CreatePaste(PasteBinPasteDbModel paste, CancellationToken cancellationToken = default);
    Task<PasteBinDeleteResult?> DeletePaste(Guid pasteId, Guid userId, CancellationToken cancellationToken = default);
    Task<List<PasteBinExpiredFile>> GetExpiredPastes(CancellationToken cancellationToken = default);
    Task DeleteExpiredPastes(CancellationToken cancellationToken = default);
    Task<HashSet<string>> GetStoredFileNames(CancellationToken cancellationToken = default);
}

public sealed class PasteBinData : IPasteBinData
{
    private readonly IMariaDbDataAccess _database;
    public PasteBinData(IMariaDbDataAccess database) => _database = database;

    public async Task<PasteBinSettingsDbModel> GetPasteBinSettings(CancellationToken cancellationToken = default) =>
        await _database.GetDataSP("sp_paste_bin_settings_get", ReadSettings, cancellationToken: cancellationToken) ?? new();

    public Task UpdatePasteBinSettings(int maximumUploadSizeMb, CancellationToken cancellationToken = default) =>
        _database.ExecuteSP("sp_paste_bin_settings_update", Parameters(("p_maximum_upload_size_mb", maximumUploadSizeMb)), cancellationToken);

    public Task<List<PasteBinPasteDbModel>> GetPasteBinPastes(CancellationToken cancellationToken = default) =>
        _database.GetBulkDataSP("sp_paste_bin_pastes_get", ReadPaste, cancellationToken: cancellationToken);

    public Task<PasteBinPasteDbModel?> GetPasteByShortCode(string shortCode, CancellationToken cancellationToken = default) =>
        _database.GetDataSP("sp_paste_bin_paste_get_by_short_code", ReadPaste, Parameters(("p_short_code", shortCode)), cancellationToken);

    public async Task<bool> ShortCodeExists(string shortCode, CancellationToken cancellationToken = default) =>
        await _database.GetScalarSP<int>("sp_paste_bin_short_code_exists", Parameters(("p_short_code", shortCode)), cancellationToken) == 1;

    public Task CreatePaste(PasteBinPasteDbModel paste, CancellationToken cancellationToken = default) =>
        _database.ExecuteSP("sp_paste_bin_paste_create", Parameters(
            ("p_paste_id", paste.PasteId),
            ("p_created_by_user_id", paste.CreatedByUserId),
            ("p_short_code", paste.ShortCode),
            ("p_title", paste.Title),
            ("p_language", paste.Language),
            ("p_content", paste.Content ?? (object)DBNull.Value),
            ("p_password_hash", paste.PasswordHash ?? (object)DBNull.Value),
            ("p_expires_utc", paste.ExpiresUtc ?? (object)DBNull.Value),
            ("p_paste_file_id", paste.File?.PasteFileId ?? (object)DBNull.Value),
            ("p_original_file_name", paste.File?.OriginalFileName ?? (object)DBNull.Value),
            ("p_stored_file_name", paste.File?.StoredFileName ?? (object)DBNull.Value),
            ("p_content_type", paste.File?.ContentType ?? (object)DBNull.Value),
            ("p_file_extension", paste.File?.FileExtension ?? (object)DBNull.Value),
            ("p_file_size_bytes", paste.File?.FileSizeBytes ?? (object)DBNull.Value)), cancellationToken);

    public Task<PasteBinDeleteResult?> DeletePaste(Guid pasteId, Guid userId, CancellationToken cancellationToken = default) =>
        _database.GetDataSP("sp_paste_bin_paste_delete", reader => new PasteBinDeleteResult(IsNull(reader, "StoredFileName") ? null : reader.GetString("StoredFileName")),
            Parameters(("p_paste_id", pasteId), ("p_user_id", userId)), cancellationToken);

    public Task<List<PasteBinExpiredFile>> GetExpiredPastes(CancellationToken cancellationToken = default) =>
        _database.GetBulkDataSP("sp_paste_bin_expired_pastes_get", reader => new PasteBinExpiredFile(
            reader.GetGuid("PasteId"), IsNull(reader, "StoredFileName") ? string.Empty : reader.GetString("StoredFileName")), cancellationToken: cancellationToken);

    public Task DeleteExpiredPastes(CancellationToken cancellationToken = default) =>
        _database.ExecuteSP("sp_paste_bin_expired_pastes_delete", cancellationToken: cancellationToken);

    public async Task<HashSet<string>> GetStoredFileNames(CancellationToken cancellationToken = default) =>
        (await _database.GetBulkDataSP("sp_paste_bin_stored_file_names_get", reader => reader.GetString("StoredFileName"), cancellationToken: cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static PasteBinSettingsDbModel ReadSettings(MySqlDataReader reader) => new()
    {
        MaximumUploadSizeMb = reader.GetInt32("MaximumUploadSizeMb"),
        UpdatedUtc = Utc(reader.GetDateTime("UpdatedUtc"))
    };

    private static PasteBinPasteDbModel ReadPaste(MySqlDataReader reader)
    {
        PasteBinPasteDbModel paste = new()
        {
            PasteId = reader.GetGuid("PasteId"),
            CreatedByUserId = reader.GetGuid("CreatedByUserId"),
            CreatedByDisplayName = reader.GetString("CreatedByDisplayName"),
            ShortCode = reader.GetString("ShortCode"),
            Title = reader.GetString("Title"),
            Language = reader.GetString("Language"),
            Content = IsNull(reader, "Content") ? null : reader.GetString("Content"),
            PasswordHash = IsNull(reader, "PasswordHash") ? null : reader.GetString("PasswordHash"),
            CreatedUtc = Utc(reader.GetDateTime("CreatedUtc")),
            ExpiresUtc = IsNull(reader, "ExpiresUtc") ? null : Utc(reader.GetDateTime("ExpiresUtc"))
        };

        if (!IsNull(reader, "PasteFileId"))
        {
            paste.File = new PasteBinFileDbModel
            {
                PasteFileId = reader.GetGuid("PasteFileId"),
                PasteId = paste.PasteId,
                OriginalFileName = reader.GetString("OriginalFileName"),
                StoredFileName = reader.GetString("StoredFileName"),
                ContentType = reader.GetString("FileContentType"),
                FileExtension = reader.GetString("FileExtension"),
                FileSizeBytes = reader.GetInt64("FileSizeBytes"),
                CreatedUtc = Utc(reader.GetDateTime("FileCreatedUtc"))
            };
        }
        return paste;
    }

    private static MySqlParameter[] Parameters(params (string Name, object Value)[] values) =>
        values.Select(value => new MySqlParameter(value.Name, value.Value is Guid id ? id.ToString("D") : value.Value)).ToArray();

    private static bool IsNull(MySqlDataReader reader, string columnName) => reader.IsDBNull(reader.GetOrdinal(columnName));
    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
