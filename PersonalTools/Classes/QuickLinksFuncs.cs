using PersonalTools.Data;
using PersonalTools.Entities;

namespace PersonalTools.Classes;

public interface IQuickLinksFuncs
{
    Task<List<QuickLink>> GetQuickLinks(long userId);
    Task<long> CreateQuickLink(long userId, string title, string url, string? iconClass);
    Task UpdateQuickLink(long userId, long quickLinkId, string title, string url, string? iconClass);
    Task DeleteQuickLink(long userId, long quickLinkId);
}

public sealed class QuickLinksFuncs : IQuickLinksFuncs
{
    private readonly IQuickLinksData _data;
    public QuickLinksFuncs(IQuickLinksData data) => _data = data;
    public Task<List<QuickLink>> GetQuickLinks(long userId) => _data.GetQuickLinks(userId);
    public Task<long> CreateQuickLink(long userId, string title, string url, string? iconClass) { Validate(title, url); return _data.CreateQuickLink(userId, title.Trim(), NormaliseUrl(url), iconClass?.Trim()); }
    public Task UpdateQuickLink(long userId, long quickLinkId, string title, string url, string? iconClass) { Validate(title, url); return _data.UpdateQuickLink(userId, quickLinkId, title.Trim(), NormaliseUrl(url), iconClass?.Trim()); }
    public Task DeleteQuickLink(long userId, long quickLinkId) => _data.DeleteQuickLink(userId, quickLinkId);
    private static void Validate(string title, string url) { if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 100) throw new InvalidOperationException("Enter a link title up to 100 characters."); if (!Uri.TryCreate(NormaliseUrl(url), UriKind.Absolute, out Uri? uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)) throw new InvalidOperationException("Enter a valid http or https URL."); }
    private static string NormaliseUrl(string url) => url.Trim().StartsWith("http", StringComparison.OrdinalIgnoreCase) ? url.Trim() : "https://" + url.Trim();
}
