using System.Net.Http;
using System.Text.Json;
using System.IO;
using System.Net;

namespace SeedWallpaper.Services;

public sealed class OpenverseWallpaperService
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(25) };

    public async Task<IReadOnlyList<OnlineWallpaper>> SearchAsync(string query, int page = 1)
    {
        var url = "https://api.openverse.org/v1/images/?q=" + Uri.EscapeDataString(query)
            + "&page=" + page + "&page_size=40&aspect_ratio=wide&size=large";
        using var response = await Client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());
        var images = new List<OnlineWallpaper>();
        foreach (var item in document.RootElement.GetProperty("results").EnumerateArray())
        {
            var image = Get(item, "url");
            var thumbnail = Get(item, "thumbnail");
            var source = Get(item, "foreign_landing_url");
            if (!IsPublicHttps(image) || !IsPublicHttps(thumbnail) || !IsPublicHttps(source)) continue;
            if (!new[] { ".jpg", ".jpeg", ".png" }.Contains(Path.GetExtension(new Uri(image).AbsolutePath), StringComparer.OrdinalIgnoreCase)) continue;
            images.Add(new OnlineWallpaper(Get(item, "title"), thumbnail, image, source,
                Get(item, "license") + " " + Get(item, "license_version"), Get(item, "creator")));
        }
        return images;
    }

    public async Task<string> DownloadAsync(OnlineWallpaper wallpaper)
    {
        if (!IsPublicHttps(wallpaper.DownloadUrl)) throw new InvalidOperationException("Unsupported download URL.");
        var uri = new Uri(wallpaper.DownloadUrl);
        var extension = Path.GetExtension(uri.AbsolutePath).ToLowerInvariant();
        if (!new[] { ".jpg", ".jpeg", ".png" }.Contains(extension)) throw new InvalidOperationException("Unsupported format.");
        using var response = await Client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is > 30_000_000) throw new InvalidOperationException("Image exceeds 30 MB.");
        var temp = Path.Combine(Path.GetTempPath(), "Seed-" + Guid.NewGuid().ToString("N") + extension);
        try
        {
            await using (var output = File.Create(temp))
            await using (var input = await response.Content.ReadAsStreamAsync())
            {
                var buffer = new byte[81920];
                long size = 0;
                int n;
                while ((n = await input.ReadAsync(buffer)) != 0)
                {
                    size += n;
                    if (size > 30_000_000) throw new InvalidOperationException("Image exceeds 30 MB.");
                    await output.WriteAsync(buffer.AsMemory(0, n));
                }
            }
            return new LocalWallpaperService().Import(temp);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    private static bool IsPublicHttps(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.UserInfo.Length > 0) return false;
        if (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)) return false;
        if (IPAddress.TryParse(uri.Host, out var ip))
            return !IPAddress.IsLoopback(ip) && !ip.IsIPv6LinkLocal && !ip.IsIPv6SiteLocal
                && !(ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                    (ip.GetAddressBytes()[0] == 10 || ip.GetAddressBytes()[0] == 127 ||
                     (ip.GetAddressBytes()[0] == 192 && ip.GetAddressBytes()[1] == 168) ||
                     (ip.GetAddressBytes()[0] == 172 && ip.GetAddressBytes()[1] is >= 16 and <= 31)));
        return true;
    }

    private static string Get(JsonElement element, string key) =>
        element.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? "" : "";
}
