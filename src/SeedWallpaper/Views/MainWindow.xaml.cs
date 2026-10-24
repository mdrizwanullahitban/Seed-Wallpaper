using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using SeedWallpaper.Services;

namespace SeedWallpaper.Views;

public partial class MainWindow : Window
{
    private readonly LocalWallpaperService _library = new();
    private readonly FavoritesService _favorites = new();
    private readonly Brush _activeBrush = (Brush)new BrushConverter().ConvertFromString("#1C6D75")!;
    private readonly Brush _inactiveBrush = Brushes.Transparent;
    private string? _selectedPath;
    private bool _showLibrary = true;
    private IReadOnlyList<string> _images = Array.Empty<string>();

    public MainWindow()
    {
        InitializeComponent();
        ShowGarden();
        ClearSelection();
    }

    private void SetPage(string title, string description, bool showLibrary, Button active)
    {
        PageTitle.Text = title;
        PageDescription.Text = description;
        _showLibrary = showLibrary;
        GalleryList.Visibility = showLibrary ? Visibility.Visible : Visibility.Collapsed;
        EmptyHint.Visibility = showLibrary && _images.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ImportButton.Visibility = showLibrary ? Visibility.Visible : Visibility.Collapsed;
        RefreshButton.Visibility = showLibrary ? Visibility.Visible : Visibility.Collapsed;
        SearchBox.IsEnabled = showLibrary;
        foreach (var button in new[] { HomeButton, DiscoverButton, StudioButton, GardenButton, SettingsButton })
            button.Background = button == active ? _activeBrush : _inactiveBrush;
    }

    private void ShowGarden()
    {
        _images = _library.ListImages();
        SetPage("My Garden", "Your personal collection. Import, preview and apply wallpapers.", true, GardenButton);
        FilterGallery();
    }

    private void FilterGallery()
    {
        var term = SearchBox.Text.Trim();
        var filtered = _images.Where(path => Path.GetFileName(path).Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
        GalleryList.ItemsSource = filtered;
        if (_selectedPath is not null && filtered.Contains(_selectedPath))
            GalleryList.SelectedItem = _selectedPath;
        else
            ClearSelection();
        EmptyHint.Text = _images.Count == 0
            ? "Your garden is waiting.\nImport your first wallpaper to begin."
            : "No wallpapers match your search.";
        EmptyHint.Visibility = _showLibrary && filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ClearSelection()
    {
        _selectedPath = null;
        PreviewImage.Source = null;
        SelectedText.Text = "Select a wallpaper";
        FavoriteButton.Content = "♡  Add to Favourites";
        ApplyButton.IsEnabled = false;
        FavoriteButton.IsEnabled = false;
        DeleteButton.IsEnabled = false;
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(LocalWallpaperService.GardenFolder);
        Process.Start(new ProcessStartInfo("explorer.exe", LocalWallpaperService.GardenFolder)
        { UseShellExecute = true });
    }

    private void Favorite_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedPath is null) return;
        try
        {
            var added = _favorites.Toggle(_selectedPath);
            FavoriteButton.Content = added ? "♥  Remove Favourite" : "♡  Add to Favourites";
            StatusText.Text = added ? "Added to favourites." : "Removed from favourites.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Could not save favourite", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedPath is null) return;
        var path = _selectedPath;
        if (MessageBox.Show("Remove this image from My Garden? The imported copy will be deleted.",
            "Confirm removal", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        try
        {
            PreviewImage.Source = null;
            File.Delete(path);
            _favorites.Remove(path);
            ClearSelection();
            ShowGarden();
            StatusText.Text = "Wallpaper removed from My Garden.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Could not remove wallpaper", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Search_Changed(object sender, TextChangedEventArgs e)
    {
        if (GalleryList is not null) FilterGallery();
    }

    private void Home_Click(object sender, RoutedEventArgs e) =>
        SetPage("Welcome to Seed", "Your calm space for beautiful desktop wallpapers. Visit My Garden to get started.", false, HomeButton);
    private void Discover_Click(object sender, RoutedEventArgs e) =>
        SetPage("Discover", "Online wallpaper discovery is planned for the next milestone.", false, DiscoverButton);
    private void Studio_Click(object sender, RoutedEventArgs e) =>
        SetPage("Seed Studio", "AI generation and reference-image editing are planned for a future milestone.", false, StudioButton);
    private void Garden_Click(object sender, RoutedEventArgs e) => ShowGarden();
    private void Settings_Click(object sender, RoutedEventArgs e) =>
        SetPage("Settings", "More personalisation options are coming soon.", false, SettingsButton);

    private void Refresh_Click(object sender, RoutedEventArgs e) => ShowGarden();

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog { Filter = "Supported images|*.jpg;*.jpeg;*.png;*.bmp", Multiselect = true };
        if (picker.ShowDialog() != true) return;
        try
        {
            foreach (var file in picker.FileNames) _library.Import(file);
            SearchBox.Clear();
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
            bitmap.DecodePixelWidth = 900;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            PreviewImage.Source = bitmap;
            SelectedText.Text = Path.GetFileName(path);
            FavoriteButton.Content = _favorites.Contains(path) ? "♥  Remove Favourite" : "♡  Add to Favourites";
            ApplyButton.IsEnabled = true;
            FavoriteButton.IsEnabled = true;
            DeleteButton.IsEnabled = true;
        }
        catch (Exception ex) { ClearSelection(); StatusText.Text = "Preview failed: " + ex.Message; }
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
