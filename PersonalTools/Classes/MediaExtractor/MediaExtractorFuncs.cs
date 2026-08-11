using HtmlAgilityPack;
using PersonalTools.Entities.MediaExtractor;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace PersonalTools.Classes.MediaExtractor
{
    public interface IMediaExtractorFuncs
    {
        Task<List<MediaItemObj>> Parse(string html);
    }

    public class MediaExtractorFuncs : IMediaExtractorFuncs
    {
        private readonly HttpClient _httpClient;

        public MediaExtractorFuncs(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="html"></param>
        /// <returns></returns>
        public async Task<List<MediaItemObj>> Parse(string html)
        {
            List<MediaItemObj> items = new();

            HtmlDocument doc = new();
            doc.LoadHtml(html);

            ParseImages(doc, items);

            ParseVideos(doc, items);

            ParseSources(doc, items);

            ParseCssBackgrounds(doc, items);

            ParseRegexMedia(html, items);

            var metaImages = ExtractMetaImages(doc);

            foreach (var url in metaImages)
            {
                AddMedia(url, "Image", items);
            }

            items = items
                .Where(x => !string.IsNullOrWhiteSpace(x.Url))
                .GroupBy(x => x.Url)
                .Select(x => x.First())
                .ToList();

            await Task.WhenAll(items.Select(async item =>
            {
                await PopulateSize(item);

                if (item.Type == "Image")
                {
                    var (w, h) = await GetImageDimensions(item.Url);
                    item.Width = w;
                    item.Height = h;
                }

                if (item.Type == "Video")
                {
                    item.DurationSeconds = await GetVideoDuration(item.Url);
                    item.DurationFormatted = TimeSpan.FromSeconds(item.DurationSeconds).ToString(@"mm\:ss");
                }
            }));

            return items;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="doc"></param>
        /// <param name="items"></param>
        private void ParseImages(HtmlDocument doc, List<MediaItemObj> items)
        {
            var nodes = doc.DocumentNode.SelectNodes("//img");

            if (nodes == null)
                return;

            foreach (var node in nodes)
            {
                // 1. normal src
                string src = node.GetAttributeValue("src", "");

                // 2. lazy-load patterns
                string dataSrc =
                    node.GetAttributeValue("data-src", "") ??
                    node.GetAttributeValue("data-original", "") ??
                    node.GetAttributeValue("data-lazy", "") ??
                    node.GetAttributeValue("data-url", "");

                // 3. srcset (take BEST quality image)
                string srcset = node.GetAttributeValue("srcset", "");

                string? bestFromSrcset = GetBestFromSrcSet(srcset);

                // 4. decide best candidate
                string finalUrl = bestFromSrcset ?? dataSrc ?? src;

                AddMedia(finalUrl, "Image", items);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="srcset"></param>
        /// <returns></returns>
        private string? GetBestFromSrcSet(string srcset)
        {
            if (string.IsNullOrWhiteSpace(srcset))
                return null;

            // format: "img1.jpg 480w, img2.jpg 1200w"
            var parts = srcset.Split(',');

            string? bestUrl = null;
            int bestWidth = 0;

            foreach (var part in parts)
            {
                var trimmed = part.Trim();

                var spaceIndex = trimmed.LastIndexOf(' ');
                if (spaceIndex == -1)
                    continue;

                var url = trimmed.Substring(0, spaceIndex);
                var widthPart = trimmed.Substring(spaceIndex + 1);

                int width = 0;

                if (widthPart.EndsWith("w"))
                {
                    int.TryParse(widthPart.Replace("w", ""), out width);
                }

                if (width > bestWidth)
                {
                    bestWidth = width;
                    bestUrl = url;
                }
            }

            return bestUrl;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="doc"></param>
        /// <param name="items"></param>
        private void ParseVideos(HtmlDocument doc, List<MediaItemObj> items)
        {
            var videoNodes = doc.DocumentNode.SelectNodes("//video");

            if (videoNodes == null)
                return;

            foreach (var node in videoNodes)
            {
                // direct video src
                string src = node.GetAttributeValue("src", "");
                AddMedia(src, "Video", items);

                // fallback source tags inside video
                var sourceNodes = node.SelectNodes(".//source");

                if (sourceNodes != null)
                {
                    foreach (var source in sourceNodes)
                    {
                        string sourceSrc = source.GetAttributeValue("src", "");
                        AddMedia(sourceSrc, "Video", items);
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="doc"></param>
        /// <param name="items"></param>
        private void ParseSources(HtmlDocument doc, List<MediaItemObj> items)
        {
            var nodes = doc.DocumentNode.SelectNodes("//source");

            if (nodes == null)
                return;

            foreach (var node in nodes)
            {
                string src = node.GetAttributeValue("src", "");

                AddMedia(src, GetMediaType(src), items);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="html"></param>
        /// <param name="items"></param>
        private void ParseCssBackgrounds(HtmlDocument doc, List<MediaItemObj> items)
        {
            var nodes = doc.DocumentNode.SelectNodes("//*[@style]");

            if (nodes != null)
            {
                foreach (var node in nodes)
                {
                    string style = node.GetAttributeValue("style", "");

                    ExtractCssUrls(style, items);
                }
            }

            // also scan full HTML (for <style> blocks)
            var styleNodes = doc.DocumentNode.SelectNodes("//style");

            if (styleNodes != null)
            {
                foreach (var node in styleNodes)
                {
                    ExtractCssUrls(node.InnerText, items);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="input"></param>
        /// <param name="items"></param>
        private void ExtractCssUrls(string input, List<MediaItemObj> items)
        {
            if (string.IsNullOrWhiteSpace(input))
                return;

            // matches: url(...)
            var matches = Regex.Matches(
                input,
                @"url\((.*?)\)",
                RegexOptions.IgnoreCase);

            foreach (Match match in matches)
            {
                string url = match.Groups[1].Value
                    .Trim()
                    .Trim('"', '\'', ' ');

                if (string.IsNullOrWhiteSpace(url))
                    continue;

                // ignore CSS variables and data URIs edge cases
                if (url.StartsWith("data:") || url.StartsWith("var("))
                    continue;

                AddMedia(url, "Image", items);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="html"></param>
        /// <param name="items"></param>
        private void ParseRegexMedia(string html, List<MediaItemObj> items)
        {
            Regex regex = new(
                @"https?:\/\/[^\s""']+\.(jpg|jpeg|png|gif|webp|bmp|mp4|webm|mov|m3u8)",
                RegexOptions.IgnoreCase);

            foreach (Match match in regex.Matches(html))
            {
                string url = match.Value;

                AddMedia(url, GetMediaType(url), items);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="url"></param>
        /// <param name="type"></param>
        /// <param name="items"></param>
        private void AddMedia(string url, string type, List<MediaItemObj> items)
        {
            if (string.IsNullOrWhiteSpace(url))
                return;

            if (items.Any(x => x.Url == url))
                return;

            items.Add(new MediaItemObj
            {
                Url = url,
                Type = type,
                Name = GetFileName(url),
                Extension = Path.GetExtension(url)
            });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        private string GetMediaType(string url)
        {
            string ext = Path.GetExtension(url).ToLower();

            return ext switch
            {
                ".mp4" => "Video",
                ".webm" => "Video",
                ".mov" => "Video",
                ".m3u8" => "Video",
                _ => "Image"
            };
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        private async Task PopulateSize(MediaItemObj item)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Head, item.Url);
                using var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode &&
                    response.Content.Headers.ContentLength != null)
                {
                    item.SizeBytes = response.Content.Headers.ContentLength.Value;
                }
                else
                {
                    using var getResponse = await _httpClient.GetAsync(item.Url, HttpCompletionOption.ResponseHeadersRead);

                    item.SizeBytes = getResponse.Content.Headers.ContentLength ?? 0;
                }

                item.SizeFormatted = FormatSize(item.SizeBytes);
            }
            catch
            {
                item.SizeFormatted = "Unknown";
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        private string FormatSize(long bytes)
        {
            if (bytes == 0)
                return "";

            double mb = bytes / 1024d / 1024d;

            return $"{mb:F2} MB";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        private string GetFileName(string url)
        {
            try
            {
                Uri uri = new(url);

                string file = Path.GetFileName(uri.LocalPath);

                if (string.IsNullOrWhiteSpace(file))
                    return uri.Host;

                return file;
            }
            catch
            {
                return url;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="doc"></param>
        /// <returns></returns>
        private List<string> ExtractMetaImages(HtmlDocument doc)
        {
            var results = new List<string>();

            var metaTags = doc.DocumentNode.SelectNodes("//meta");

            if (metaTags == null)
                return results;

            foreach (var meta in metaTags)
            {
                var property = meta.GetAttributeValue("property", "");
                var name = meta.GetAttributeValue("name", "");
                var content = meta.GetAttributeValue("content", "");

                if (string.IsNullOrWhiteSpace(content))
                    continue;

                if (property == "og:image" || property == "og:image:url" || name == "twitter:image" || name == "twitter:image:src")
                    results.Add(content);
            }

            return results.Distinct().ToList();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        private async Task<(int width, int height)> GetImageDimensions(string url)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Head, url);
                using var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                    return (0, 0);

                // Try common headers first (rare but fast when available)
                if (response.Content.Headers.TryGetValues("X-Image-Width", out var wVals) &&
                    response.Content.Headers.TryGetValues("X-Image-Height", out var hVals))
                {
                    int.TryParse(wVals.FirstOrDefault(), out int w);
                    int.TryParse(hVals.FirstOrDefault(), out int h);
                    return (w, h);
                }

                return (0, 0);
            }
            catch
            {
                return (0, 0);
            }
        }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="url"></param>
            /// <returns></returns>
            private Task<double> GetVideoDuration(string url)
            {
                // Server-side duration detection removed for safety and reliability
                return Task.FromResult(0d);
            }
    }
}