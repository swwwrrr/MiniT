using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;

namespace MiniT.UI.Pages;

/// <summary>
/// Runs an "Online" tool inside MiniT itself via WebView2, instead of handing
/// off to the system browser — so online tools open as a tab in the app just
/// like local tools do. One instance per tab (created fresh per launch).
/// </summary>
public sealed partial class WebToolPage : Page
{
    private string _url = "";

    public WebToolPage() => InitializeComponent();

    public async void Navigate(string url)
    {
        _url = url;
        AddressText.Text = url;
        SetStatus(true, "Loading…");

        try
        {
            await Browser.EnsureCoreWebView2Async();
        }
        catch (Exception ex)
        {
            // Most commonly: the WebView2 Runtime isn't installed on this
            // machine. Surface something actionable instead of a blank tab.
            SetStatus(true, "Couldn't load embedded browser.\n" + ex.Message);
            return;
        }

        Browser.CoreWebView2.NavigationStarting += (_, _) => SetStatus(true, "Loading…");
        Browser.CoreWebView2.NavigationCompleted += (_, args) =>
        {
            SetStatus(!args.IsSuccess, args.IsSuccess
                ? ""
                : "This page couldn't be loaded here.\nSome sites block being embedded.");
            UpdateAddress();
            UpdateNavButtons();
        };
        Browser.CoreWebView2.SourceChanged += (_, _) => UpdateAddress();
        Browser.CoreWebView2.HistoryChanged += (_, _) => UpdateNavButtons();

        Browser.CoreWebView2.Navigate(url);
    }

    private void SetStatus(bool visible, string message)
    {
        StatusPanel.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        LoadingRing.IsActive = visible && message == "Loading…";
        LoadingRing.Visibility = LoadingRing.IsActive ? Visibility.Visible : Visibility.Collapsed;
        StatusText.Text = message;
    }

    private void UpdateAddress()
    {
        if (Browser.CoreWebView2?.Source is string src && !string.IsNullOrEmpty(src))
            AddressText.Text = src;
    }

    private void UpdateNavButtons()
    {
        BackButton.IsEnabled = Browser.CoreWebView2?.CanGoBack ?? false;
        ForwardButton.IsEnabled = Browser.CoreWebView2?.CanGoForward ?? false;
    }

    private void OnBack(object sender, RoutedEventArgs e)
    {
        if (Browser.CoreWebView2?.CanGoBack == true) Browser.CoreWebView2.GoBack();
    }

    private void OnForward(object sender, RoutedEventArgs e)
    {
        if (Browser.CoreWebView2?.CanGoForward == true) Browser.CoreWebView2.GoForward();
    }

    private void OnReload(object sender, RoutedEventArgs e)
    {
        if (Browser.CoreWebView2 != null) Browser.CoreWebView2.Reload();
        else Navigate(_url);
    }

    private void OnOpenExternal(object sender, RoutedEventArgs e)
    {
        var target = Browser.CoreWebView2?.Source ?? _url;
        if (!string.IsNullOrEmpty(target))
            _ = Windows.System.Launcher.LaunchUriAsync(new Uri(target));
    }
}
