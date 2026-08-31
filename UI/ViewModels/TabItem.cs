using CommunityToolkit.Mvvm.ComponentModel;
using MiniT.Core.Models;
using Microsoft.UI.Xaml.Controls;

namespace MiniT.UI.ViewModels;

public partial class TabItem : ObservableObject
{
    public string ToolId { get; }
    public string Title { get; }
    public Page Content { get; }

    [ObservableProperty] private bool _isActive;

    public TabItem(ToolDefinition tool, Page content)
    {
        ToolId = tool.Id;
        Title = tool.Name;
        Content = content;
    }
}
