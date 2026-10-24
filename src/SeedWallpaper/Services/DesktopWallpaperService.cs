using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace SeedWallpaper.Services;

public static class DesktopWallpaperService
{
    private const int SetDesktopWallpaper = 0x0014;
    private const int UpdateProfile = 0x01;
    private const int BroadcastChange = 0x02;

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(int action, int parameter, string value, int flags);

    public static void Apply(string path, string fit)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Image not found.", path);
        var (style, tile) = fit switch
        {
            "Fit" => ("6", "0"),
            "Stretch" => ("2", "0"),
            "Center" => ("0", "0"),
            "Tile" => ("0", "1"),
            "Span" => ("22", "0"),
            _ => ("10", "0") // Fill
        };
        using var settings = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", writable: true)
            ?? throw new InvalidOperationException("Windows wallpaper settings are unavailable.");
        settings.SetValue("WallpaperStyle", style);
        settings.SetValue("TileWallpaper", tile);
        if (!SystemParametersInfo(SetDesktopWallpaper, 0, path, UpdateProfile | BroadcastChange))
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }
}
