namespace MiniT.Core.Models;

public class FolderDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "New Folder";
    public List<string> ToolIds { get; set; } = new();
    public int Order { get; set; }
}

public class SpaceDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "New Space";
    public int Order { get; set; }
    public List<string> ToolIds { get; set; } = new();
    public List<FolderDefinition> Folders { get; set; } = new();
    public List<string> FavoriteToolIds { get; set; } = new();
    public bool IsPinned { get; set; }
}

public class SpacesData
{
    public List<SpaceDefinition> Spaces { get; set; } = new();
}
