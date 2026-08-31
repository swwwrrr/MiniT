using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace MiniT.UI.Pages;

public sealed partial class UuidGeneratorPage : Page
{
    private readonly List<string> _uuids = new();

    public UuidGeneratorPage()
    {
        InitializeComponent();
        GenerateUuids(1);
    }

    private void OnGenerate(object sender, RoutedEventArgs e) =>
        GenerateUuids((int)CountBox.Value);

    private void OnCopyAll(object sender, RoutedEventArgs e) =>
        SetClipboard(string.Join("\n", _uuids));

    private void OnClear(object sender, RoutedEventArgs e)
    {
        _uuids.Clear();
        RebuildList();
    }

    private void GenerateUuids(int count)
    {
        _uuids.Clear();
        for (int i = 0; i < count; i++)
            _uuids.Add(FormatGuid(Guid.NewGuid()));
        RebuildList();
    }

    private void RebuildList()
    {
        // Build rows in code to avoid DataTemplate binding issues
        var items = _uuids.Select(uuid =>
        {
            var tb = new TextBlock
            {
                Text = uuid, FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
                VerticalAlignment = VerticalAlignment.Center, IsTextSelectionEnabled = true,
                TextWrapping = TextWrapping.NoWrap
            };
            var btn = new Button { Content = "Copy", Margin = new Thickness(8, 0, 0, 0), Tag = uuid };
            btn.Click += (_, _) => SetClipboard((string)btn.Tag!);

            var row = new Grid { Padding = new Thickness(12, 8, 12, 8) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(tb, 0); row.Children.Add(tb);
            Grid.SetColumn(btn, 1); row.Children.Add(btn);

            return (UIElement)new Border
            {
                Child = row, CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(1),
                BorderBrush = (Microsoft.UI.Xaml.Media.Brush)
                    Application.Current.Resources["CardStrokeColorDefaultBrush"],
                Background = (Microsoft.UI.Xaml.Media.Brush)
                    Application.Current.Resources["CardBackgroundFillColorDefaultBrush"]
            };
        }).ToList();

        ResultsList.ItemsSource = items;
    }

    private string FormatGuid(Guid g)
    {
        if (FormatUpper.IsChecked == true) return g.ToString().ToUpperInvariant();
        if (FormatBraces.IsChecked == true) return $"{{{g}}}";
        return g.ToString();
    }

    private static void SetClipboard(string text)
    {
        var dp = new DataPackage();
        dp.SetText(text);
        Clipboard.SetContent(dp);
    }
}
