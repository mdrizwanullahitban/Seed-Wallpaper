using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;

namespace SeedWallpaper.Services;

public sealed class AiWallpaperService
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromMinutes(3) };

    public async Task<string> GenerateAsync(string prompt, string model, int width, int height, string apiKey)
    {
        if (string.IsNullOrWhiteSpace(prompt)) throw new ArgumentException("Enter a description of your wallpaper.");
        if (string.IsNullOrWhiteSpace(apiKey)) throw new ArgumentException("Enter your Pollinations API key.");
        if (prompt.Length > 1000) throw new ArgumentException("Prompt must be 1000 characters or fewer.");
        if (width is < 512 or > 1920 || height is < 512 or > 1920) throw new ArgumentException("Unsupported resolution.");
        var url = "https://gen.pollinations.ai/image/" + Uri.EscapeDataString(prompt.Trim()) +
                  "?model=" + Uri.EscapeDataString(model) + "&width=" + width + "&height=" + height +
                  "&seed=" + Random.Shared.Next(1, int.MaxValue) + "&nologo=true";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
        using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Generation failed ({(int)response.StatusCode}). Check your key, balance and model access.");
        if (response.Content.Headers.ContentType?.MediaType is not ("image/jpeg" or "image/png" or "image/webp"))
            throw new InvalidDataException("The AI service did not return an image.");
        var ext = response.Content.Headers.ContentType?.MediaType == "image/png" ? ".png" : ".jpg";
        var temp = Path.Combine(Path.GetTempPath(), "seed-ai-" + Guid.NewGuid().ToString("N") + ext);
        try
        {
            await using (var stream = File.Create(temp))
            await using (var incoming = await response.Content.ReadAsStreamAsync())
            {
                var buffer = new byte[81920];
                long total = 0;
                int count;
                while ((count = await incoming.ReadAsync(buffer)) != 0)
                {
                    total += count;
                    if (total > 30_000_000) throw new InvalidDataException("Generated image exceeds 30 MB.");
                    await stream.WriteAsync(buffer.AsMemory(0, count));
                }
            }
            return new LocalWallpaperService().Import(temp);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
