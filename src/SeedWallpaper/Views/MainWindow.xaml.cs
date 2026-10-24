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
    private readonly AiWallpaperService _aiService = new();
    private readonly List<OnlineWallpaper> _onlineResults = new();
    private string _currentQuery = "landscape";
    private int _onlinePage = 0;
    private string? _studioGeneratedPath;
    private string? _studioReferencePath;
    private bool _perchanceInitialized;
    private bool _googleInitialized;
    private int _googleNavigationVersion;
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

    private static async Task<bool> CanReachAsync(string host)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
            using var response = await client.GetAsync(host, HttpCompletionOption.ResponseHeadersRead);
            return true; // Even HTTP errors prove the remote server is reachable.
        }
        catch (HttpRequestException) { return false; }
        catch (TaskCanceledException) { return false; }
    }

    private void ShowOfflineIfCurrent(string page)
    {
        if (_page == page) OfflinePanel.Visibility = Visibility.Visible;
    }

    private async void OfflineRetry_Click(object sender, RoutedEventArgs e)
    {
        OfflinePanel.Visibility = Visibility.Collapsed;
        if (_page == "Home") await SearchOnlineAsync("featured pictures landscape");
        else if (_page == "Discover") await NavigateGoogleAsync(SearchBox.Text);
        else if (_page == "Studio")
        {
            if (!await CanReachAsync("https://perchance.org/03tqzy7x1l"))
            {
                ShowOfflineIfCurrent("Studio");
                return;
            }
            if (PerchanceBrowser.CoreWebView2 is null) Studio_Click(sender, e);
            else PerchanceBrowser.Reload();
        }
    }

    private void OfflineSettings_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo("ms-settings:network-status") { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Network settings"); }
    }

    private void WindowMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void WindowMaximize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void WindowClose_Click(object sender, RoutedEventArgs e) => Close();

    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) WindowMaximize_Click(sender, e);
        else if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed) DragMove();
    }

    private void SetPage(string title, string description, bool showLibrary, Button active)
    {
        OfflinePanel.Visibility = Visibility.Collapsed;
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
        StudioPanel.Visibility = Visibility.Collapsed;
        PerchanceStudioPanel.Visibility = _page == "Studio" ? Visibility.Visible : Visibility.Collapsed;
        GoogleDiscoverPanel.Visibility = _page == "Discover" ? Visibility.Visible : Visibility.Collapsed;
        TopSearchBar.Visibility = _page == "Studio" ? Visibility.Collapsed : Visibility.Visible;
        GalleryScroll.Visibility = _page is "Studio" or "Discover" ? Visibility.Collapsed : Visibility.Visible;
        WallpaperDetailsPanel.Visibility = _page == "Garden" ? Visibility.Visible : Visibility.Collapsed;
        DetailsColumn.Width = _page == "Garden" ? new GridLength(300) : new GridLength(0);
        GalleryScroll.Margin = _page == "Home" ? new Thickness(0) : new Thickness(0, 0, 20, 0);
        SearchBox.Visibility = _page == "Studio" ? Visibility.Collapsed : Visibility.Visible;
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
        SetPage("Discover", "Search Google Images for wallpapers.", false, DiscoverButton);
        await NavigateGoogleAsync(string.IsNullOrWhiteSpace(SearchBox.Text) ? "4k desktop wallpapers" : SearchBox.Text);
    }

    private async Task NavigateGoogleAsync(string query)
    {
        if (!await CanReachAsync("https://www.google.com"))
        {
            ShowOfflineIfCurrent("Discover");
            return;
        }
        if (_page != "Discover") return;
        OfflinePanel.Visibility = Visibility.Collapsed;
        var url = "https://www.google.com/search?tbm=isch&q=" +
            Uri.EscapeDataString(string.IsNullOrWhiteSpace(query) ? "4k desktop wallpapers" : query.Trim());
        try
        {
            if (!_googleInitialized)
            {
                await GoogleDiscoverBrowser.EnsureCoreWebView2Async();
                GoogleDiscoverBrowser.CoreWebView2.Settings.AreDevToolsEnabled = false;
                // Register before navigation, so Google's header is hidden before
                // the first paint and again on every reload or result navigation.
                await GoogleDiscoverBrowser.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync("""
                    (() => {
                      const css = `
                        #searchform, #gb, #top_nav, #hdtb, #appbar,
                        header[role="banner"], [role="search"],
                        [role="navigation"][aria-label="Search"],
                        [role="navigation"][aria-label="Search options"],
                        a[href*="udm=50"], a[href*="aep=11"] {
                          display: none !important;
                        }
                        html, body { padding-top: 0 !important; }
                      `;
                      const apply = () => {
                        if (!document.documentElement) return;
                        let style = document.getElementById('seed-discover-cleanup');
                        if (!style) {
                          style = document.createElement('style');
                          style.id = 'seed-discover-cleanup';
                          (document.head || document.documentElement).appendChild(style);
                        }
                        if (style.textContent !== css) style.textContent = css;
                        if (!document.body) return;
                        for (const node of document.querySelectorAll('a, [role="tab"]')) {
                          if ((node.textContent || '').trim().toLowerCase() === 'ai mode') {
                            const item = node.closest('li') || node;
                            if (item.style.display !== 'none') item.style.display = 'none';
                          }
                        }
                      };
                      apply();
                      if (document.documentElement)
                        new MutationObserver(apply).observe(document.documentElement, {childList:true, subtree:true});
                    })();
                    """);
                _googleInitialized = true;
            }
            GoogleDiscoverBrowser.Visibility = Visibility.Hidden;
            GoogleLoadingText.Visibility = Visibility.Visible;
            GoogleLoadingText.Text = "Finding wallpapers…";
            GoogleDiscoverBrowser.CoreWebView2.Navigate(url);
        }
        catch (Exception ex)
        {
            GoogleLoadingText.Text = "Could not load Google Images: " + ex.Message +
                ". Use Open in browser instead.";
            GoogleLoadingText.Visibility = Visibility.Visible;
        }
    }

    private void GoogleNavigationStarting(object sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationStartingEventArgs e)
    {
        ++_googleNavigationVersion;
        GoogleDiscoverBrowser.Visibility = Visibility.Hidden;
        GoogleLoadingText.Text = "Preparing wallpaper results…";
        GoogleLoadingText.Visibility = Visibility.Visible;
    }

    private async void GoogleNavigationCompleted(object sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e)
    {
        var navigationVersion = _googleNavigationVersion;
        if (!e.IsSuccess)
        {
            GoogleLoadingText.Text = "Google Images could not load here. Check your connection.";
            ShowOfflineIfCurrent("Discover");
            GoogleLoadingText.Visibility = Visibility.Visible;
            return;
        }

        const string cleanupScript = """
            (() => {
              const css = `
                #searchform, #gb, #top_nav, #hdtb, #appbar,
                header[role="banner"], [role="search"],
                [role="navigation"][aria-label="Search"],
                [role="navigation"][aria-label="Search options"],
                div[role="navigation"]:has(a[href*="udm=50"]),
                a[href*="udm=50"], a[href*="aep=11"] {
                  display: none !important;
                }
                html, body { padding-top: 0 !important; }
              `;
              const apply = () => {
                if (!document.documentElement) return;
                let style = document.getElementById('seed-discover-cleanup');
                if (!style) {
                  style = document.createElement('style');
                  style.id = 'seed-discover-cleanup';
                  (document.head || document.documentElement).appendChild(style);
                }
                if (style.textContent !== css) style.textContent = css;
                if (!document.body) return;
                for (const node of document.querySelectorAll('a, [role="tab"]')) {
                  if ((node.textContent || '').trim().toLowerCase() === 'ai mode') {
                    const item = node.closest('li') || node;
                    if (item.style.display !== 'none') item.style.display = 'none';
                  }
                }
              };
              apply();
              const start = () => {
                if (!document.documentElement) return;
                new MutationObserver(apply).observe(document.documentElement, {childList:true, subtree:true});
                apply();
              };
              if (document.documentElement) start();
              else document.addEventListener('DOMContentLoaded', start, {once:true});
            })();
            """;
        try
        {
            if (GoogleDiscoverBrowser.CoreWebView2 is not null)
            {
                await GoogleDiscoverBrowser.CoreWebView2.ExecuteScriptAsync(cleanupScript);
                // Let the image grid and cleanup observer settle before revealing the browser.
                await Task.Delay(350);
                if (navigationVersion != _googleNavigationVersion) return;
                GoogleDiscoverBrowser.Visibility = Visibility.Visible;
                GoogleLoadingText.Visibility = Visibility.Collapsed;
            }
        }
        catch (Exception)
        {
            if (navigationVersion != _googleNavigationVersion) return;
            GoogleLoadingText.Text = "Could not prepare the image results. Try another search.";
            GoogleLoadingText.Visibility = Visibility.Visible;
        }
    }

    private void GoogleBack_Click(object sender, RoutedEventArgs e)
    {
        if (GoogleDiscoverBrowser.CanGoBack) GoogleDiscoverBrowser.GoBack();
    }

    private void GoogleReload_Click(object sender, RoutedEventArgs e)
    {
        if (GoogleDiscoverBrowser.CoreWebView2 is not null) GoogleDiscoverBrowser.Reload();
        else _ = NavigateGoogleAsync(SearchBox.Text);
    }

    private void GoogleOpen_Click(object sender, RoutedEventArgs e)
    {
        var url = GoogleDiscoverBrowser.Source?.AbsoluteUri ??
            "https://www.google.com/search?tbm=isch&q=4k%20desktop%20wallpapers";
        if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return;
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private async Task SearchOnlineAsync(string term)
    {
        if (!await CanReachAsync("https://commons.wikimedia.org"))
        {
            ShowOfflineIfCurrent("Home");
            return;
        }
        if (_page == "Home") OfflinePanel.Visibility = Visibility.Collapsed;
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

    private void GalleryList_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        if (_page is not ("Home" or "Garden")) return;
        // ListBox has an internal ScrollViewer which otherwise swallows wheel
        // events even when the collection is laid out inside GalleryScroll.
        e.Handled = true;
        double step = System.Windows.SystemParameters.WheelScrollLines * 22.0;
        if (step <= 0 || step > 500) step = 66;
        GalleryScroll.ScrollToVerticalOffset(
            Math.Max(0, GalleryScroll.VerticalOffset - e.Delta / 120.0 * step));
    }

    private void Search_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter && (_page is "Discover" or "Home"))
        {
            e.Handled = true;
            if (_page == "Discover") _ = NavigateGoogleAsync(SearchBox.Text);
            else _ = SearchOnlineAsync(SearchBox.Text);
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
    private async void Studio_Click(object sender, RoutedEventArgs e)
    {
        SetPage("Seed Studio", "Generate images with Perchance.", false, StudioButton);
        if (!await CanReachAsync("https://perchance.org/03tqzy7x1l"))
        {
            ShowOfflineIfCurrent("Studio");
            return;
        }
        if (_perchanceInitialized) { PerchanceBrowser.Reload(); return; }
        try
        {
            await PerchanceBrowser.EnsureCoreWebView2Async();
            PerchanceBrowser.CoreWebView2.Settings.AreDevToolsEnabled = false;
            PerchanceBrowser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            PerchanceBrowser.Source = new Uri("https://perchance.org/03tqzy7x1l");
            _perchanceInitialized = true;
        }
        catch (Exception ex)
        {
            PerchanceLoadingText.Text = "Could not start the embedded browser: " + ex.Message +
                ". Install Microsoft Edge WebView2 Runtime, or use Open in browser.";
            PerchanceLoadingText.Visibility = Visibility.Visible;
        }
    }

    private void PerchanceNavigationCompleted(object sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e)
    {
        PerchanceLoadingText.Visibility = e.IsSuccess ? Visibility.Collapsed : Visibility.Visible;
        if (!e.IsSuccess) ShowOfflineIfCurrent("Studio");
        if (!e.IsSuccess) PerchanceLoadingText.Text = "Perchance could not load. Check your internet connection or open it in your browser.";
    }

    private void PerchanceReload_Click(object sender, RoutedEventArgs e)
    {
        if (PerchanceBrowser.CoreWebView2 is not null) PerchanceBrowser.Reload();
        else Studio_Click(sender, e);
    }

    private void PerchanceOpen_Click(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo("https://perchance.org/03tqzy7x1l") { UseShellExecute = true });

    private void StudioModel_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (StudioCustomModelPanel is null || StudioModelDescription is null) return;
        var model = (StudioModel.SelectedItem as ComboBoxItem)?.Tag as string;
        StudioCustomModelPanel.Visibility = model == "custom" ? Visibility.Visible : Visibility.Collapsed;
        StudioModelDescription.Text = model switch
        {
            "flux" => "Flux via Pollinations. Availability and cost depend on your API account.",
            "zimage" => "Z-Image via Pollinations. Availability and cost depend on your API account.",
            _ => "Enter the exact model ID from your Pollinations account. Only models supported by that API can generate images."
        };
    }

    private void StudioMode_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (StudioReferencePanel is null || StudioGenerate is null || StudioStatus is null) return;
        bool imageMode = (StudioMode.SelectedItem as ComboBoxItem)?.Tag as string == "i2i";
        StudioReferencePanel.Visibility = imageMode ? Visibility.Visible : Visibility.Collapsed;
        StudioGenerate.IsEnabled = !imageMode;
        StudioStatus.Text = imageMode
            ? "Image-to-image needs a compatible backend. Reference image selection is available, but generation is not connected yet."
            : "Ready to generate with a Pollinations API key.";
    }

    private void StudioReference_Click(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg" };
        if (picker.ShowDialog() != true) return;
        _studioReferencePath = picker.FileName;
        StudioReferenceLabel.Text = Path.GetFileName(_studioReferencePath);
    }

    private void StudioEnhance_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(StudioPrompt.Text)) return;
        const string suffix = ", cinematic composition, natural lighting, balanced colours, detailed desktop wallpaper";
        if (!StudioPrompt.Text.Contains("cinematic composition", StringComparison.OrdinalIgnoreCase))
            StudioPrompt.Text = StudioPrompt.Text.Trim().TrimEnd(',') + suffix;
        StudioStatus.Text = "Added descriptive prompt details (local template, not AI rewriting).";
    }

    private void StudioRandomSeed_Click(object sender, RoutedEventArgs e) =>
        StudioSeed.Text = Random.Shared.Next(1, int.MaxValue).ToString();

    private void StudioOverlay_Changed(object sender, RoutedEventArgs e)
    {
        if (StudioDesktopOverlay is not null)
            StudioDesktopOverlay.Visibility = StudioOverlayToggle.IsChecked == true
                ? Visibility.Visible : Visibility.Collapsed;
    }

    private void StudioApply_Click(object sender, RoutedEventArgs e)
    {
        if (_studioGeneratedPath is null || !File.Exists(_studioGeneratedPath)) return;
        try
        {
            DesktopWallpaperService.Apply(_studioGeneratedPath, "Fill");
            StudioStatus.Text = "Applied generated image as Windows desktop wallpaper.";
        }
        catch (Exception ex) { StudioStatus.Text = "Could not apply wallpaper: " + ex.Message; }
    }

    private async void StudioGenerate_Click(object sender, RoutedEventArgs e)
    {
        var prompt = StudioPrompt.Text.Trim();
        var key = StudioKey.Password;
        if (string.IsNullOrWhiteSpace(prompt) || string.IsNullOrWhiteSpace(key))
        {
            StudioStatus.Text = "Enter a wallpaper description and your Pollinations API key.";
            return;
        }
        StudioGenerate.IsEnabled = false;
        StudioOpenGarden.IsEnabled = false;
        StudioApply.IsEnabled = false;
        StudioStatus.Text = "Generating wallpaper... This can take a minute.";
        try
        {
            var model = (StudioModel.SelectedItem as ComboBoxItem)?.Tag as string ?? "flux";
            if (model == "custom")
            {
                model = StudioCustomModel.Text.Trim();
                if (string.IsNullOrWhiteSpace(model) || model.Length > 80 ||
                    !System.Text.RegularExpressions.Regex.IsMatch(model, "^[a-zA-Z0-9._-]+$"))
                    throw new ArgumentException("Enter a valid Pollinations model ID (letters, digits, dots, underscores or hyphens).");
            }
            var size = ((StudioResolution.SelectedItem as ComboBoxItem)?.Tag as string ?? "1536x1024").Split('x');
            var path = await _aiService.GenerateAsync(prompt, model, int.Parse(size[0]), int.Parse(size[1]), key);
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            StudioResult.Source = image;
            _studioGeneratedPath = path;
            StudioPreviewHint.Visibility = Visibility.Collapsed;
            StudioClock.Text = DateTime.Now.ToString("h:mm tt");
            StudioApply.IsEnabled = true;
            StudioStatus.Text = "Wallpaper generated and saved to My Garden.";
            StudioOpenGarden.IsEnabled = true;
        }
        catch (Exception ex)
        {
            StudioStatus.Text = "Generation failed: " + ex.Message;
        }
        finally { StudioGenerate.IsEnabled = (StudioMode.SelectedItem as ComboBoxItem)?.Tag as string != "i2i"; }
    }

    private void StudioOpenGarden_Click(object sender, RoutedEventArgs e) => ShowGarden();
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
