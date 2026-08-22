namespace PersonalTools.Entities.PasteBin;

public sealed class PasteBinSettingsDbModel
{
    public int MaximumUploadSizeMb { get; set; } = 50;
    public DateTime UpdatedUtc { get; set; }
}

public sealed class PasteBinFileDbModel
{
    public Guid PasteFileId { get; set; }
    public Guid PasteId { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public string FileExtension { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTime CreatedUtc { get; set; }
}

public sealed class PasteBinPasteDbModel
{
    public Guid PasteId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string CreatedByDisplayName { get; set; } = string.Empty;
    public string ShortCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Language { get; set; } = "text";
    public string? Content { get; set; }
    public string? PasswordHash { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? ExpiresUtc { get; set; }
    public PasteBinFileDbModel? File { get; set; }
}

public sealed class PasteBinFileObj
{
    public Guid PasteFileId { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public string FileExtension { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public bool CanPreviewInline { get; set; }
}

public sealed class PasteBinPasteObj
{
    public Guid PasteId { get; set; }
    public string ShortCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Language { get; set; } = "text";
    public string? Content { get; set; }
    public string CreatedByDisplayName { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
    public DateTime? ExpiresUtc { get; set; }
    public bool IsProtected { get; set; }
    public bool IsUnlocked { get; set; }
    public bool CanDelete { get; set; }
    public PasteBinFileObj? File { get; set; }
}

public sealed record PasteBinCreateResult(Guid PasteId, string ShortCode);
public sealed record PasteBinDeleteResult(string? StoredFileName);
public sealed record PasteBinExpiredFile(Guid PasteId, string StoredFileName);

public sealed class PasteBinCreateRequest
{
    public string Title { get; set; } = string.Empty;
    public string Language { get; set; } = "text";
    public string Content { get; set; } = string.Empty;
    public string Expiry { get; set; } = "day";
    public string Password { get; set; } = string.Empty;
}

public sealed class PasteBinAccessException : InvalidOperationException
{
    public PasteBinAccessException(string message, int statusCode = StatusCodes.Status400BadRequest) : base(message) => StatusCode = statusCode;
    public int StatusCode { get; }
}
