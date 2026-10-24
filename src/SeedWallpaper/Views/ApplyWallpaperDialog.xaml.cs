using System.Windows;
using System.Windows.Controls;

namespace SeedWallpaper.Views;
public partial class ApplyWallpaperDialog : Window
{
    public bool ApplyDesktop => DesktopRadio.IsChecked == true || BothRadio.IsChecked == true;
    public bool OpenLockSettings => LockRadio.IsChecked == true || BothRadio.IsChecked == true;
    public string Fit => (FitBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Fill";

    public ApplyWallpaperDialog() => InitializeComponent();
    private void Continue_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
