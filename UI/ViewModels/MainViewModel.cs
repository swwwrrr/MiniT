using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiniT.Core.Models;
using MiniT.Core.Services;
using System.Collections.ObjectModel;
using Microsoft.UI.Xaml.Controls;

namespace MiniT.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    /// <summary>Fixed id for the synthetic "All Tools" space. It's never written
    /// to spaces.json — it's rebuilt fresh every launch — so this id only needs
    /// to be stable within a running session (e.g. to match Settings.LastActiveSpaceId).</summary>
    public const string SystemAllToolsId = "system-all-tools";

    private readonly ToolRegistry _registry;
    private readonly ToolLauncher _launcher;
    private readonly SpaceService _spaceService;
    private readonly SettingsService _settingsService;

    public ObservableCollection<SpaceViewModel> Spaces { get; } = new();

    /// <summary>Every user-created space, excluding the system "All Tools" space —
    /// the set of valid destinations offered in the "Add to space" menu.</summary>
    public IEnumerable<SpaceViewModel> RealSpaces => Spaces.Where(s => !s.IsSystem);
    public ObservableCollection<TabItem> OpenTabs { get; } = new();
    public ObservableCollection<ToolDefinition> SearchResults { get; } = new();

    [ObservableProperty] private SpaceViewModel? _selectedSpace;
    [ObservableProperty] private TabItem? _activeTab;
    [ObservableProperty] private string _searchQuery = "";
    [ObservableProperty] private bool _isSearchOpen;
    [ObservableProperty] private bool _isSettingsOpen;
    [ObservableProperty] private MiniTSettings _settings = new();
    [ObservableProperty] private string _searchFilter = "All"; // All, Local, Online

    public MainViewModel(ToolRegistry registry, ToolLauncher launcher,
        SpaceService spaceService, SettingsService settingsService)
    {
        _registry = registry;
        _launcher = launcher;
        _spaceService = spaceService;
        _settingsService = settingsService;
    }

    public void Initialize()
    {
        Settings = _settingsService.LoadSettings();
        _spaceService.Load();
        _registry.Initialize();
        LoadSpaces();

        if (Spaces.Count > 0)
        {
            var lastId = Settings.LastActiveSpaceId;
            SelectedSpace = Spaces.FirstOrDefault(s => s.Id == lastId) ?? Spaces[0];
        }
    }

    private void LoadSpaces()
    {
        Spaces.Clear();

        // The system space is a throwaway SpaceDefinition that's never handed
        // to SpaceService — it exists only so SpaceViewModel has something to
        // wrap, not because it's ever persisted or looked up by id there.
        var systemDef = new SpaceDefinition { Id = SystemAllToolsId, Name = "All Tools" };
        Spaces.Add(new SpaceViewModel(systemDef, _registry, _spaceService, isSystem: true));

        foreach (var s in _spaceService.Spaces)
            Spaces.Add(new SpaceViewModel(s, _registry, _spaceService));
    }

    [RelayCommand]
    public void LaunchTool(ToolDefinition tool)
    {
        // check if already open
        var existing = OpenTabs.FirstOrDefault(t => t.ToolId == tool.Id);
        if (existing != null) { SetActiveTab(existing); return; }

        var page = _launcher.Launch(tool);
        if (page == null) return; // e.g. External tool launched as a real process, no tab needed

        var tab = new TabItem(tool, page);
        OpenTabs.Add(tab);
        SetActiveTab(tab);
    }

    [RelayCommand]
    public void CloseTab(TabItem tab)
    {
        var idx = OpenTabs.IndexOf(tab);
        OpenTabs.Remove(tab);
        if (ActiveTab == tab)
        {
            if (OpenTabs.Count > 0)
                SetActiveTab(OpenTabs[Math.Max(0, idx - 1)]);
            else
                ActiveTab = null;
        }
    }

    [RelayCommand]
    public void SelectTab(TabItem tab) => SetActiveTab(tab);

    [RelayCommand]
    public void SelectTabByIndex(int index)
    {
        if (index >= 0 && index < OpenTabs.Count)
            SetActiveTab(OpenTabs[index]);
    }

    [RelayCommand]
    public void OpenSearch() { IsSearchOpen = true; UpdateSearch(); }

    [RelayCommand]
    public void CloseSearch() { IsSearchOpen = false; SearchQuery = ""; }

    [RelayCommand]
    public void OpenSettings() => IsSettingsOpen = true;

    [RelayCommand]
    public void CloseSettings() => IsSettingsOpen = false;

    partial void OnSearchQueryChanged(string value) => UpdateSearch();
    partial void OnSearchFilterChanged(string value) => UpdateSearch();

    private void UpdateSearch()
    {
        SearchResults.Clear();
        ToolType? filter = SearchFilter switch
        {
            "Local" => ToolType.Local,
            "Online" => ToolType.Online,
            _ => null
        };
        foreach (var t in _registry.Search(SearchQuery, filter))
            SearchResults.Add(t);
    }

    [RelayCommand]
    public void AddSpace()
    {
        var s = _spaceService.AddSpace("New Space");
        var vm = new SpaceViewModel(s, _registry, _spaceService);
        Spaces.Add(vm);
        SelectedSpace = vm;
    }

    [RelayCommand]
    public void DeleteSpace(SpaceViewModel space)
    {
        if (space.IsSystem) return; // the "All Tools" space can't be deleted
        _spaceService.DeleteSpace(space.Id);
        Spaces.Remove(space);
        if (SelectedSpace == space)
            SelectedSpace = Spaces.FirstOrDefault();
    }

    [RelayCommand]
    public void RenameSpace(SpaceViewModel space)
    {
        if (space.IsSystem) return; // the "All Tools" space can't be renamed
        space.IsEditing = true;
    }

    /// <summary>Commits a rename and re-sorts the sidebar (pin state can also
    /// have changed sort order since this space was last loaded).</summary>
    public void CommitRenameSpace(SpaceViewModel space, string newName)
    {
        space.Rename(newName);
        space.IsEditing = false;
        ReorderSpaces();
    }

    [RelayCommand]
    public void ToggleSpacePin(SpaceViewModel space)
    {
        if (space.IsSystem) return; // the "All Tools" space can't be pinned — it's already always first
        space.TogglePin();
        ReorderSpaces();
    }

    /// <summary>Re-reads sort order (pinned-first) from the service and applies
    /// it to the existing view-model collection in place, so identity/selection
    /// is preserved instead of rebuilding every SpaceViewModel from scratch. The
    /// system "All Tools" space isn't part of that persisted order — it's pinned
    /// to index 0 unconditionally.</summary>
    private void ReorderSpaces()
    {
        var orderedIds = _spaceService.Spaces.Select(s => s.Id).ToList();
        var sorted = orderedIds
            .Select(id => Spaces.FirstOrDefault(s => s.Id == id))
            .Where(s => s != null)
            .Cast<SpaceViewModel>()
            .ToList();

        var system = Spaces.FirstOrDefault(s => s.IsSystem);
        var finalOrder = system != null
            ? new List<SpaceViewModel> { system }.Concat(sorted).ToList()
            : sorted;

        for (int i = 0; i < finalOrder.Count; i++)
        {
            var current = Spaces.IndexOf(finalOrder[i]);
            if (current != i) Spaces.Move(current, i);
        }
    }

    /// <summary>Whether <paramref name="tool"/> has been added to <paramref name="space"/> —
    /// drives the checkmark in the "Add to space" menu. Always false for the
    /// system space, since it isn't a real destination.</summary>
    public bool IsToolInSpace(SpaceViewModel space, ToolDefinition tool) =>
        !space.IsSystem && _spaceService.SpaceContainsTool(space.Id, tool.Id);

    /// <summary>Adds a tool to a space from the "Add to space" menu (available on
    /// any tool card, including in the "All Tools" space, which is the main place
    /// this is meant to be used from).</summary>
    public void AddToolToSpace(ToolDefinition tool, SpaceViewModel space)
    {
        if (space.IsSystem) return;
        _spaceService.AddToolToSpace(space.Id, tool.Id);
        space.Refresh();
    }

    /// <summary>Removes a tool from a space (unchecking it in the "Add to space" menu).</summary>
    public void RemoveToolFromSpace(ToolDefinition tool, SpaceViewModel space)
    {
        if (space.IsSystem) return;
        _spaceService.RemoveToolFromSpace(space.Id, tool.Id);
        space.Refresh();
    }

    /// <summary>Adds a user-defined tool — Online (name + URL) or Local (name +
    /// path to a local site's entry HTML file) — to the registry and persists
    /// it as a manifest so it survives restarts.</summary>
    [RelayCommand]
    public void AddTool(ToolDefinition tool)
    {
        _registry.AddUserTool(tool);
        UpdateSearch();
    }

    [RelayCommand]
    public void ToggleFavorite(ToolDefinition tool)
    {
        if (SelectedSpace == null || SelectedSpace.IsSystem) return;
        _spaceService.ToggleFavorite(SelectedSpace.Id, tool.Id);
        SelectedSpace.Refresh();
    }

    partial void OnSelectedSpaceChanged(SpaceViewModel? value)
    {
        if (value != null)
        {
            Settings.LastActiveSpaceId = value.Id;
            _settingsService.SaveSettings(Settings);
        }
    }

    public void SaveSettings() => _settingsService.SaveSettings(Settings);

    private void SetActiveTab(TabItem tab)
    {
        if (ActiveTab != null) ActiveTab.IsActive = false;
        ActiveTab = tab;
        tab.IsActive = true;
    }
}
