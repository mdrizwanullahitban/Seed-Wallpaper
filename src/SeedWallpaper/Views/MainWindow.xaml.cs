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
    private readonly WikimediaWallpaperService _onlineService = new();
    private readonly OpenverseWallpaperService _openverse = new();
    private readonly List<OnlineWallpaper> _onlineResults = new();
    private string _currentQuery = "landscape";
    private int _onlinePage = 0;
    private OnlineWallpaper? _selectedOnline;
    private string _page = "Home";
    private int _searchGeneration;
    private readonly Brush _activeBrush = (Brush)new BrushConverter().ConvertFromString("#1C6D75")!;
    private readonly Brush _inactiveBrush = Brushes.Transparent;
    private string? _selectedPath;
    private bool _showLibrary = true;
    private IReadOnlyList<string> _images = Array.Empty<string>();

    public MainWindow()
    {
        InitializeComponent();
        ShowHome();
        ClearSelection();
        Loaded += async (_, _) => await SearchOnlineAsync("featured pictures landscape");
    }

    private void SetPage(string title, string description, bool showLibrary, Button active)
    {
        PageTitle.Text = title;
        PageDescription.Text = description;
        _showLibrary = showLibrary;
        _page = active == DiscoverButton ? "Discover" : active == GardenButton ? "Garden" : active == HomeButton ? "Home" : active == StudioButton ? "Studio" : "Settings";
        OnlineList.Visibility = _page is "Discover" or "Home" ? Visibility.Visible : Visibility.Collapsed;
        if (_page is not ("Discover" or "Home")) _selectedOnline = null;
        BrowseButton.Visibility = _page == "Home" ? Visibility.Visible : Visibility.Collapsed;
        DownloadButton.Visibility = _page is "Discover" or "Home" ? Visibility.Visible : Visibility.Collapsed;
        SourceButton.Visibility = _page is "Discover" or "Home" ? Visibility.Visible : Visibility.Collapsed;
        CreditText.Visibility = _page is "Discover" or "Home" ? Visibility.Visible : Visibility.Collapsed;
        ApplyButton.Visibility = showLibrary ? Visibility.Visible : Visibility.Collapsed;
        FavoriteButton.Visibility = showLibrary ? Visibility.Visible : Visibility.Collapsed;
        DeleteButton.Visibility = showLibrary ? Visibility.Visible : Visibility.Collapsed;
        CategoryBar.Visibility = _page == "Home" ? Visibility.Visible : Visibility.Collapsed;
        MoreButton.Visibility = _page is "Discover" or "Home" ? Visibility.Visible : Visibility.Collapsed;
        GalleryHeading.Text = _page == "Discover" ? "Online Wallpapers" : _page == "Home" ? "Featured Wallpapers" : "Wallpapers";
        ClearSelection();
        GalleryList.Visibility = showLibrary ? Visibility.Visible : Visibility.Collapsed;
        EmptyHint.Visibility = showLibrary && _images.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ImportButton.Visibility = showLibrary ? Visibility.Visible : Visibility.Collapsed;
        RefreshButton.Visibility = showLibrary ? Visibility.Visible : Visibility.Collapsed;
        OpenFolderButton.Visibility = showLibrary ? Visibility.Visible : Visibility.Collapsed;
        SearchBox.IsEnabled = showLibrary || _page is "Discover" or "Home";
        SearchBox.ToolTip = _page is "Discover" or "Home" ? "Search wallpapers online and press Enter" : "Filter My Garden by filename";
        foreach (var button in new[] { HomeButton, DiscoverButton, StudioButton, GardenButton, SettingsButton })
            button.Background = button == active ? _activeBrush : _inactiveBrush;
    }

    private void ShowHome()
    {
        SetPage("Explore Collections", "Browse wallpaper categories and featured selections. Choose a collection to see online images.", false, HomeButton);
        EmptyHint.Text = "Loading featured wallpapers...";
        EmptyHint.Visibility = Visibility.Visible;
        StatusText.Text = "Welcome to Seed Wallpaper.";
    }

    private async void Category_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string query) return;
        GalleryHeading.Text = button.Content + " Wallpapers";
        await SearchOnlineAsync(query);
    }

    private async void Browse_Click(object sender, RoutedEventArgs e) => await ShowDiscoverAsync();

    private async Task ShowDiscoverAsync()
    {
        SetPage("Discover", "Search freely licensed images from Wikimedia Commons. Review each image's licence and attribution.", false, DiscoverButton);
        EmptyHint.Text = "Searching online wallpapers...";
        EmptyHint.Visibility = Visibility.Visible;
        if (string.IsNullOrWhiteSpace(SearchBox.Text)) SearchBox.Text = "nature landscape";
        await SearchOnlineAsync(SearchBox.Text);
    }

    private async Task SearchOnlineAsync(string term)
    {
        var generation = ++_searchGeneration;
        EmptyHint.Text = "Searching Wikimedia Commons...";
        EmptyHint.Visibility = Visibility.Visible;
        _currentQuery = term;
        _onlinePage = 0;
        _onlineResults.Clear();
        OnlineList.ItemsSource = null;
        DownloadButton.IsEnabled = false;
        SourceButton.IsEnabled = false;
        try
        {
            var results = await Task.WhenAll(
                _onlineService.SearchAsync(term).ContinueWith(t => t.IsCompletedSuccessfully ? t.Result : Array.Empty<OnlineWallpaper>()),
                _openverse.SearchAsync(term).ContinueWith(t => t.IsCompletedSuccessfully ? t.Result : Array.Empty<OnlineWallpaper>()));
            var images = results.SelectMany(x => x).DistinctBy(x => x.DownloadUrl).ToList();
            if (generation != _searchGeneration || _page is not ("Discover" or "Home")) return;
            _onlineResults.AddRange(images);
            _onlinePage = 1;
            OnlineList.ItemsSource = _onlineResults.ToList();
            MoreButton.Visibility = Visibility.Visible;
            MoreButton.IsEnabled = true;
            EmptyHint.Text = images.Count == 0 ? "No results. Try another search, such as mountains or forest." : "";
            EmptyHint.Visibility = images.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            StatusText.Text = $"Found {images.Count} online images. Select one to preview and save.";
        }
        catch (Exception ex)
        {
            if (generation != _searchGeneration) return;
            EmptyHint.Text = "Unable to load online wallpapers. Check your connection and try again.";
            EmptyHint.Visibility = Visibility.Visible;
            StatusText.Text = "Online search failed: " + ex.Message;
        }
    }

    private void Online_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedOnline = OnlineList.SelectedItem as OnlineWallpaper;
        if (_selectedOnline is null) return;
        try
        {
            PreviewImage.Source = new BitmapImage(new Uri(_selectedOnline.PreviewUrl));
        }
        catch (Exception ex)
        {
            PreviewImage.Source = null;
            StatusText.Text = "Preview unavailable: " + ex.Message;
        }
        SelectedText.Text = _selectedOnline.Title;
        CreditText.Text = $"Licence: {_selectedOnline.License}\nCreator: {_selectedOnline.Artist}";
        DownloadButton.IsEnabled = true;
        SourceButton.IsEnabled = true;
    }

    private async void Download_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedOnline is null) return;
        DownloadButton.IsEnabled = false;
        try
        {
            StatusText.Text = "Downloading original image...";
            var saved = new Uri(_selectedOnline.DownloadUrl).Host.EndsWith(".wikimedia.org", StringComparison.OrdinalIgnoreCase)
                ? await _onlineService.DownloadAsync(_selectedOnline)
                : await _openverse.DownloadAsync(_selectedOnline);
            StatusText.Text = "Saved to My Garden: " + Path.GetFileName(saved) + ". Review source licence for attribution.";
            MessageBox.Show("Image saved to My Garden. You can apply it there.\nRemember to check the source licence for attribution requirements.",
                "Download complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Download failed", MessageBoxButton.OK, MessageBoxImage.Error); }
        finally { DownloadButton.IsEnabled = _selectedOnline is not null; }
    }

    private async void More_Click(object sender, RoutedEventArgs e)
    {
        if (_onlinePage < 1) return;
        MoreButton.IsEnabled = false;
        try
        {
            var next = await _openverse.SearchAsync(_currentQuery, _onlinePage + 1);
            var seen = _onlineResults.Select(x => x.DownloadUrl).ToHashSet(StringComparer.OrdinalIgnoreCase);
            _onlineResults.AddRange(next.Where(x => seen.Add(x.DownloadUrl)));
            _onlinePage++;
            OnlineList.ItemsSource = _onlineResults.ToList();
            StatusText.Text = $"Showing {_onlineResults.Count} wallpapers from Wikimedia Commons and Openverse.";
        }
        catch (Exception ex) { StatusText.Text = "Could not load more: " + ex.Message; }
        finally { MoreButton.IsEnabled = true; }
    }

    private void Source_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedOnline is not null)
            Process.Start(new ProcessStartInfo(_selectedOnline.PageUrl) { UseShellExecute = true });
    }

    private void Search_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter && (_page is "Discover" or "Home"))
        {
            e.Handled = true;
            _ = SearchOnlineAsync(SearchBox.Text);
        }
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
        if (GalleryList is not null && _page == "Garden") FilterGallery();
    }

    private async void Home_Click(object sender, RoutedEventArgs e)
    {
        ShowHome();
        await SearchOnlineAsync("featured pictures landscape");
    }
    private async void Discover_Click(object sender, RoutedEventArgs e) => await ShowDiscoverAsync();
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
