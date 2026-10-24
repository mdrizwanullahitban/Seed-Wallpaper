using System.IO;
using System.Security.Cryptography;

namespace SeedWallpaper.Services;

public sealed class LocalWallpaperService
{
    private static readonly string Root = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SeedWallpaper", "MyGarden");

    public static string GardenFolder => Root;

    public IReadOnlyList<string> ListImages()
    {
        Directory.CreateDirectory(Root);
        return Directory.EnumerateFiles(Root)
            .Where(IsSupported).OrderByDescending(File.GetCreationTimeUtc).ToList();
    }

    public string Import(string source)
    {
        if (!File.Exists(source) || !IsSupported(source))
            throw new InvalidOperationException("Choose a JPG, PNG or BMP image.");
        Directory.CreateDirectory(Root);
        var extension = Path.GetExtension(source).ToLowerInvariant();
        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source)))[..20];
        var target = Path.Combine(Root, hash + extension);
        if (!File.Exists(target)) File.Copy(source, target);
        return target;
    }

    private static bool IsSupported(string path) =>
        new[] { ".jpg", ".jpeg", ".png", ".bmp" }.Contains(
            Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
}
