<div align="center">

# MiniT — Mini Tools Launcher

**A clean, fast, extensible WinUI 3 launcher for your everyday tools.**

`v0.1.0` · Windows · .NET 8 · WinUI 3

</div>

---

MiniT is a lightweight desktop shell that lets you organize small tools — web-based or local — into **Spaces**, open them in tabs, and jump between them instantly with a keyboard-driven search. Think of it as a personal dashboard for the little utilities you use every day: converters, generators, internal dashboards, local HTML tools, and anything else you'd rather not hunt for in a pile of browser tabs.

## Features

- 🗂️ **Spaces** — group tools into custom workspaces, add/delete freely
- 🧩 **Tool cards with folders** — organize tools visually
- 🌐 **Local & Online tools** — embed a URL or a local HTML app, both run via WebView2
- 📇 **Tool Registry** — simple JSON manifest system, no rebuild required to add tools
- 🪟 **Tabs** — keep several tools open at once and switch between them
- 🔍 **Instant search** (`Ctrl+K`) — find and launch any tool without touching the mouse
- ⭐ **Favorites** — pin the tools you use most
- 🎨 **Light / Dark / System theme**
- ⌨️ **Keyboard-first navigation**
- 🧪 **UUID Generator** — included as a demo/reference tool
- 💾 **Local data only** — everything lives in `%LocalAppData%\MiniT\`, nothing phones home
- 🧱 **MVVM** — built with CommunityToolkit.Mvvm for a clean, testable structure

## Requirements

- Windows 10 (build 19041+) or Windows 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Windows App SDK 1.6](https://learn.microsoft.com/windows/apps/windows-app-sdk/downloads)
- Visual Studio 2022 (17.8+) **or** the `dotnet` CLI

## Getting Started

```bash
git clone https://github.com/sstce/MiniT.git
cd MiniT

# Restore packages
dotnet restore

# Build (x64)
dotnet build -f net8.0-windows10.0.19041.0 -r win-x64

# Run
dotnet run -f net8.0-windows10.0.19041.0 -r win-x64
```

Or just open `MiniT.csproj` in **Visual Studio 2022**, select the **x64** platform, and hit **F5**.

## Keyboard Shortcuts

| Key | Action |
|---|---|
| `Ctrl+K` | Open search |
| `Escape` | Close search / close active tab |
| `Ctrl+1` … `Ctrl+3` | Switch to the 1st / 2nd / 3rd open tool |

## Tool Types

MiniT's naming here is a bit subtle, so it's worth spelling out. There are really only two values in the `ToolType` enum that matter day-to-day — **Online** and **Local** — but "Local" itself covers two very different situations depending on one extra field:

| Type | What it is | How it opens |
|---|---|---|
| **Online** | A tool that lives at a URL | Embedded via WebView2, pointed at the URL |
| **Local (site)** | A tool on disk with an `index.html` (something you built with plain HTML/JS, or a `dist`/`build` folder), added via **Add Tool → Browse…** | Embedded via WebView2, pointed at that local file. `ToolDefinition.LocalEntryPath` is set. |
| **Local (built-in)** | A tool that is part of MiniT itself — a real XAML page written in C#, like the bundled UUID Generator | Rendered natively as a Page, found via `Entry`. No WebView2, and `LocalEntryPath` is left empty. |

In other words: both "Local (site)" and "Local (built-in)" carry `Type: Local` in their `manifest.json` — what actually decides *how* MiniT opens them is whether `LocalEntryPath` is set. If it is, MiniT loads that HTML file in WebView2. If it isn't, MiniT looks up `Entry` in `BuiltInToolViewFactory.cs` and renders a native page instead. Keep this in mind when adding tools so you don't confuse "a Local tool the user pointed at a folder" with "a Local tool built into the app's source."

## Adding Tools

### From the UI (Online / Local)

The sidebar's **Add Tool** button handles both non-built-in types from one dialog:

- **Online** — provide a name, URL, description, and category.
- **Local** — provide a name, description, category, and pick a folder with **Browse…**. MiniT looks for `index.html` / `index.htm` in that folder (falling back to any `.html` file, including one level deep for `dist`/`build`-style output), then opens it the same way — embedded via WebView2, just pointed at the local file.

Both are saved as `manifest.json` under `%LocalAppData%\MiniT\Tools\<id>\` and are picked up automatically on next launch.

### Adding a Built-in Tool

This is for tools you want to ship as part of MiniT's source code, not something end users add via the UI:

1. Create `UI/Pages/MyToolPage.xaml` + `MyToolPage.xaml.cs`
2. Register it in `BuiltInToolViewFactory.cs`:
   ```csharp
   "MyToolPage" => new MyToolPage(),
   ```
3. (Optional) Add an entry to `Registry/tools.json` if you want it discoverable/searchable the same way as other tools, using `"entry": "MyToolPage"` to point at the factory key above.
4. Add the tool to a Space in `%LocalAppData%\MiniT\config\spaces.json`

## Architecture

```
MiniT/
├── Core/
│   ├── Models/          # ToolDefinition, SpaceDefinition, Settings
│   └── Services/        # ToolRegistry, ToolLauncher, SpaceService, SettingsService
├── UI/
│   ├── Pages/           # UuidGeneratorPage, SpaceContentPage, SettingsPage, WebToolPage
│   ├── Controls/        # ToolCard, SimpleWrapPanel
│   └── ViewModels/      # MainViewModel, SpaceViewModel, ToolCardVM, TabItem, ToolSearchItemVM
├── Tools/
│   └── Local/UuidGenerator/manifest.json
├── Registry/tools.json  # optional global tool registry
└── App.xaml / MainWindow.xaml
```

## Data Location

MiniT keeps all user data local — nothing is uploaded or synced:

```
%LocalAppData%\MiniT\
├── config\
│   ├── settings.json    # theme, last active space
│   └── spaces.json      # spaces, folders, favorites
└── Tools\               # user-installed tool manifests
```

## Contributing

Contributions, bug reports, and feature ideas are welcome — feel free to open an issue or a pull request.

## License

MiniT is licensed under the MIT License.