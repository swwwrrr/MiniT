namespace MiniT.Core.Models;

public enum ToolType { Local, Online, External }

public class ToolDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Version { get; set; } = "1.0.0";
    public string Description { get; set; } = "";
    public ToolType Type { get; set; } = ToolType.Local;
    public string Category { get; set; } = "";
    public List<string> Keywords { get; set; } = new();
    public string Entry { get; set; } = "";
    public string? Url { get; set; }
    public bool IsFavorite { get; set; }

    /// <summary>For a user-added "local site" tool: absolute path to the HTML
    /// file (usually index.html) that gets loaded into WebView2. Distinguishes
    /// it from a built-in Local tool, which is rendered by a native Page found
    /// via <see cref="Entry"/> instead. Null/empty for every other tool type.</summary>
    public string? LocalEntryPath { get; set; }

    /// <summary>The folder the user picked for a local-site tool (its parent is
    /// <see cref="LocalEntryPath"/>). Kept mainly so it can be shown back to the
    /// user (e.g. "Reveal in Explorer"); not used for launching.</summary>
    public string? LocalFolderPath { get; set; }

    public bool MatchesSearch(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return true;
        var q = query.ToLowerInvariant();
        return Name.Contains(q, StringComparison.OrdinalIgnoreCase)
            || Description.Contains(q, StringComparison.OrdinalIgnoreCase)
            || Category.Contains(q, StringComparison.OrdinalIgnoreCase)
            || Keywords.Any(k => k.Contains(q, StringComparison.OrdinalIgnoreCase));
    }
}
