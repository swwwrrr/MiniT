using MiniT.Core.Models;
using MiniT.UI.Pages;
using Microsoft.UI.Xaml.Controls;

namespace MiniT.Core.Services;

public interface IToolViewFactory
{
    Page? CreateView(ToolDefinition tool);
}

public class ToolLauncher
{
    private readonly IToolViewFactory _factory;

    public ToolLauncher(IToolViewFactory factory)
    {
        _factory = factory;
    }

    /// <summary>Returns a Page for the tool, or null if tool cannot be shown inline.</summary>
    public Page? Launch(ToolDefinition tool)
    {
        return tool.Type switch
        {
            ToolType.Local => LaunchLocal(tool),
            ToolType.Online => LaunchOnline(tool),
            ToolType.External => LaunchExternal(tool),
            _ => null
        };
    }

    /// <summary>A Local tool is either a bundled native page (looked up via
    /// <see cref="ToolDefinition.Entry"/>) or a user-added local site — a folder
    /// on disk with an HTML entry point, which opens the same embedded WebView2
    /// surface an Online tool uses, just pointed at a file:// URL instead.</summary>
    private Page? LaunchLocal(ToolDefinition tool)
    {
        if (!string.IsNullOrEmpty(tool.LocalEntryPath))
        {
            if (!File.Exists(tool.LocalEntryPath)) return null;
            var page = new WebToolPage();
            page.Navigate(new Uri(tool.LocalEntryPath).AbsoluteUri);
            return page;
        }
        return _factory.CreateView(tool);
    }

    /// <summary>Online tools run embedded inside MiniT via WebView2 — a real
    /// tab, not a hand-off to the system browser — same as a Local tool.</summary>
    private static Page? LaunchOnline(ToolDefinition tool)
    {
        if (string.IsNullOrEmpty(tool.Url)) return null;
        var page = new WebToolPage();
        page.Navigate(tool.Url);
        return page;
    }

    private static Page? LaunchExternal(ToolDefinition tool)
    {
        if (!string.IsNullOrEmpty(tool.Entry) && File.Exists(tool.Entry))
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(tool.Entry) { UseShellExecute = true });
        return null;
    }
}
