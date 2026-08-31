using MiniT.Core.Models;

namespace MiniT.Core.Services;

public class SpaceService
{
    private readonly SettingsService _settings;
    private SpacesData _data = new();

    public SpaceService(SettingsService settings)
    {
        _settings = settings;
    }

    // Pinned spaces float to the top (still ordered among themselves by Order),
    // then the rest follow in their own Order — so pinning never has to
    // renumber the whole list, it just changes which group a space sorts into.
    public List<SpaceDefinition> Spaces => _data.Spaces
        .OrderByDescending(s => s.IsPinned)
        .ThenBy(s => s.Order)
        .ToList();

    public void Load() => _data = _settings.LoadSpaces();
    public void Save() => _settings.SaveSpaces(_data);

    public SpaceDefinition AddSpace(string name)
    {
        var s = new SpaceDefinition { Name = name, Order = _data.Spaces.Count };
        _data.Spaces.Add(s);
        Save();
        return s;
    }

    public void RenameSpace(string id, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName)) return;
        var s = _data.Spaces.FirstOrDefault(x => x.Id == id);
        if (s != null) { s.Name = newName.Trim(); Save(); }
    }

    public void DeleteSpace(string id)
    {
        _data.Spaces.RemoveAll(s => s.Id == id);
        Save();
    }

    public void TogglePin(string id)
    {
        var s = _data.Spaces.FirstOrDefault(x => x.Id == id);
        if (s == null) return;
        s.IsPinned = !s.IsPinned;
        Save();
    }

    public bool IsPinned(string id) =>
        _data.Spaces.FirstOrDefault(x => x.Id == id)?.IsPinned ?? false;

    public void ToggleFavorite(string spaceId, string toolId)
    {
        var s = _data.Spaces.FirstOrDefault(x => x.Id == spaceId);
        if (s == null) return;
        if (s.FavoriteToolIds.Contains(toolId)) s.FavoriteToolIds.Remove(toolId);
        else s.FavoriteToolIds.Add(toolId);
        Save();
    }

    public bool IsFavorite(string spaceId, string toolId)
    {
        var s = _data.Spaces.FirstOrDefault(x => x.Id == spaceId);
        return s?.FavoriteToolIds.Contains(toolId) ?? false;
    }

    /// <summary>Adds a tool to a space's top-level (unfoldered) list, e.g. from the
    /// "Add to space" menu on a tool card. No-op if it's already there.</summary>
    public void AddToolToSpace(string spaceId, string toolId)
    {
        var s = _data.Spaces.FirstOrDefault(x => x.Id == spaceId);
        if (s == null) return;
        if (!s.ToolIds.Contains(toolId))
        {
            s.ToolIds.Add(toolId);
            Save();
        }
    }

    /// <summary>Removes a tool from a space entirely — its top-level list, every
    /// folder, and favorites — so unchecking a space in the "Add to space" menu
    /// fully detaches the tool from it.</summary>
    public void RemoveToolFromSpace(string spaceId, string toolId)
    {
        var s = _data.Spaces.FirstOrDefault(x => x.Id == spaceId);
        if (s == null) return;
        bool changed = s.ToolIds.Remove(toolId);
        foreach (var f in s.Folders)
            if (f.ToolIds.Remove(toolId)) changed = true;
        if (s.FavoriteToolIds.Remove(toolId)) changed = true;
        if (changed) Save();
    }

    /// <summary>Whether a tool has been added anywhere in the given space
    /// (top-level or in one of its folders) — drives the checkmark in the
    /// "Add to space" menu.</summary>
    public bool SpaceContainsTool(string spaceId, string toolId)
    {
        var s = _data.Spaces.FirstOrDefault(x => x.Id == spaceId);
        if (s == null) return false;
        return s.ToolIds.Contains(toolId) || s.Folders.Any(f => f.ToolIds.Contains(toolId));
    }

    public FolderDefinition AddFolder(string spaceId, string name)
    {
        var s = _data.Spaces.FirstOrDefault(x => x.Id == spaceId);
        if (s == null) throw new InvalidOperationException("Space not found");
        var f = new FolderDefinition { Name = name, Order = s.Folders.Count };
        s.Folders.Add(f);
        Save();
        return f;
    }

    public void DeleteFolder(string spaceId, string folderId)
    {
        var s = _data.Spaces.FirstOrDefault(x => x.Id == spaceId);
        s?.Folders.RemoveAll(f => f.Id == folderId);
        Save();
    }
}
