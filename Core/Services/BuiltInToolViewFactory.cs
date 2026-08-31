using MiniT.Core.Models;
using MiniT.UI.Pages;
using Microsoft.UI.Xaml.Controls;

namespace MiniT.Core.Services;

/// <summary>
/// Maps tool Entry string → WinUI Page.
/// Add new tools here without touching MainWindow or any other file.
/// </summary>
public class BuiltInToolViewFactory : IToolViewFactory
{
    public Page? CreateView(ToolDefinition tool) => tool.Entry switch
    {
        "UuidGeneratorPage" => new UuidGeneratorPage(),
        _ => null
    };
}
