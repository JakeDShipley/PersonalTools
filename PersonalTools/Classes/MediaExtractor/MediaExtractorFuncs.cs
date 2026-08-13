using HtmlAgilityPack;
using PersonalTools.Entities.MediaExtractor;
using System.Net;
using System.Text.RegularExpressions;

namespace PersonalTools.Classes.MediaExtractor;

public interface IMediaExtractorFuncs { Task<List<MediaItemObj>> Parse(string source); }

public sealed class MediaExtractorFuncs : IMediaExtractorFuncs
{
    private readonly HttpClient _httpClient;
    public MediaExtractorFuncs(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<List<MediaItemObj>> Parse(string source)
    {
        if (string.IsNullOrWhiteSpace(source)) return new List<MediaItemObj>();
        Uri? baseUri = null;
        if (Uri.TryCreate(source.Trim(), UriKind.Absolute, out Uri? pageUri) && IsSafeHttpUri(pageUri)) { baseUri = pageUri; source = await _httpClient.GetStringAsync(pageUri); }
        HtmlDocument document = new(); document.LoadHtml(source);
        List<MediaItemObj> results = new();

        foreach (HtmlNode image in document.DocumentNode.SelectNodes("//img") ?? Enumerable.Empty<HtmlNode>())
        {
            Add(BestFromSrcSet(image.GetAttributeValue("srcset", string.Empty)), "Image", baseUri, results);
            Add(FirstValue(image, "data-src", "data-original", "data-lazy", "data-url", "src"), "Image", baseUri, results);
        }
        foreach (HtmlNode video in document.DocumentNode.SelectNodes("//video|//video/source") ?? Enumerable.Empty<HtmlNode>()) Add(FirstValue(video, "src", "data-src"), "Video", baseUri, results);
        foreach (HtmlNode sourceNode in document.DocumentNode.SelectNodes("//picture/source") ?? Enumerable.Empty<HtmlNode>()) Add(BestFromSrcSet(sourceNode.GetAttributeValue("srcset", string.Empty)), "Image", baseUri, results);
        foreach (HtmlNode meta in document.DocumentNode.SelectNodes("//meta[@content]") ?? Enumerable.Empty<HtmlNode>())
        {
            string property = meta.GetAttributeValue("property", string.Empty); string name = meta.GetAttributeValue("name", string.Empty);
            if (property is "og:image" or "og:image:url" or "og:video" || name is "twitter:image" or "twitter:image:src" or "twitter:player:stream") Add(meta.GetAttributeValue("content", string.Empty), property.Contains("video") || name.Contains("stream") ? "Video" : "Image", baseUri, results);
        }
        foreach (Match match in Regex.Matches(source, @"url\(([^)]+)\)", RegexOptions.IgnoreCase)) Add(match.Groups[1].Value, "Image", baseUri, results);
        foreach (Match match in Regex.Matches(source, @"https?:\\?/\\?/[^s\""'<>]+\.(?:avif|bmp|gif|jpe?g|png|svg|webp|mp4|webm|mov|m3u8)(?:\?[^s\""'<>]*)?", RegexOptions.IgnoreCase)) Add(match.Value.Replace("\\/", "/"), GetType(match.Value), baseUri, results);

        return results.Where(item => !IsUnwanted(item.Url)).GroupBy(item => item.Url, StringComparer.OrdinalIgnoreCase).Select(group => group.First()).Take(250).ToList();
    }

    private static string FirstValue(HtmlNode node, params string[] attributes) => attributes.Select(attribute => node.GetAttributeValue(attribute, string.Empty)).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    private static string BestFromSrcSet(string value) => value.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(item => item.Trim().Split(' ')[0]).LastOrDefault() ?? string.Empty;
    private static string GetType(string value) => Regex.IsMatch(value, @"\.(mp4|webm|mov|m3u8)(\?|$)", RegexOptions.IgnoreCase) ? "Video" : "Image";
    private static bool IsUnwanted(string url) => Regex.IsMatch(url, @"(?:google-analytics|googletagmanager|doubleclick|pixel|tracking|spacer|blank\.gif)", RegexOptions.IgnoreCase);
    private static void Add(string value, string type, Uri? baseUri, List<MediaItemObj> results)
    {
        value = WebUtility.HtmlDecode(value ?? string.Empty).Trim().Trim('"', '\'', ' ');
        if (string.IsNullOrWhiteSpace(value) || value.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return;
        if (!Uri.TryCreate(baseUri, value, out Uri? uri) || !IsSafeHttpUri(uri)) return;
        string name = Path.GetFileName(uri.LocalPath);
        results.Add(new MediaItemObj { Url = uri.AbsoluteUri, Type = type, Name = string.IsNullOrWhiteSpace(name) ? uri.Host : name, Extension = Path.GetExtension(uri.LocalPath), SizeFormatted = "—" });
    }
    private static bool IsSafeHttpUri(Uri uri)
    {
        if (uri.Scheme is not ("http" or "https") || uri.IsLoopback || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)) return false;
        if (!IPAddress.TryParse(uri.Host, out IPAddress? ip)) return true;
        byte[] bytes = ip.GetAddressBytes(); return !(IPAddress.IsLoopback(ip) || ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any) || (bytes.Length == 4 && (bytes[0] == 10 || bytes[0] == 127 || (bytes[0] == 192 && bytes[1] == 168) || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31))));
    }
}
