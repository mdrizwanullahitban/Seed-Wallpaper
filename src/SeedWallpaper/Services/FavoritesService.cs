using System.IO;
using System.Text.Json;

namespace SeedWallpaper.Services;

public sealed class FavoritesService
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SeedWallpaper", "favorites.json");

    private readonly HashSet<string> _items;

    public FavoritesService()
    {
        try
        {
            _items = File.Exists(FilePath)
                ? new HashSet<string>(JsonSerializer.Deserialize<string[]>(File.ReadAllText(FilePath))
                    ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            _items = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public bool Contains(string path) => _items.Contains(path);

    public bool Toggle(string path)
    {
        var added = _items.Add(path);
        if (!added) _items.Remove(path);
        Save();
        return added;
    }

    public void Remove(string path)
    {
        if (_items.Remove(path)) Save();
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(_items.OrderBy(x => x).ToArray()));
    }
}
