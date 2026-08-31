using System.Text.Json;
using MiniT.Core.Models;

namespace MiniT.Core.Services;

public class SettingsService
{
    private static readonly string AppDataPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MiniT");
    private static readonly string ConfigPath = Path.Combine(AppDataPath, "config");
    private static readonly string SettingsFile = Path.Combine(ConfigPath, "settings.json");
    private static readonly string SpacesFile = Path.Combine(ConfigPath, "spaces.json");

    private static readonly JsonSerializerOptions Opts = new() { WriteIndented = true };

    public static string GetUserToolsPath() => Path.Combine(AppDataPath, "Tools");

    public MiniTSettings LoadSettings()
    {
        EnsureDirs();
        if (!File.Exists(SettingsFile)) return new MiniTSettings();
        try { return JsonSerializer.Deserialize<MiniTSettings>(File.ReadAllText(SettingsFile)) ?? new(); }
        catch { return new(); }
    }

    public void SaveSettings(MiniTSettings settings)
    {
        EnsureDirs();
        File.WriteAllText(SettingsFile, JsonSerializer.Serialize(settings, Opts));
    }

    public SpacesData LoadSpaces()
    {
        EnsureDirs();
        if (!File.Exists(SpacesFile)) return CreateDefaultSpaces();
        try
        {
            var data = JsonSerializer.Deserialize<SpacesData>(File.ReadAllText(SpacesFile));
            return data ?? CreateDefaultSpaces();
        }
        catch { return CreateDefaultSpaces(); }
    }

    public void SaveSpaces(SpacesData data)
    {
        EnsureDirs();
        File.WriteAllText(SpacesFile, JsonSerializer.Serialize(data, Opts));
    }

    private static SpacesData CreateDefaultSpaces()
    {
        var spaces = new SpacesData
        {
            Spaces = new List<SpaceDefinition>
            {
                new() { Name = "Work",     Order = 0,
                    Folders = new() {
                        new() { Name = "Generators", ToolIds = new() { "uuid-generator" }, Order = 0 }
                    },
                    ToolIds = new() { "uuid-generator" }
                },
                new() { Name = "Personal", Order = 1 },
                new() { Name = "School",   Order = 2 },
            }
        };
        return spaces;
    }

    private static void EnsureDirs()
    {
        Directory.CreateDirectory(ConfigPath);
        Directory.CreateDirectory(GetUserToolsPath());
    }
}
