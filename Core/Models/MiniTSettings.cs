namespace MiniT.Core.Models;

public enum AppTheme { System, Light, Dark }

public class MiniTSettings
{
    public AppTheme Theme { get; set; } = AppTheme.System;
    public string LastActiveSpaceId { get; set; } = "";
    public bool ShowToolTypeLabel { get; set; } = true;
    public bool IsSidebarCollapsed { get; set; }
    public double SidebarWidth { get; set; } = 248;
}
