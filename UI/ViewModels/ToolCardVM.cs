using MiniT.Core.Models;

namespace MiniT.UI.ViewModels;

public class ToolCardVM
{
    public ToolDefinition Tool { get; }
    public string Name => Tool.Name;
    public string Description => Tool.Description;
    public string TypeLabel => Tool.Type.ToString();
    public bool IsFavorite { get; set; }
    public string Initials => Tool.Name.Split(' ')
        .Select(w => w.Length > 0 ? w[0].ToString() : "")
        .Take(2).Aggregate("", (a, b) => a + b);

    public ToolCardVM(ToolDefinition tool, bool isFavorite)
    {
        Tool = tool;
        IsFavorite = isFavorite;
    }
}

public class FolderCardVM
{
    public string Name { get; }
    public List<ToolCardVM> Tools { get; }

    public FolderCardVM(FolderViewModel folder, Func<ToolDefinition, bool> isFavorite)
    {
        Name = folder.Name;
        Tools = folder.Tools.Select(t => new ToolCardVM(t, isFavorite(t))).ToList();
    }
}
