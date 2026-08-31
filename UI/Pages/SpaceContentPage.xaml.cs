using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MiniT.Core.Models;
using MiniT.UI.Controls;
using MiniT.UI.Helpers;
using MiniT.UI.ViewModels;

namespace MiniT.UI.Pages;

public sealed partial class SpaceContentPage : Page
{
    private MainViewModel? _vm;
    private SpaceViewModel? _space;

    /// <summary>
    /// Raised when a tool card is clicked. MainWindow owns the tab bar / content
    /// frame, so it must be the one to react — calling the VM command directly
    /// from here (as before) left the tab silently created but never shown.
    /// </summary>
    public event EventHandler<ToolDefinition>? ToolLaunched;

    public SpaceContentPage() => InitializeComponent();

    public void Load(SpaceViewModel space, MainViewModel vm)
    {
        _space = space;
        _vm = vm;
        Rebuild();
    }

    public void Rebuild()
    {
        if (_space == null || _vm == null) return;
        _space.Refresh();
        RootStack.Children.Clear();

        if (_space.FavoriteTools.Count > 0)
            RootStack.Children.Add(MakeSection("Favorites", _space.FavoriteTools, "\uE734"));

        foreach (var folder in _space.Folders)
        {
            if (folder.Tools.Count == 0) continue;
            RootStack.Children.Add(MakeSection(folder.Name, folder.Tools, "\uE8B7"));
        }

        if (_space.UnfolderedTools.Count > 0)
            RootStack.Children.Add(MakeCardRow(_space.UnfolderedTools));

        if (RootStack.Children.Count == 0)
            RootStack.Children.Add(MakeEmptyState(_space.IsSystem));
    }

    private StackPanel MakeEmptyState(bool isSystem = false)
    {
        var panel = new StackPanel
        {
            Spacing = 10,
            Margin = new Thickness(0, 72, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center
        };

        panel.Children.Add(new Border
        {
            Width = 56, Height = 56, CornerRadius = new CornerRadius(16),
            HorizontalAlignment = HorizontalAlignment.Center,
            Background = (Brush)Application.Current.Resources["AppAccentSoftBrush"],
            Child = new FontIcon
            {
                Glyph = "\uE71D", FontSize = 22,
                Foreground = (Brush)Application.Current.Resources["AppAccentBrush"]
            }
        });

        panel.Children.Add(new TextBlock
        {
            Text = isSystem ? "No tools installed yet" : "This space is empty",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 15,
            HorizontalAlignment = HorizontalAlignment.Center
        });

        panel.Children.Add(new TextBlock
        {
            Text = isSystem
                ? "Local and online tools you add will show up here."
                : "Open \"All Tools\" and use the + button on a tool to add it here, or press Ctrl+K to search.",
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        });

        return panel;
    }

    private StackPanel MakeSection(string title, IEnumerable<ToolDefinition> tools, string glyph)
    {
        var toolList = tools.ToList();
        var sp = new StackPanel { Spacing = 14 };

        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        header.Children.Add(new FontIcon
        {
            Glyph = glyph, FontSize = 13,
            Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"]
        });
        header.Children.Add(new TextBlock
        {
            Text = title,
            Style = (Style)Application.Current.Resources["SectionHeaderTextStyle"]
        });
        header.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(7, 1, 7, 1),
            VerticalAlignment = VerticalAlignment.Center,
            Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"],
            Child = new TextBlock
            {
                Text = toolList.Count.ToString(),
                FontSize = 11,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"]
            }
        });

        sp.Children.Add(header);
        sp.Children.Add(MakeCardRow(toolList));
        return sp;
    }

    // Custom lightweight wrap panel (avoids the flaky CommunityToolkit WrapLayout NuGet package)
    private SimpleWrapPanel MakeCardRow(IEnumerable<ToolDefinition> tools)
    {
        var panel = new SimpleWrapPanel
        {
            HorizontalSpacing = 12,
            VerticalSpacing = 12
        };
        int i = 0;
        foreach (var tool in tools)
        {
            var card = MakeCard(tool);
            panel.Children.Add(card);
            // Stagger each card in a little after the previous one, capped so a
            // large grid doesn't take forever to finish settling.
            card.PlayEntrance(Math.Min(i * 18, 180));
            i++;
        }
        return panel;
    }

    private ToolCard MakeCard(ToolDefinition tool)
    {
        var card = new ToolCard
        {
            Tool = tool,
            IsFavorite = _space?.FavoriteTools.Any(f => f.Id == tool.Id) ?? false,
            // Favoriting belongs to a real space, not to the whole registry, so
            // there's nothing meaningful for the star to do in "All Tools".
            AllowFavorite = !(_space?.IsSystem ?? false)
        };
        card.LaunchRequested += (_, t) => ToolLaunched?.Invoke(this, t);
        card.FavoriteToggled += (_, t) =>
        {
            _vm?.ToggleFavoriteCommand.Execute(t);
            Rebuild();
        };
        card.MoreRequested += (_, t) =>
        {
            if (_vm == null) return;
            AddToSpaceMenu.Show(_vm, t, card, onChanged: Rebuild);
        };
        return card;
    }
}
