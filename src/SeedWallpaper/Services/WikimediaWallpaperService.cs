using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.IO;

namespace SeedWallpaper.Services;

public sealed record OnlineWallpaper(string Title, string PreviewUrl, string DownloadUrl,
    string PageUrl, string License, string Artist);

public sealed class WikimediaWallpaperService
{
    private static readonly HttpClient Client = new()
    {
        Timeout = TimeSpan.FromSeconds(25),
        DefaultRequestHeaders = { UserAgent = { new System.Net.Http.Headers.ProductInfoHeaderValue("SeedWallpaper", "0.2") } }
    };

    public async Task<IReadOnlyList<OnlineWallpaper>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return Array.Empty<OnlineWallpaper>();
        var parameters = new Dictionary<string, string>
        {
            ["action"] = "query", ["format"] = "json", ["generator"] = "search",
            ["gsrsearch"] = "filetype:bitmap " + query.Trim(), ["gsrnamespace"] = "6",
            ["gsrlimit"] = "24", ["prop"] = "imageinfo", ["iiprop"] = "url|extmetadata|size",
            ["iiurlwidth"] = "480"
        };
        var url = "https://commons.wikimedia.org/w/api.php?" +
            string.Join("&", parameters.Select(kv => Uri.EscapeDataString(kv.Key) + "=" + Uri.EscapeDataString(kv.Value)));
        using var response = await Client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        if (!json.RootElement.TryGetProperty("query", out var q) ||
            !q.TryGetProperty("pages", out var pages)) return Array.Empty<OnlineWallpaper>();
        var result = new List<OnlineWallpaper>();
        foreach (var page in pages.EnumerateObject())
        {
            var entry = page.Value;
            if (!entry.TryGetProperty("imageinfo", out var info) || info.GetArrayLength() == 0) continue;
            var image = info[0];
            var original = GetString(image, "url");
            if (!Uri.TryCreate(original, UriKind.Absolute, out var imageUri) ||
                imageUri.Scheme != Uri.UriSchemeHttps) continue;
            if (!new[] { ".jpg", ".jpeg", ".png" }.Contains(Path.GetExtension(imageUri.AbsolutePath), StringComparer.OrdinalIgnoreCase))
                continue;
            var preview = GetString(image, "thumburl");
            if (string.IsNullOrWhiteSpace(preview)) preview = original;
            var meta = image.TryGetProperty("extmetadata", out var m) ? m : default;
            var license = Meta(meta, "LicenseShortName");
            var artist = StripHtml(Meta(meta, "Artist"));
            var title = GetString(entry, "title").Replace("File:", "");
            var pageUrl = GetString(image, "descriptionurl");
            if (string.IsNullOrWhiteSpace(pageUrl)) continue;
            result.Add(new OnlineWallpaper(title, preview, original, pageUrl,
                string.IsNullOrWhiteSpace(license) ? "Check source licence" : license,
                string.IsNullOrWhiteSpace(artist) ? "See source for attribution" : artist));
        }
        return result;
    }

    public async Task<string> DownloadAsync(OnlineWallpaper wallpaper, CancellationToken cancellationToken = default)
    {
        var uri = new Uri(wallpaper.DownloadUrl);
        if (uri.Scheme != Uri.UriSchemeHttps || !(uri.Host.Equals("upload.wikimedia.org", StringComparison.OrdinalIgnoreCase)
            || uri.Host.EndsWith(".wikimedia.org", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Unsupported image host.");
        using var response = await Client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is > 30_000_000)
            throw new InvalidOperationException("Image is larger than the 30 MB download limit.");
        var extension = Path.GetExtension(uri.AbsolutePath).ToLowerInvariant();
        if (!new[] { ".jpg", ".jpeg", ".png" }.Contains(extension))
            throw new InvalidOperationException("Unsupported image format.");
        var temp = Path.Combine(Path.GetTempPath(), "SeedWallpaper-" + Guid.NewGuid().ToString("N") + extension);
        try
        {
            await using (var output = File.Create(temp))
            await using (var input = await response.Content.ReadAsStreamAsync(cancellationToken))
            {
                var buffer = new byte[81920];
                long total = 0;
                int count;
                while ((count = await input.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    total += count;
                    if (total > 30_000_000) throw new InvalidOperationException("Image exceeds the 30 MB download limit.");
                    await output.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
                }
            }
            return new LocalWallpaperService().Import(temp);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    private static string GetString(JsonElement obj, string name) =>
        obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? "" : "";
    private static string Meta(JsonElement obj, string name) =>
        obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(name, out var item)
            ? GetString(item, "value") : "";
    private static string StripHtml(string value) =>
        Regex.Replace(value, "<[^>]*>", "").Trim();
}
