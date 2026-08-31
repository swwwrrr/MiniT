using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MiniT.Core.Models;
using MiniT.UI.Helpers;
using MiniT.UI.ViewModels;

namespace MiniT.UI.Pages;

public sealed partial class SettingsPage : Page
{
    private MainViewModel? _vm;
    public event Action<AppTheme>? ThemeChanged;

    /// <summary>Raised when the user wants to leave the Settings page (Back button or Escape).</summary>
    public event EventHandler? BackRequested;

    public SettingsPage()
    {
        InitializeComponent();
        DataPathText.Text = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MiniT");

        AttachPress(BackButton);
        AttachPress(OpenFolderButton, pressScale: 0.98);
    }

    private void AttachPress(FrameworkElement el, double pressScale = 0.97)
    {
        el.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
        var scale = new ScaleTransform();
        el.RenderTransform = scale;
        Motion.AttachPressScale(el, scale, pressScale, 1.0);
    }

    private void OnBack(object sender, RoutedEventArgs e) => BackRequested?.Invoke(this, EventArgs.Empty);

    public void SetViewModel(MainViewModel vm)
    {
        _vm = vm;
        ThemeCombo.SelectedIndex = (int)vm.Settings.Theme;
    }

    private void OnThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_vm == null || ThemeCombo.SelectedIndex < 0) return;
        _vm.Settings.Theme = (AppTheme)ThemeCombo.SelectedIndex;
        _vm.SaveSettings();
        ThemeChanged?.Invoke(_vm.Settings.Theme);
    }

    private void OnOpenDataFolder(object sender, RoutedEventArgs e)
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MiniT");
        System.Diagnostics.Process.Start("explorer.exe", path);
    }
}
