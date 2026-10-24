using System.Windows;
namespace SeedWallpaper.Views;
public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();
    private void ShowPage(string title, string description) { PageTitle.Text = title; PageDescription.Text = description; }
    private void Home_Click(object sender, RoutedEventArgs e) => ShowPage("Welcome to Seed", "A calm place for wallpapers. Discover, create and apply beautiful images.");
    private void Discover_Click(object sender, RoutedEventArgs e) => ShowPage("Discover", "Online wallpaper search and licensed collections will appear here in Phase 2.");
    private void Studio_Click(object sender, RoutedEventArgs e) => ShowPage("Seed Studio", "AI generation and reference-image editing will be added in later phases.");
    private void Garden_Click(object sender, RoutedEventArgs e) => ShowPage("My Garden", "Your saved and imported wallpapers will live here.");
    private void Settings_Click(object sender, RoutedEventArgs e) => ShowPage("Settings", "Manage providers, storage, appearance and Windows integration here.");
}
