using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PersonalTools.Pages.Shared;
using PersonalTools.Security;
using PersonalTools.Classes.MediaExtractor;
using PersonalTools.Entities.MediaExtractor;
using System.IO.Compression;
using System.Text.Json;
using System.IO;


namespace PersonalTools.Pages.MediaExtractor
{
    public class IndexModel : RoleRestrictedPageModel
    {
        private readonly IMediaExtractorFuncs _mediaExtractorFuncs;

        public IndexModel(IMediaExtractorFuncs mediaExtractorFuncs)
        {
            _mediaExtractorFuncs = mediaExtractorFuncs;
        }

        [BindProperty]
        public string SourceCode { get; set; } = "";

        public List<MediaItemObj> MediaItems { get; set; } = new();

        public async Task<IActionResult> OnPostParse()
        {
            if (!IsUserAllowedHere(AppRole.Admin)) return RedirectWhenUserIsNotAllowed();
            MediaItems = await _mediaExtractorFuncs.Parse(SourceCode);

            return Page();
        }

        public async Task<IActionResult> OnPostDownloadZip()
        {
            if (!IsUserAllowedHere(AppRole.Admin)) return RedirectWhenUserIsNotAllowed();
            using var reader = new StreamReader(Request.Body);
            var json = await reader.ReadToEndAsync();

            var urls = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json)
                       ?? new List<string>();

            using var ms = new MemoryStream();

            using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(30);

                foreach (var url in urls)
                {
                    try
                    {
                        var response = await client.GetAsync(url);

                        if (!response.IsSuccessStatusCode)
                            continue;

                        var bytes = await response.Content.ReadAsByteArrayAsync();

                        var safeName = Path.GetFileName(new Uri(url).LocalPath);

                        if (string.IsNullOrWhiteSpace(safeName))
                            safeName = Guid.NewGuid().ToString();

                        var entry = archive.CreateEntry(safeName);

                        using var entryStream = entry.Open();
                        await entryStream.WriteAsync(bytes);
                    }
                    catch
                    {
                        // skip broken files
                    }
                }
            }

            ms.Seek(0, SeekOrigin.Begin);
            ms.Position = 0;
            return File(ms, "application/zip", "media.zip");
        }

        public async Task<IActionResult> OnGetDownloadFile(string url)
        {
            if (!IsUserAllowedHere(AppRole.Admin)) return RedirectWhenUserIsNotAllowed();
            if (string.IsNullOrWhiteSpace(url))
                return BadRequest();

            try
            {
                using var client = new HttpClient();

                // Browser-like headers (reduces 403s on basic protection)
                client.DefaultRequestHeaders.UserAgent.ParseAdd(
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120 Safari/537.36");

                client.DefaultRequestHeaders.Accept.ParseAdd("*/*");

                // optional referrer (some sites ignore or block it)
                try
                {
                    client.DefaultRequestHeaders.Referrer =
                        new Uri("https://www.google.com/");
                }
                catch
                {
                    // ignore invalid referrer issues
                }

                var response = await client.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                    return StatusCode((int)response.StatusCode, "Failed to download file");

                var bytes = await response.Content.ReadAsByteArrayAsync();

                var fileName = Path.GetFileName(new Uri(url).LocalPath);

                if (string.IsNullOrWhiteSpace(fileName))
                    fileName = Guid.NewGuid().ToString();

                return File(bytes, "application/octet-stream", fileName);
            }
            catch
            {
                return BadRequest("Download failed");
            }
        }
        public IActionResult OnGet() => IsUserAllowedHere(AppRole.Admin) ? Page() : RedirectWhenUserIsNotAllowed();
    }
}
