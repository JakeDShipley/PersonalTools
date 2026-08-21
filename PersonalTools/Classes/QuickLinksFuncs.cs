using PersonalTools.Data;
using PersonalTools.Entities;
using Mapster;

namespace PersonalTools.Classes;

public interface IQuickLinksFuncs
{
    Task<List<QuickLink>> GetQuickLinks(Guid userId);
    Task<Guid> CreateQuickLink(Guid userId, string title, string url, string? iconClass);
    Task UpdateQuickLink(Guid userId, Guid quickLinkId, string title, string url, string? iconClass);
    Task DeleteQuickLink(Guid userId, Guid quickLinkId);
    Task UpdateOrder(Guid userId, IReadOnlyList<Guid> quickLinkIds, CancellationToken cancellationToken = default);
}

public sealed class QuickLinksFuncs : IQuickLinksFuncs
{
    private readonly IQuickLinksData _data;
    public QuickLinksFuncs(IQuickLinksData data) => _data = data;
    /// <summary>
    /// Converts persistence rows at the service boundary so controller responses are not tied
    /// to stored-procedure column shapes.
    /// </summary>
    public async Task<List<QuickLink>> GetQuickLinks(Guid userId) =>
        (await _data.GetQuickLinks(userId)).Adapt<List<QuickLink>>();
    public Task<Guid> CreateQuickLink(Guid userId, string title, string url, string? iconClass) { Validate(title, url); return _data.CreateQuickLink(userId, Guid.NewGuid(), title.Trim(), NormaliseUrl(url), iconClass?.Trim()); }
    public Task UpdateQuickLink(Guid userId, Guid quickLinkId, string title, string url, string? iconClass) { Validate(title, url); return _data.UpdateQuickLink(userId, quickLinkId, title.Trim(), NormaliseUrl(url), iconClass?.Trim()); }
    public Task DeleteQuickLink(Guid userId, Guid quickLinkId) => _data.DeleteQuickLink(userId, quickLinkId);
    public Task UpdateOrder(Guid userId, IReadOnlyList<Guid> quickLinkIds, CancellationToken cancellationToken = default)
    {
        if (quickLinkIds.Count > 500 || quickLinkIds.Any(id => id == Guid.Empty))
            throw new InvalidOperationException("The quick link order was invalid.");
        return _data.UpdateOrder(userId, quickLinkIds.Distinct().ToList(), cancellationToken);
    }
    private static void Validate(string title, string url) { if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 100) throw new InvalidOperationException("Enter a link title up to 100 characters."); if (!Uri.TryCreate(NormaliseUrl(url), UriKind.Absolute, out Uri? uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)) throw new InvalidOperationException("Enter a valid http or https URL."); }
    private static string NormaliseUrl(string url) => url.Trim().StartsWith("http", StringComparison.OrdinalIgnoreCase) ? url.Trim() : "https://" + url.Trim();
}
