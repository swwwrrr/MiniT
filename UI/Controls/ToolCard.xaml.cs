using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using MiniT.Core.Models;
using MiniT.UI.Helpers;

namespace MiniT.UI.Controls;

public sealed partial class ToolCard : UserControl
{
    public event EventHandler<ToolDefinition>? LaunchRequested;
    public event EventHandler<ToolDefinition>? FavoriteToggled;

    /// <summary>Raised when the "…" button is clicked, asking whoever hosts this
    /// card to show the "Add to space" menu anchored on it.</summary>
    public event EventHandler<ToolDefinition>? MoreRequested;

    public static readonly DependencyProperty ToolProperty =
        DependencyProperty.Register(nameof(Tool), typeof(ToolDefinition),
            typeof(ToolCard), new PropertyMetadata(null, OnToolChanged));

    public static readonly DependencyProperty IsFavoriteProperty =
        DependencyProperty.Register(nameof(IsFavorite), typeof(bool),
            typeof(ToolCard), new PropertyMetadata(false, OnFavChanged));

    /// <summary>Whether the favorite star is shown at all. Off for cards inside
    /// the system "All Tools" space, where "favorite" doesn't map to anything —
    /// favorites belong to a real space, not to the whole registry.</summary>
    public static readonly DependencyProperty AllowFavoriteProperty =
        DependencyProperty.Register(nameof(AllowFavorite), typeof(bool),
            typeof(ToolCard), new PropertyMetadata(true, OnAllowFavoriteChanged));

    public ToolDefinition? Tool
    {
        get => (ToolDefinition?)GetValue(ToolProperty);
        set => SetValue(ToolProperty, value);
    }

    public bool IsFavorite
    {
        get => (bool)GetValue(IsFavoriteProperty);
        set => SetValue(IsFavoriteProperty, value);
    }

    public bool AllowFavorite
    {
        get => (bool)GetValue(AllowFavoriteProperty);
        set => SetValue(AllowFavoriteProperty, value);
    }

    private bool _favClicked;
    private bool _moreClicked;
    private bool _isPointerOver;

    public ToolCard()
    {
        InitializeComponent();
        UpdateColors();
        Tapped += (_, _) =>
        {
            if (_favClicked) { _favClicked = false; return; }
            if (_moreClicked) { _moreClicked = false; return; }
            if (Tool != null) LaunchRequested?.Invoke(this, Tool);
        };
        CardBorder.PointerPressed += (_, _) => Motion.AnimateScale(CardScale, 0.965, 70);
        CardBorder.PointerReleased += (_, _) => Motion.AnimateScale(CardScale, _isPointerOver ? 1.03 : 1.0, 100);
        CardBorder.PointerCanceled += (_, _) => Motion.AnimateScale(CardScale, _isPointerOver ? 1.03 : 1.0, 100);
        CardBorder.PointerCaptureLost += (_, _) => Motion.AnimateScale(CardScale, _isPointerOver ? 1.03 : 1.0, 100);
        // Colors below are read once from Application.Current.Resources rather
        // than bound via ThemeResource, so they need to be re-applied explicitly
        // whenever the app's actual theme changes (e.g. switching Light/Dark in
        // Settings while a Space full of cards is already on screen).
        ActualThemeChanged += (_, _) => UpdateColors();
    }

    /// <summary>Fades and gently rises the card into place. Call with an increasing
    /// delay across a grid so cards settle in one after another rather than all
    /// popping in at once.</summary>
    public void PlayEntrance(int delayMs = 0) => Motion.EntranceFadeRise(CardBorder, CardTranslate, delayMs);

    private void UpdateColors()
    {
        // Apply theme-aware colors in code (safer than ThemeResource in UserControl XAML)
        var cardBg      = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"];
        var cardBorder  = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"];
        var secondary   = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
        var attentionBg = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"];

        CardBorder.Background    = cardBg;
        CardBorder.BorderBrush   = cardBorder;
        CardBorder.BorderThickness = new Thickness(1);

        if (CardBorder.Child is Grid g)
        {
            // InitialsText border (row 0): one flat, low-opacity accent tint
            // behind a full-opacity, same-hue initials label. Every card uses
            // the same hue — the "colored icon on a tinted chip" pattern reads
            // as considered when it's one disciplined color, not a different
            // random hue per card fighting for attention.
            if (g.Children[0] is Border badge)
            {
                badge.Background = (Brush)Application.Current.Resources["AppAccentSoftBrush"];
                if (badge.Child is TextBlock tb)
                    tb.Foreground = (Brush)Application.Current.Resources["AppAccentBrush"];
            }
            // TypeLabel border (row 2 grid)
            if (g.Children[2] is Grid footer)
            {
                if (footer.Children[0] is Border typeBorder)
                {
                    typeBorder.Background = attentionBg;
                    if (typeBorder.Child is TextBlock tl)
                        tl.Foreground = secondary;
                }
            }
        }

        FavIcon.Foreground = IsFavorite
            ? new SolidColorBrush(Color.FromArgb(255, 0xD9, 0x77, 0x06))
            : secondary;
    }

    private static void OnToolChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ToolCard card || e.NewValue is not ToolDefinition t) return;
        card.NameText.Text = t.Name;
        card.TypeLabel.Text = t.Type.ToString();
        card.InitialsText.Text = string.Concat(
            t.Name.Split(' ').Select(w => w.Length > 0 ? w[0].ToString() : "").Take(2));
        card.UpdateColors();
    }

    private static void OnFavChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ToolCard card) return;
        bool fav = (bool)e.NewValue;
        card.FavIcon.Glyph = fav ? "\uE735" : "\uE734"; // filled vs. outline star
        card.UpdateColors();
    }

    private void OnFavoriteClick(object sender, RoutedEventArgs e)
    {
        _favClicked = true;
        if (Tool != null) FavoriteToggled?.Invoke(this, Tool);
    }

    private void OnMoreClick(object sender, RoutedEventArgs e)
    {
        _moreClicked = true;
        if (Tool != null) MoreRequested?.Invoke(this, Tool);
    }

    private static void OnAllowFavoriteChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ToolCard card) return;
        card.FavBtn.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnEnter(object sender, PointerRoutedEventArgs e)
    {
        _isPointerOver = true;
        if (Application.Current.Resources.TryGetValue(
            "CardBackgroundFillColorSecondaryBrush", out var b) && b is Brush brush)
            CardBorder.Background = brush;

        Motion.AnimateScale(CardScale, 1.03, 150);
    }

    private void OnExit(object sender, PointerRoutedEventArgs e)
    {
        _isPointerOver = false;
        if (Application.Current.Resources.TryGetValue(
            "CardBackgroundFillColorDefaultBrush", out var b) && b is Brush brush)
            CardBorder.Background = brush;

        Motion.AnimateScale(CardScale, 1.0, 150);
    }
}
