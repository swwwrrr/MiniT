using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using MiniT.Core.Models;
using MiniT.Core.Services;
using MiniT.UI.Helpers;
using MiniT.UI.Pages;
using MiniT.UI.ViewModels;
using Windows.System;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace MiniT;

public sealed partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private SpaceContentPage? _spacePage;
    private SettingsPage? _settingsPage;

    public MainWindow()
    {
        InitializeComponent();

        // Mica gives the shell the same soft, depth-aware backdrop as native
        // Windows 11 apps (Settings, File Explorer) instead of a flat fill.
        SystemBackdrop = new MicaBackdrop { Kind = MicaKind.BaseAlt };

        // Custom title bar: just the wordmark is draggable, everything below
        // (search bar, sidebar, content) stays fully interactive.
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        TryTintCaptionButtons();

        var settingsSvc = new SettingsService();
        var registry    = new ToolRegistry();
        var factory     = new BuiltInToolViewFactory();
        var launcher    = new ToolLauncher(factory);
        var spaceSvc    = new SpaceService(settingsSvc);

        _vm = new MainViewModel(registry, launcher, spaceSvc, settingsSvc);
        _vm.Initialize();

        // Restore the sidebar's last width/collapsed state before the first
        // sidebar build, so there's no flash of the default width.
        _sidebarCollapsed = _vm.Settings.IsSidebarCollapsed;
        SidebarColumn.Width = new GridLength(_sidebarCollapsed
            ? CollapsedSidebarWidth
            : Math.Clamp(_vm.Settings.SidebarWidth, MinSidebarWidth, MaxSidebarWidth));

        RebuildSidebar();

        if (_vm.SelectedSpace != null)
            ShowSpace(_vm.SelectedSpace);

        if (Content is FrameworkElement root)
        {
            root.KeyDown += OnGlobalKeyDown;
            // Re-tint anything we built in code the moment the OS/theme switch
            // actually takes effect, so Light theme isn't stuck showing Dark colors.
            root.ActualThemeChanged += (_, _) => RefreshThemedVisuals();
        }

        ApplyTheme(_vm.Settings.Theme);
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1200, 760));

        AttachPress(SearchBarButton, pressScale: 0.985);
        AttachPress(AddSpaceButton, pressScale: 0.98);
        AttachPress(AddToolButton, pressScale: 0.98);
        AttachPress(SettingsButton, pressScale: 0.98);
        AttachPress(CollapseSidebarButton, pressScale: 0.98);

        // Apply the restored collapsed state to labels/icon/alignment now that
        // every named element exists and the sidebar has already been built
        // once above in the right (collapsed or expanded) shape.
        ApplySidebarCollapsedVisuals(_sidebarCollapsed);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static Thickness T(double all) => new(all, all, all, all);
    private static Thickness T(double h, double v) => new(h, v, h, v);
    private static Thickness T(double l, double t, double r, double b) => new(l, t, r, b);

    private Brush Res(string key) =>
        (Brush)Application.Current.Resources[key];

    /// <summary>Every place that changes what's showing in the main content area
    /// should go through this, so moving between a Space, a tool tab, and
    /// Settings always cross-fades instead of some paths animating and others
    /// cutting instantly.</summary>
    private void SetFrameContent(object? content) => Motion.SwapContent(ContentFrame, content);

    /// <summary>Adds a tactile "press down" scale to a hand-built row (sidebar
    /// item, search result, tab pill) — these are plain Borders, so unlike a
    /// real Button they get no press feedback for free.</summary>
    private void AttachPress(FrameworkElement el, double pressScale = 0.97)
    {
        el.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
        var scale = new ScaleTransform();
        el.RenderTransform = scale;
        Motion.AttachPressScale(el, scale, pressScale, 1.0);
    }

    /// <summary>Makes the caption buttons (min/max/close) blend with the app instead
    /// of showing the default opaque OS chrome. No-ops safely on older Windows builds.</summary>
    private void TryTintCaptionButtons()
    {
        if (!AppWindowTitleBar.IsCustomizationSupported()) return;
        var titleBar = AppWindow.TitleBar;
        titleBar.ButtonBackgroundColor = Colors.Transparent;
        titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
    }

    // ── Sidebar resize / collapse ────────────────────────────────────────

    private const double MinSidebarWidth = 200;
    private const double MaxSidebarWidth = 420;
    private const double CollapsedSidebarWidth = 64;

    private bool _resizingSidebar;
    private double _resizeStartWidth;
    private double _resizeStartPointerX;
    private bool _sidebarCollapsed;

    private void OnSplitterPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (_sidebarCollapsed) return;
        SplitterLine.Background = Res("AppAccentBrush");
    }

    private void OnSplitterPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (!_resizingSidebar)
            SplitterLine.Background = Res("CardStrokeColorDefaultBrush");
    }

    private void OnSplitterPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_sidebarCollapsed) return; // resize only applies to the expanded sidebar
        _resizingSidebar = true;
        _resizeStartWidth = SidebarColumn.Width.Value;
        _resizeStartPointerX = e.GetCurrentPoint(RootGrid).Position.X;
        Splitter.CapturePointer(e.Pointer);
    }

    private void OnSplitterPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_resizingSidebar) return;
        var x = e.GetCurrentPoint(RootGrid).Position.X;
        var newWidth = Math.Clamp(_resizeStartWidth + (x - _resizeStartPointerX), MinSidebarWidth, MaxSidebarWidth);
        SidebarColumn.Width = new GridLength(newWidth);
    }

    private void OnSplitterPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_resizingSidebar) return;
        _resizingSidebar = false;
        SplitterLine.Background = Res("CardStrokeColorDefaultBrush");
        Splitter.ReleasePointerCapture(e.Pointer);
        _vm.Settings.SidebarWidth = SidebarColumn.Width.Value;
        _vm.SaveSettings();
    }

    /// <summary>Toggles between the full sidebar (icon + name) and a narrow,
    /// icon-only rail — same idea as VS Code / most modern shells' collapsed
    /// nav. The width the user had before collapsing is remembered and
    /// restored on expand.</summary>
    private void OnToggleSidebarCollapsed(object sender, RoutedEventArgs e) => SetSidebarCollapsed(!_sidebarCollapsed);

    private void SetSidebarCollapsed(bool collapsed)
    {
        _sidebarCollapsed = collapsed;
        _vm.Settings.IsSidebarCollapsed = collapsed;

        if (collapsed)
        {
            SidebarColumn.Width = new GridLength(CollapsedSidebarWidth);
        }
        else
        {
            var restoreWidth = Math.Clamp(_vm.Settings.SidebarWidth, MinSidebarWidth, MaxSidebarWidth);
            SidebarColumn.Width = new GridLength(restoreWidth);
        }
        _vm.SaveSettings();

        ApplySidebarCollapsedVisuals(collapsed);
        RebuildSidebar();
    }

    /// <summary>Just the footer labels/icon/alignment for the current collapsed
    /// state — split out from SetSidebarCollapsed so window startup can apply
    /// the restored state without redundantly rebuilding the sidebar twice.</summary>
    private void ApplySidebarCollapsedVisuals(bool collapsed)
    {
        SpacesHeaderLabel.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
        AddSpaceLabel.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
        AddToolLabel.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
        SettingsLabel.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
        CollapseLabel.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
        CollapseIcon.Glyph = collapsed ? "\uE76C" : "\uE76B";
        ToolTipService.SetToolTip(CollapseSidebarButton, collapsed ? "Expand sidebar" : "Collapse sidebar");

        // Footer buttons center their icon instead of left-aligning a now-empty row.
        AddSpaceButton.HorizontalContentAlignment = collapsed ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        AddToolButton.HorizontalContentAlignment = collapsed ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        SettingsButton.HorizontalContentAlignment = collapsed ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        CollapseSidebarButton.HorizontalContentAlignment = collapsed ? HorizontalAlignment.Center : HorizontalAlignment.Left;
    }

    // ── Keyboard ──────────────────────────────────────────────────────────

    private void OnGlobalKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var ctrl = Microsoft.UI.Input.InputKeyboardSource
            .GetKeyStateForCurrentThread(VirtualKey.Control)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

        if (ctrl && e.Key == VirtualKey.K)           { OpenSearch(); e.Handled = true; }
        else if (e.Key == VirtualKey.Escape)         { HandleEscape(); e.Handled = true; }
        else if (ctrl && e.Key == VirtualKey.Number1){ ActivateTabByIndex(0); e.Handled = true; }
        else if (ctrl && e.Key == VirtualKey.Number2){ ActivateTabByIndex(1); e.Handled = true; }
        else if (ctrl && e.Key == VirtualKey.Number3){ ActivateTabByIndex(2); e.Handled = true; }
    }

    private void HandleEscape()
    {
        if (SearchOverlay.Visibility == Visibility.Visible) { CloseSearch(); return; }
        if (_vm.IsSettingsOpen) { CloseSettings(); return; }
        if (_vm.ActiveTab != null)
        {
            _vm.CloseTabCommand.Execute(_vm.ActiveTab);
            RebuildTabBar();
            if (_vm.ActiveTab != null) SetFrameContent(_vm.ActiveTab.Content);
            else ShowSpaceOrEmpty();
        }
    }

    private void ActivateTabByIndex(int idx)
    {
        _vm.SelectTabByIndexCommand.Execute(idx);
        if (_vm.ActiveTab != null) { SetFrameContent(_vm.ActiveTab.Content); RebuildTabBar(); }
    }

    // ── Search ────────────────────────────────────────────────────────────

    private void OnSearchClick(object sender, RoutedEventArgs e) => OpenSearch();

    private void OpenSearch()
    {
        _vm.OpenSearchCommand.Execute(null);
        SearchOverlay.Visibility = Visibility.Visible;
        SearchOverlay.Opacity = 0;
        SearchCardScale.ScaleX = 0.97;
        SearchCardScale.ScaleY = 0.97;
        Motion.FadeTo(SearchOverlay, 1, 130);
        Motion.AnimateScale(SearchCardScale, 1.0, 160);
        RebuildSearchResults();
        var checkedFilter = FilterAll.IsChecked == true ? FilterAll
            : FilterLocal.IsChecked == true ? FilterLocal : FilterOnline;
        UpdateFilterIndicator(checkedFilter, animate: false);
        SearchBox.Focus(FocusState.Programmatic);
    }

    private void CloseSearch()
    {
        Motion.FadeTo(SearchOverlay, 0, 100, () => SearchOverlay.Visibility = Visibility.Collapsed);
        Motion.AnimateScale(SearchCardScale, 0.97, 100);
        _vm.CloseSearchCommand.Execute(null);
    }

    /// <summary>Clicking the dimmed backdrop (not the palette card itself) closes search,
    /// matching every other command-palette pattern users already know.</summary>
    private void OnSearchOverlayBackgroundPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (ReferenceEquals(e.OriginalSource, SearchOverlay)) CloseSearch();
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        _vm.SearchQuery = SearchBox.Text;
        RebuildSearchResults();
    }

    private void OnSearchKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape) { CloseSearch(); e.Handled = true; }
        else if (e.Key == VirtualKey.Enter && _vm.SearchResults.Count > 0)
        {
            LaunchAndClose(_vm.SearchResults[0]);
            e.Handled = true;
        }
    }

    private void OnFilterChanged(object sender, RoutedEventArgs e)
    {
        // RadioButton with IsChecked="True" in XAML fires Checked synchronously
        // during InitializeComponent() — before later named elements in the tree
        // are connected AND before _vm is assigned in the constructor (it's set
        // after the InitializeComponent() call). Guard against both being unready.
        if (_vm == null) return;

        _vm.SearchFilter = (sender as FrameworkElement)?.Name switch
        {
            "FilterLocal" => "Local",
            "FilterOnline" => "Online",
            _ => "All"
        };
        if (sender is RadioButton rb) UpdateFilterIndicator(rb, animate: true);
        RebuildSearchResults();
    }

    /// <summary>Slides the accent highlight in the search overlay's All/Local/Online
    /// segmented control to sit exactly under whichever pill is now checked.</summary>
    private void UpdateFilterIndicator(RadioButton target, bool animate)
    {
        if (target.ActualWidth <= 0)
        {
            // Not laid out yet (e.g. overlay was just made visible this frame) —
            // force a layout pass so we can read a real width/position.
            FilterButtonsPanel.UpdateLayout();
        }

        var transform = target.TransformToVisual(FilterButtonsPanel);
        var x = transform.TransformPoint(new Windows.Foundation.Point(0, 0)).X;
        var width = target.ActualWidth;

        if (animate) Motion.AnimateSlide(FilterIndicatorTransform, FilterIndicator, x, width);
        else Motion.SetSlideImmediate(FilterIndicatorTransform, FilterIndicator, x, width);
    }

    private void RebuildSearchResults()
    {
        if (_vm.SearchResults.Count == 0)
        {
            SearchResultsList.ItemsSource = new List<UIElement> { BuildSearchEmptyState() };
            return;
        }
        SearchResultsList.ItemsSource = _vm.SearchResults.Select(BuildSearchItem).ToList();
    }

    private StackPanel BuildSearchEmptyState()
    {
        var panel = new StackPanel
        {
            Spacing = 6,
            Padding = T(0, 40, 0, 40),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        panel.Children.Add(new FontIcon
        {
            Glyph = "\uE721",
            FontSize = 22,
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = Res("TextFillColorTertiaryBrush")
        });
        panel.Children.Add(new TextBlock
        {
            Text = "No tools match your search",
            FontSize = 13,
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = Res("TextFillColorSecondaryBrush")
        });
        return panel;
    }

    private Border BuildSearchItem(ToolDefinition tool)
    {
        var initials = string.Concat(
            tool.Name.Split(' ').Select(w => w.Length > 0 ? w[0].ToString() : "").Take(2));

        var badge = new Border
        {
            Width = 38, Height = 38, CornerRadius = new CornerRadius(10),
            Background = Res("AppAccentSoftBrush"),
            Margin = T(0, 0, 12, 0),
            Child = new TextBlock
            {
                Text = initials,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                FontSize = 14,
                Foreground = Res("AppAccentBrush")
            }
        };

        var typeBadge = new Border
        {
            CornerRadius = new CornerRadius(5),
            Padding = T(7, 3, 7, 3),
            Background = Res("SubtleFillColorSecondaryBrush"),
            Child = new TextBlock
            {
                Text = tool.Type.ToString(), FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = Res("TextFillColorSecondaryBrush")
            }
        };

        var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 1 };
        info.Children.Add(new TextBlock
        {
            Text = tool.Name,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 14
        });
        info.Children.Add(new TextBlock
        {
            Text = tool.Description, FontSize = 12,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = Res("TextFillColorSecondaryBrush")
        });

        var addBtn = new Button
        {
            Content = new FontIcon { Glyph = "\uE710", FontSize = 13 }, // "+" glyph
            Width = 30,
            Height = 30,
            Margin = T(8, 0, 0, 0),
            Background = new SolidColorBrush(Colors.Transparent),
            BorderThickness = T(0, 0, 0, 0),
            Padding = T(0),
            CornerRadius = new CornerRadius(7),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Foreground = Res("TextFillColorSecondaryBrush")
        };
        ToolTipService.SetToolTip(addBtn, "Add to space");
        // Stop the press from bubbling up to the row's PointerPressed (which
        // launches the tool) — this button has its own, different job.
        addBtn.PointerPressed += (_, pe) => pe.Handled = true;
        addBtn.Click += (_, _) => AddToSpaceMenu.Show(_vm, tool, addBtn);

        var grid = new Grid { Padding = T(10, 10, 10, 10), CornerRadius = new CornerRadius(10) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(badge, 0);     grid.Children.Add(badge);
        Grid.SetColumn(info, 1);      grid.Children.Add(info);
        Grid.SetColumn(typeBadge, 2); grid.Children.Add(typeBadge);
        Grid.SetColumn(addBtn, 3);    grid.Children.Add(addBtn);

        var border = new Border { Child = grid, Tag = tool, CornerRadius = new CornerRadius(10) };
        border.PointerPressed += (_, _) => LaunchAndClose(tool);
        border.PointerEntered += (_, _) =>
            grid.Background = Res("SubtleFillColorSecondaryBrush");
        border.PointerExited += (_, _) =>
            grid.Background = new SolidColorBrush(Colors.Transparent);
        AttachPress(border);
        return border;
    }

    private void LaunchAndClose(ToolDefinition tool)
    {
        CloseSearch();
        LaunchTool(tool);
    }

    /// <summary>
    /// Launches a tool and makes sure the tab bar + content frame actually reflect it.
    /// Any UI entry point that starts a tool (search, sidebar, or a tool card inside
    /// a Space) must go through this so the app never "silently" opens a tab.
    /// </summary>
    private void LaunchTool(ToolDefinition tool)
    {
        if (_vm.IsSettingsOpen) CloseSettings();
        _vm.LaunchToolCommand.Execute(tool);
        RebuildTabBar();
        if (_vm.ActiveTab != null) SetFrameContent(_vm.ActiveTab.Content);
    }

    // ── Sidebar ───────────────────────────────────────────────────────────

    private void RebuildSidebar()
    {
        // A thin separator right after the system "All Tools" space visually
        // splits it from the user's own spaces below, since it behaves quite
        // differently (always present, always full, nothing to rename/pin/delete).
        var items = new List<UIElement>();
        foreach (var space in _vm.Spaces)
        {
            items.Add(BuildSpaceItem(space));
            if (space.IsSystem)
                items.Add(new Border
                {
                    Height = 1,
                    Margin = T(12, 6, 12, 6),
                    Background = Res("CardStrokeColorDefaultBrush")
                });
        }
        SpaceList.ItemsSource = items;
    }

    private Border BuildSpaceItem(SpaceViewModel space)
    {
        bool isSelected = !_vm.IsSettingsOpen && _vm.SelectedSpace?.Id == space.Id;
        bool collapsed = _sidebarCollapsed;

        var dot = new FontIcon
        {
            Glyph = space.IsSystem ? "\uE71D" : (space.IsPinned ? "\uE718" : "\uE8B7"), FontSize = 13,
            Margin = collapsed ? T(0) : T(1, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = collapsed ? HorizontalAlignment.Center : HorizontalAlignment.Left,
            Foreground = isSelected ? Res("AppAccentBrush") : Res("TextFillColorTertiaryBrush")
        };

        var label = new TextBlock
        {
            Text = space.Name,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            FontSize = 13.5,
            FontWeight = isSelected ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
            Foreground = isSelected ? Res("TextFillColorPrimaryBrush") : Res("TextFillColorSecondaryBrush"),
            Visibility = (space.IsEditing || collapsed) ? Visibility.Collapsed : Visibility.Visible
        };

        // Swapped in for `label` while renaming — same slot in the grid so the
        // row's layout doesn't jump when entering/leaving edit mode. Renaming
        // always happens with the sidebar expanded (see ShowSpaceContextMenu),
        // so this never needs to render in the collapsed, icon-only layout.
        var editBox = new TextBox
        {
            Text = space.Name,
            FontSize = 13.5,
            Padding = T(6, 2, 6, 2),
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = (space.IsEditing && !collapsed) ? Visibility.Visible : Visibility.Collapsed
        };

        void CommitEdit()
        {
            _vm.CommitRenameSpace(space, editBox.Text);
            RebuildSidebar();
        }
        editBox.KeyDown += (_, ke) =>
        {
            if (ke.Key == VirtualKey.Enter) { CommitEdit(); ke.Handled = true; }
            else if (ke.Key == VirtualKey.Escape) { space.IsEditing = false; RebuildSidebar(); ke.Handled = true; }
        };
        editBox.LostFocus += (_, _) => { if (space.IsEditing) CommitEdit(); };
        if (space.IsEditing && !collapsed)
            editBox.Loaded += (_, _) => { editBox.Focus(FocusState.Programmatic); editBox.SelectAll(); };

        // 28x28 hit target (not just the glyph) so it's easy to hit without
        // fat-fingering the space item underneath it. Opens the same rename/
        // pin/delete menu as right-clicking the row. Hidden in the collapsed
        // rail — there's no room, and right-click still reaches the same menu.
        var menuBtn = new Button
        {
            Content = new FontIcon { Glyph = "\uE712", FontSize = 14 }, // "more" (…) glyph
            Width = 26,
            Height = 26,
            Background = new SolidColorBrush(Colors.Transparent),
            BorderThickness = T(0, 0, 0, 0),
            Padding = T(0),
            CornerRadius = new CornerRadius(6),
            Margin = T(0, 0, 4, 0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Foreground = Res("TextFillColorTertiaryBrush"),
            Opacity = 0,
            // Nothing to rename/pin/delete on the system space, so it gets no
            // "…" button at all rather than one that opens an empty menu.
            Visibility = (collapsed || space.IsSystem) ? Visibility.Collapsed : Visibility.Visible
        };
        ToolTipService.SetToolTip(menuBtn, "More options");
        menuBtn.Click += (_, _) => ShowSpaceContextMenu(menuBtn, space);

        Grid content;
        if (collapsed)
        {
            // Icon-only: single centered column, no name/menu column to save.
            content = new Grid { ColumnDefinitions = { new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) } } };
            Grid.SetColumn(dot, 0); content.Children.Add(dot);
            ToolTipService.SetToolTip(content, space.Name);
        }
        else
        {
            content = new Grid { ColumnDefinitions = { new ColumnDefinition { Width = GridLength.Auto }, new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }, new ColumnDefinition { Width = GridLength.Auto } } };
            Grid.SetColumn(dot, 0);       content.Children.Add(dot);
            Grid.SetColumn(label, 1);     content.Children.Add(label);
            Grid.SetColumn(editBox, 1);   content.Children.Add(editBox);
            Grid.SetColumn(menuBtn, 2);   content.Children.Add(menuBtn);
        }

        // Thin accent bar on the left edge marks the active space, like a
        // selection indicator in a modern nav rail, instead of a flat fill only.
        // Skipped in the collapsed rail — too little width to spare and the
        // centered icon + tinted background already read as "selected".
        var row = content;
        if (!collapsed)
        {
            var indicator = new Border
            {
                Width = 3, CornerRadius = new CornerRadius(2),
                Margin = T(0, 3, 0, 3),
                Background = isSelected ? Res("AppAccentBrush")
                                         : new SolidColorBrush(Colors.Transparent)
            };
            if (isSelected)
            {
                // Grows the bar in from the middle right as this space becomes
                // active, so switching spaces reads as the indicator "snapping"
                // onto the new row instead of just appearing.
                indicator.Loaded += (_, _) => Motion.GrowVerticalIn(indicator);
            }

            row = new Grid { ColumnDefinitions = { new ColumnDefinition { Width = GridLength.Auto }, new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) } } };
            Grid.SetColumn(indicator, 0); row.Children.Add(indicator);
            var inner = new Border { Child = content, Padding = T(10, 8, 6, 8) };
            Grid.SetColumn(inner, 1); row.Children.Add(inner);
        }

        var border = new Border
        {
            Child = row,
            Margin = T(8, 1, 8, 1),
            Padding = collapsed ? T(0, 9, 0, 9) : T(0),
            CornerRadius = new CornerRadius(8),
            Background = isSelected
                ? Res("SubtleFillColorSecondaryBrush")
                : new SolidColorBrush(Colors.Transparent)
        };
        border.PointerPressed += (_, _) =>
        {
            if (space.IsEditing) return; // don't switch spaces while renaming
            if (_vm.SelectedSpace?.Id == space.Id && !_vm.IsSettingsOpen) return;
            if (_vm.IsSettingsOpen)
            {
                _vm.CloseSettingsCommand.Execute(null);
                SettingsButton.Background = new SolidColorBrush(Colors.Transparent);
            }
            _vm.SelectedSpace = space;
            ShowSpace(space);
            RebuildSidebar();
        };
        border.RightTapped += (_, re) =>
        {
            if (space.IsSystem) { re.Handled = true; return; }
            ShowSpaceContextMenu(border, space, re.GetPosition(border));
            re.Handled = true;
        };
        border.PointerEntered += (_, _) =>
        {
            if (!isSelected)
                border.Background = Res("SubtleFillColorTertiaryBrush");
            if (!space.IsSystem) menuBtn.Opacity = 1;
        };
        border.PointerExited += (_, _) =>
        {
            if (!isSelected)
                border.Background = new SolidColorBrush(Colors.Transparent);
            menuBtn.Opacity = 0;
        };
        AttachPress(border, pressScale: 0.985);
        return border;
    }

    /// <summary>Right-click (or the "…" button) context menu for a space:
    /// rename, pin/unpin, delete — the three things you can't do to a space
    /// any other way.</summary>
    private void ShowSpaceContextMenu(FrameworkElement anchor, SpaceViewModel space, Windows.Foundation.Point? position = null)
    {
        var menu = new MenuFlyout();

        var renameItem = new MenuFlyoutItem
        {
            Text = "Rename",
            Icon = new FontIcon { Glyph = "\uE70F" }
        };
        renameItem.Click += (_, _) =>
        {
            // Renaming needs the full-width row (name + textbox); expand the
            // rail first if it's currently collapsed to icons-only.
            if (_sidebarCollapsed) SetSidebarCollapsed(false);
            space.IsEditing = true;
            RebuildSidebar();
        };
        menu.Items.Add(renameItem);

        var pinItem = new MenuFlyoutItem
        {
            Text = space.IsPinned ? "Unpin" : "Pin to top",
            Icon = new FontIcon { Glyph = space.IsPinned ? "\uE77A" : "\uE718" }
        };
        pinItem.Click += (_, _) =>
        {
            _vm.ToggleSpacePinCommand.Execute(space);
            RebuildSidebar();
        };
        menu.Items.Add(pinItem);

        menu.Items.Add(new MenuFlyoutSeparator());

        var deleteItem = new MenuFlyoutItem
        {
            Text = "Delete",
            Icon = new FontIcon { Glyph = "\uE74D" },
            Foreground = Res("SystemFillColorCriticalBrush")
        };
        deleteItem.Click += async (_, _) =>
        {
            var confirmed = await ConfirmDeleteSpaceAsync(space.Name);
            if (!confirmed) return;
            _vm.DeleteSpaceCommand.Execute(space);
            RebuildSidebar();
            ShowSpaceOrEmpty();
        };
        menu.Items.Add(deleteItem);

        if (position.HasValue)
            menu.ShowAt(anchor, position.Value);
        else
            menu.ShowAt(anchor);
    }

    /// <summary>Asks the user to confirm before permanently deleting a space and its layout.</summary>
    private async Task<bool> ConfirmDeleteSpaceAsync(string spaceName)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = Content.XamlRoot,
            Title = "Delete space?",
            Content = $"\"{spaceName}\" and its folders/favorites will be removed. " +
                      "The tools themselves are not affected.",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close
        };
        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    private void OnAddSpace(object sender, RoutedEventArgs e)
    {
        if (_vm.IsSettingsOpen)
        {
            _vm.CloseSettingsCommand.Execute(null);
            SettingsButton.Background = new SolidColorBrush(Colors.Transparent);
        }
        _vm.AddSpaceCommand.Execute(null);
        RebuildSidebar();
        if (_vm.SelectedSpace != null) ShowSpace(_vm.SelectedSpace);
    }

    // ── Add Tool (Online or Local) ──────────────────────────────────────────

    /// <summary>One button now covers both tool types: a segmented choice at
    /// the top of the dialog swaps the "location" field between a URL box
    /// (Online) and a folder picker (Local) while Name/Description/Category
    /// stay shared, then a single Add button commits whichever type is active.</summary>
    private async void OnAddTool(object sender, RoutedEventArgs e)
    {
        var typeSelector = new RadioButtons { MaxColumns = 2 };
        typeSelector.Items.Add("Online");
        typeSelector.Items.Add("Local");
        typeSelector.SelectedIndex = 0;

        var nameBox = new TextBox { PlaceholderText = "e.g. ChatGPT", Header = "Name" };
        var urlBox = new TextBox { PlaceholderText = "https://example.com", Header = "Link (URL)" };

        // Local tools point at a folder on disk instead of a URL — the path
        // box is read-only and only ever filled in via the Browse picker, so
        // there's no typo-prone manual path entry.
        var folderPathBox = new TextBox
        {
            PlaceholderText = "No folder selected",
            Header = "Folder",
            IsReadOnly = true
        };
        var browseButton = new Button { Content = "Browse…", Margin = new Thickness(0, 6, 0, 0) };
        var localPanel = new StackPanel { Spacing = 8, Visibility = Visibility.Collapsed };
        localPanel.Children.Add(folderPathBox);
        localPanel.Children.Add(browseButton);

        string? selectedFolderPath = null;

        browseButton.Click += async (_, _) =>
        {
            var picker = new FolderPicker { SuggestedStartLocation = PickerLocationId.ComputerFolder };
            picker.FileTypeFilter.Add("*");
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));

            var folder = await picker.PickSingleFolderAsync();
            if (folder == null) return; // user cancelled

            selectedFolderPath = folder.Path;
            folderPathBox.Text = folder.Path;
            if (string.IsNullOrWhiteSpace(nameBox.Text))
                nameBox.Text = folder.Name;
        };

        var descBox = new TextBox
        {
            PlaceholderText = "What is this tool for?",
            Header = "Description",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 70
        };
        var categoryBox = new TextBox { PlaceholderText = "e.g. AI, Design, Utilities", Header = "Category" };
        var errorText = new TextBlock
        {
            Foreground = Res("SystemFillColorCriticalBrush"),
            FontSize = 12,
            Visibility = Visibility.Collapsed,
            TextWrapping = TextWrapping.Wrap
        };

        typeSelector.SelectionChanged += (_, _) =>
        {
            var isLocal = typeSelector.SelectedIndex == 1;
            urlBox.Visibility = isLocal ? Visibility.Collapsed : Visibility.Visible;
            localPanel.Visibility = isLocal ? Visibility.Visible : Visibility.Collapsed;
            errorText.Visibility = Visibility.Collapsed;
        };

        var panel = new StackPanel { Spacing = 12, MinWidth = 340 };
        panel.Children.Add(typeSelector);
        panel.Children.Add(nameBox);
        panel.Children.Add(urlBox);
        panel.Children.Add(localPanel);
        panel.Children.Add(descBox);
        panel.Children.Add(categoryBox);
        panel.Children.Add(errorText);

        var dialog = new ContentDialog
        {
            XamlRoot = Content.XamlRoot,
            Title = "Add tool",
            Content = panel,
            PrimaryButtonText = "Add",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary
        };

        // Validate on Primary click instead of letting the dialog close on bad
        // input — args.Cancel keeps it open so the user can fix the field.
        dialog.PrimaryButtonClick += (_, args) =>
        {
            var name = nameBox.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(name))
            {
                ShowFieldError(errorText, "Please enter a name.");
                args.Cancel = true;
                return;
            }

            ToolDefinition tool;

            if (typeSelector.SelectedIndex == 1) // Local
            {
                if (string.IsNullOrWhiteSpace(selectedFolderPath))
                {
                    ShowFieldError(errorText, "Please choose a folder.");
                    args.Cancel = true;
                    return;
                }

                var entryPath = FindEntryHtml(selectedFolderPath);
                if (entryPath == null)
                {
                    ShowFieldError(errorText, "No .html file found in that folder. Pick a folder that contains an index.html (or another .html file).");
                    args.Cancel = true;
                    return;
                }

                tool = new ToolDefinition
                {
                    Name = name,
                    Description = descBox.Text?.Trim() ?? "",
                    Category = categoryBox.Text?.Trim() ?? "",
                    Type = ToolType.Local,
                    LocalEntryPath = entryPath,
                    LocalFolderPath = selectedFolderPath
                };
            }
            else // Online
            {
                var url = urlBox.Text?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(url) ||
                    !Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    ShowFieldError(errorText, "Please enter a valid link starting with http:// or https://");
                    args.Cancel = true;
                    return;
                }

                tool = new ToolDefinition
                {
                    Name = name,
                    Url = url,
                    Description = descBox.Text?.Trim() ?? "",
                    Category = categoryBox.Text?.Trim() ?? "",
                    Type = ToolType.Online
                };
            }

            _vm.AddToolCommand.Execute(tool);
        };

        await dialog.ShowAsync();
    }

    /// <summary>Resolves a user-picked folder to the HTML file MiniT should load
    /// for it: an index.html/.htm in the folder root, then any .html/.htm file in
    /// the root, then the same search one level deeper (covers a site whose actual
    /// output lives in a nested "dist"/"build" subfolder). Returns null if the
    /// folder doesn't look like a local site at all.</summary>
    private static string? FindEntryHtml(string folderPath)
    {
        if (!Directory.Exists(folderPath)) return null;

        foreach (var name in new[] { "index.html", "index.htm" })
        {
            var direct = Path.Combine(folderPath, name);
            if (File.Exists(direct)) return direct;
        }

        var rootHtml = Directory.EnumerateFiles(folderPath, "*.htm*", SearchOption.TopDirectoryOnly)
            .FirstOrDefault();
        if (rootHtml != null) return rootHtml;

        try
        {
            return Directory.EnumerateFiles(folderPath, "index.htm*", SearchOption.AllDirectories).FirstOrDefault()
                ?? Directory.EnumerateFiles(folderPath, "*.htm*", SearchOption.AllDirectories).FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private static void ShowFieldError(TextBlock errorText, string message)
    {
        errorText.Text = message;
        errorText.Visibility = Visibility.Visible;
    }

    // ── Settings ──────────────────────────────────────────────────────────

    private void OnSettings(object sender, RoutedEventArgs e)
    {
        _vm.OpenSettingsCommand.Execute(null);

        _settingsPage ??= new SettingsPage();
        _settingsPage.SetViewModel(_vm);
        _settingsPage.ThemeChanged -= ApplyTheme;
        _settingsPage.ThemeChanged += ApplyTheme;
        _settingsPage.BackRequested -= OnSettingsBackRequested;
        _settingsPage.BackRequested += OnSettingsBackRequested;

        SetFrameContent(_settingsPage);
        TabBarHost.Visibility = Visibility.Collapsed;
        RebuildSidebar(); // deselects spaces so it's clear we've navigated away
        SettingsButton.Background = Res("SubtleFillColorSecondaryBrush");
    }

    private void OnSettingsBackRequested(object? sender, EventArgs e) => CloseSettings();

    /// <summary>The one, obvious way to leave Settings: goes back to whatever was open before.</summary>
    private void CloseSettings()
    {
        _vm.CloseSettingsCommand.Execute(null);
        SettingsButton.Background = new SolidColorBrush(Colors.Transparent);
        RebuildSidebar();
        if (_vm.ActiveTab != null)
        {
            SetFrameContent(_vm.ActiveTab.Content);
            RebuildTabBar();
        }
        else
        {
            ShowSpaceOrEmpty();
        }
    }

    // ── Content / Tabs ────────────────────────────────────────────────────

    private void ShowSpace(SpaceViewModel space)
    {
        _spacePage = new SpaceContentPage();
        _spacePage.Load(space, _vm);
        _spacePage.ToolLaunched += (_, tool) => LaunchTool(tool);
        SetFrameContent(_spacePage);
        RebuildTabBar();
    }

    private void ShowSpaceOrEmpty()
    {
        if (_vm.SelectedSpace != null) ShowSpace(_vm.SelectedSpace);
        else SetFrameContent(null);
    }

    private void RebuildTabBar()
    {
        TabBar.Children.Clear();
        if (_vm.OpenTabs.Count == 0 || _vm.IsSettingsOpen)
        {
            TabBarHost.Visibility = Visibility.Collapsed;
            return;
        }
        TabBarHost.Visibility = Visibility.Visible;

        foreach (var tab in _vm.OpenTabs)
        {
            bool active = tab == _vm.ActiveTab;
            var capturedTab = tab;

            // Each tab is a small rows-of-2 grid: the pill itself, and a thin
            // accent underline that only lights up for the active tab — a more
            // modern "browser tab" feel than a solid block of accent color.
            var tabGrid = new Grid { RowDefinitions = { new RowDefinition { Height = GridLength.Auto }, new RowDefinition { Height = new GridLength(2) } } };

            var pill = new Border
            {
                CornerRadius = new CornerRadius(8, 8, 0, 0),
                Padding = T(12, 8, 8, 8),
                Background = active ? Res("CardBackgroundFillColorDefaultBrush") : new SolidColorBrush(Colors.Transparent)
            };

            var inner = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            inner.Children.Add(new TextBlock
            {
                Text = tab.Title,
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = active ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
                Foreground = active ? Res("TextFillColorPrimaryBrush") : Res("TextFillColorSecondaryBrush")
            });

            var closeBtn = new Button
            {
                Content = new FontIcon { Glyph = "\uE711", FontSize = 10 },
                Width = 20,
                Height = 20,
                Background = new SolidColorBrush(Colors.Transparent),
                BorderThickness = T(0, 0, 0, 0),
                Padding = T(0),
                CornerRadius = new CornerRadius(5),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Foreground = active ? Res("TextFillColorSecondaryBrush") : Res("TextFillColorTertiaryBrush")
            };
            ToolTipService.SetToolTip(closeBtn, "Close tab");
            closeBtn.Click += (_, _) =>
            {
                _vm.CloseTabCommand.Execute(capturedTab);
                RebuildTabBar();
                if (_vm.ActiveTab != null) SetFrameContent(_vm.ActiveTab.Content);
                else ShowSpaceOrEmpty();
            };
            inner.Children.Add(closeBtn);
            pill.Child = inner;

            pill.PointerPressed += (_, _) =>
            {
                _vm.SelectTabCommand.Execute(capturedTab);
                SetFrameContent(capturedTab.Content);
                RebuildTabBar();
            };
            pill.PointerEntered += (_, _) =>
            {
                if (!active) pill.Background = Res("SubtleFillColorTertiaryBrush");
            };
            pill.PointerExited += (_, _) =>
            {
                if (!active) pill.Background = new SolidColorBrush(Colors.Transparent);
            };

            var underline = new Border
            {
                Height = 2,
                Background = active ? Res("AppAccentBrush")
                                     : new SolidColorBrush(Colors.Transparent)
            };

            AttachPress(pill, pressScale: 0.98);

            Grid.SetRow(pill, 0);      tabGrid.Children.Add(pill);
            Grid.SetRow(underline, 1); tabGrid.Children.Add(underline);

            TabBar.Children.Add(tabGrid);
        }
    }

    // ── Theme ─────────────────────────────────────────────────────────────

    private void ApplyTheme(AppTheme theme)
    {
        if (Content is FrameworkElement root)
            root.RequestedTheme = theme switch
            {
                AppTheme.Light => ElementTheme.Light,
                AppTheme.Dark  => ElementTheme.Dark,
                _              => ElementTheme.Default
            };
        // ActualThemeChanged fires once the change above is resolved (it may
        // fire async/next layout pass), which then calls RefreshThemedVisuals().
        // Also refresh immediately in case the theme didn't actually change
        // (e.g. re-selecting the same option) so nothing looks stuck.
        RefreshThemedVisuals();
    }

    /// <summary>
    /// A lot of this UI is built in C# with brushes read once from
    /// Application.Current.Resources (badges, tab bar, sidebar, tool cards).
    /// Those brushes don't repaint themselves when the theme flips, so anything
    /// already on screen has to be rebuilt after a theme change or it stays
    /// stuck showing the old palette (this was the main cause of "Light theme
    /// doesn't work").
    /// </summary>
    private void RefreshThemedVisuals()
    {
        RebuildSidebar();
        RebuildTabBar();

        SettingsButton.Background = _vm.IsSettingsOpen
            ? Res("SubtleFillColorSecondaryBrush")
            : new SolidColorBrush(Colors.Transparent);

        if (_vm.IsSettingsOpen)
        {
            // Settings page uses ThemeResource bindings in XAML, so it repaints
            // itself — just make sure it's still the one showing.
            ContentFrame.Content = _settingsPage;
        }
        else if (_vm.ActiveTab != null)
        {
            ContentFrame.Content = _vm.ActiveTab.Content;
        }
        else if (_vm.SelectedSpace != null)
        {
            ShowSpace(_vm.SelectedSpace);
        }

        if (SearchOverlay.Visibility == Visibility.Visible)
            RebuildSearchResults();
    }
}
