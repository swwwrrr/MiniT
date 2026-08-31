using System.Text.Json;
using MiniT.Core.Models;

namespace MiniT.Core.Services;

public class ToolRegistry
{
    private readonly List<ToolDefinition> _tools = new();
    private static readonly JsonSerializerOptions Opts = new() { PropertyNameCaseInsensitive = true };

    public IReadOnlyList<ToolDefinition> AllTools => _tools;

    public void Initialize()
    {
        _tools.Clear();

        // 1. Built-in registry
        var registryFile = Path.Combine(AppContext.BaseDirectory, "Registry", "tools.json");
        if (File.Exists(registryFile))
            LoadFromRegistry(registryFile);

        // 2. Bundled local tools (manifest.json in each subfolder)
        var builtInDir = Path.Combine(AppContext.BaseDirectory, "Tools");
        if (Directory.Exists(builtInDir))
            ScanManifests(builtInDir);

        // 3. User tools
        var userDir = SettingsService.GetUserToolsPath();
        if (Directory.Exists(userDir))
            ScanManifests(userDir);
    }

    public ToolDefinition? GetById(string id) =>
        _tools.FirstOrDefault(t => t.Id == id);

    /// <summary>Adds a user-defined tool (currently used for Online tools added from
    /// the UI) and writes it to its own manifest.json under the user tools folder,
    /// so it's picked back up by ScanManifests on the next launch too.</summary>
    public ToolDefinition AddUserTool(ToolDefinition tool)
    {
        if (string.IsNullOrWhiteSpace(tool.Id))
            tool.Id = "user-" + Guid.NewGuid().ToString("N")[..12];

        // Guard against an accidental duplicate id.
        while (_tools.Any(t => t.Id == tool.Id))
            tool.Id = "user-" + Guid.NewGuid().ToString("N")[..12];

        var userDir = SettingsService.GetUserToolsPath();
        var toolDir = Path.Combine(userDir, tool.Id);
        Directory.CreateDirectory(toolDir);
        var manifestPath = Path.Combine(toolDir, "manifest.json");
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(tool, new JsonSerializerOptions { WriteIndented = true }));

        _tools.Add(tool);
        return tool;
    }

    /// <summary>Removes a previously added user tool, both from memory and disk.</summary>
    public void RemoveUserTool(string id)
    {
        _tools.RemoveAll(t => t.Id == id);
        var toolDir = Path.Combine(SettingsService.GetUserToolsPath(), id);
        if (Directory.Exists(toolDir))
        {
            try { Directory.Delete(toolDir, recursive: true); } catch { /* ignore */ }
        }
    }

    public List<ToolDefinition> Search(string query, ToolType? filter = null)
    {
        var results = _tools.Where(t => t.MatchesSearch(query));
        if (filter.HasValue)
            results = results.Where(t => t.Type == filter.Value);
        return results.ToList();
    }

    private void LoadFromRegistry(string path)
    {
        try
        {
            var list = JsonSerializer.Deserialize<List<ToolDefinition>>(File.ReadAllText(path), Opts);
            if (list != null) AddRange(list);
        }
        catch { /* ignore malformed registry */ }
    }

    private void ScanManifests(string root)
    {
        foreach (var dir in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories))
        {
            var manifest = Path.Combine(dir, "manifest.json");
            if (!File.Exists(manifest)) continue;
            try
            {
                var tool = JsonSerializer.Deserialize<ToolDefinition>(File.ReadAllText(manifest), Opts);
                if (tool != null) AddRange(new[] { tool });
            }
            catch { /* skip invalid manifest */ }
        }
    }

    private void AddRange(IEnumerable<ToolDefinition> tools)
    {
        foreach (var t in tools)
            if (!_tools.Any(x => x.Id == t.Id))
                _tools.Add(t);
    }
}
