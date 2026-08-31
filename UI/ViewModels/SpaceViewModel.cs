using CommunityToolkit.Mvvm.ComponentModel;
using MiniT.Core.Models;
using MiniT.Core.Services;
using System.Collections.ObjectModel;

namespace MiniT.UI.ViewModels;

public class FolderViewModel
{
    public string Id { get; }
    public string Name { get; }
    public List<ToolDefinition> Tools { get; }

    public FolderViewModel(FolderDefinition def, IEnumerable<ToolDefinition> tools)
    {
        Id = def.Id;
        Name = def.Name;
        Tools = tools.ToList();
    }
}

public partial class SpaceViewModel : ObservableObject
{
    private readonly SpaceDefinition _definition;
    private readonly ToolRegistry _registry;
    private readonly SpaceService _spaceService;

    public string Id => _definition.Id;

    /// <summary>True only for the built-in "All Tools" space — a synthetic,
    /// always-present view of the whole tool registry. It isn't backed by a
    /// persisted SpaceDefinition (its ToolIds/Folders are never read), can't be
    /// renamed, pinned, or deleted, and never appears in <see cref="MainViewModel.RealSpaces"/>
    /// (the pickable destinations in the "Add to space" menu).</summary>
    public bool IsSystem { get; }

    [ObservableProperty] private string _name;
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private bool _isPinned;

    public ObservableCollection<FolderViewModel> Folders { get; } = new();
    public ObservableCollection<ToolDefinition> UnfolderedTools { get; } = new();
    public ObservableCollection<ToolDefinition> FavoriteTools { get; } = new();

    public SpaceViewModel(SpaceDefinition definition, ToolRegistry registry, SpaceService spaceService, bool isSystem = false)
    {
        _definition = definition;
        _registry = registry;
        _spaceService = spaceService;
        _name = definition.Name;
        _isPinned = definition.IsPinned;
        IsSystem = isSystem;
        Refresh();
    }

    /// <summary>Renames the space via the service (validates/trims) and keeps
    /// the bound Name in sync without re-triggering OnNameChanged's own save.
    /// No-op for the system space, which can't be renamed.</summary>
    public void Rename(string newName)
    {
        if (IsSystem) return;
        if (string.IsNullOrWhiteSpace(newName) || newName.Trim() == _definition.Name) return;
        _spaceService.RenameSpace(Id, newName);
        _name = _definition.Name;
        OnPropertyChanged(nameof(Name));
    }

    /// <summary>No-op for the system space, which always sorts first and can't be pinned.</summary>
    public void TogglePin()
    {
        if (IsSystem) return;
        _spaceService.TogglePin(Id);
        IsPinned = _definition.IsPinned;
    }

    public void Refresh()
    {
        Folders.Clear();
        UnfolderedTools.Clear();
        FavoriteTools.Clear();

        if (IsSystem)
        {
            // Every tool currently in the registry, read live rather than from
            // any stored ToolIds list, so this view can never drift out of sync
            // with what's actually installed/registered.
            foreach (var tool in _registry.AllTools.OrderBy(t => t.Name))
                UnfolderedTools.Add(tool);
            return;
        }

        var folderToolIds = _definition.Folders.SelectMany(f => f.ToolIds).ToHashSet();

        foreach (var folder in _definition.Folders.OrderBy(f => f.Order))
        {
            var tools = folder.ToolIds
                .Select(id => _registry.GetById(id))
                .Where(t => t != null)
                .Cast<ToolDefinition>()
                .ToList();
            Folders.Add(new FolderViewModel(folder, tools));
        }

        foreach (var id in _definition.ToolIds.Where(id => !folderToolIds.Contains(id)))
        {
            var tool = _registry.GetById(id);
            if (tool != null) UnfolderedTools.Add(tool);
        }

        foreach (var id in _definition.FavoriteToolIds)
        {
            var tool = _registry.GetById(id);
            if (tool != null) FavoriteTools.Add(tool);
        }
    }

    partial void OnNameChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        _definition.Name = value;
        _spaceService.Save();
    }
}
