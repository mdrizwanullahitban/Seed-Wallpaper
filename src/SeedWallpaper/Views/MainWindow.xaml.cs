using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using SeedWallpaper.Services;

namespace SeedWallpaper.Views;

public partial class MainWindow : Window
{
    private readonly LocalWallpaperService _library = new();
    private string? _selectedPath;

    public MainWindow()
    {
        InitializeComponent();
        ShowGarden();
    }

    private void SetPage(string title, string description, bool showLibrary)
    {
        PageTitle.Text = title;
        PageDescription.Text = description;
        GalleryList.Visibility = showLibrary ? Visibility.Visible : Visibility.Collapsed;
        ImportButton.Visibility = showLibrary ? Visibility.Visible : Visibility.Collapsed;
        RefreshButton.Visibility = showLibrary ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ShowGarden()
    {
        SetPage("My Garden", "Import local JPG, PNG and BMP wallpapers, preview and apply them.", true);
        GalleryList.ItemsSource = _library.ListImages();
    }

    private void Home_Click(object sender, RoutedEventArgs e) =>
        SetPage("Welcome to Seed", "Choose My Garden to import and apply wallpapers.", false);
    private void Discover_Click(object sender, RoutedEventArgs e) =>
        SetPage("Discover", "Online wallpaper discovery is coming in the next Phase 2 milestone.", false);
    private void Studio_Click(object sender, RoutedEventArgs e) =>
        SetPage("Seed Studio", "AI image generation and reference editing will arrive in a later phase.", false);
    private void Garden_Click(object sender, RoutedEventArgs e) => ShowGarden();
    private void Settings_Click(object sender, RoutedEventArgs e) =>
        SetPage("Settings", "Configuration options are planned for later milestones.", false);

    private void Refresh_Click(object sender, RoutedEventArgs e) => ShowGarden();

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog
        {
            Filter = "Supported images|*.jpg;*.jpeg;*.png;*.bmp",
            Multiselect = true
        };
        if (picker.ShowDialog() != true) return;
        try
        {
            foreach (var file in picker.FileNames) _library.Import(file);
            ShowGarden();
            StatusText.Text = $"Imported {picker.FileNames.Length} image(s).";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Import failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Gallery_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GalleryList.SelectedItem is not string path) return;
        _selectedPath = path;
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.DecodePixelWidth = 700;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            PreviewImage.Source = bitmap;
            SelectedText.Text = Path.GetFileName(path);
        }
        catch (Exception ex)
        {
            StatusText.Text = "Preview failed: " + ex.Message;
        }
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedPath is null || !File.Exists(_selectedPath))
        {
            MessageBox.Show("Choose a wallpaper from My Garden first.", "Seed Wallpaper");
            return;
        }

        var dialog = new ApplyWallpaperDialog { Owner = this };
        if (dialog.ShowDialog() != true) return;
        try
        {
            if (dialog.ApplyDesktop)
            {
                DesktopWallpaperService.Apply(_selectedPath, dialog.Fit);
                StatusText.Text = "Desktop wallpaper applied successfully.";
            }
            if (dialog.OpenLockSettings)
            {
                Process.Start(new ProcessStartInfo("ms-settings:lockscreen") { UseShellExecute = true });
                StatusText.Text = dialog.ApplyDesktop
                    ? "Desktop applied. Finish lock screen setup in Windows Settings."
                    : "Choose your lock screen image in Windows Settings.";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Could not apply wallpaper", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
