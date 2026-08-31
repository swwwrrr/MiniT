using MiniT.Core.Models;

namespace MiniT.UI.ViewModels;

public class ToolSearchItemVM
{
    public ToolDefinition Tool { get; }
    public string Name => Tool.Name;
    public string Description => Tool.Description;
    public string TypeLabel => Tool.Type.ToString();
    public string Initials => Tool.Name.Length > 0
        ? Tool.Name.Split(' ').Select(w => w[0].ToString()).Take(2).Aggregate((a, b) => a + b)
        : "?";

    public ToolSearchItemVM(ToolDefinition tool) => Tool = tool;
}
