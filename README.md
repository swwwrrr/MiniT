<div align="center">

# MiniT — Mini Tools Launcher

**A clean, fast, extensible WinUI 3 launcher for your everyday tools.**

`v0.1.0` · Windows · .NET 8 · WinUI 3

</div>

---

MiniT is a lightweight desktop shell that lets you organize small tools — web-based or local — into **Spaces**, open them in tabs, and access them instantly with keyboard-driven search.

It is designed for everyday utilities such as converters, generators, dashboards, local HTML tools, and other small applications that you would otherwise keep as separate browser tabs or programs.

## Features

* 🗂️ **Spaces** — group tools into custom workspaces
* 🧩 **Tool cards with folders** — organize tools visually
* 🌐 **Local & Online tools** — run both web-based and local HTML tools through WebView2
* 📇 **Tool Registry** — JSON-based tool definitions
* 🪟 **Tabs** — keep multiple tools open at once
* 🔍 **Instant search** (`Ctrl+K`) — quickly find and open tools
* ⭐ **Favorites** — mark frequently used tools
* 🎨 **Light / Dark / System theme**
* ⌨️ **Keyboard-first navigation**
* 🧪 **UUID Generator** — included built-in tool
* 💾 **Local data only** — user data is stored locally in `%LocalAppData%\MiniT\`
* 🧱 **MVVM** — built with CommunityToolkit.Mvvm

## Requirements

* Windows 10 (build 19041+) or Windows 11
* [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
* [Windows App SDK 1.6](https://learn.microsoft.com/windows/apps/windows-app-sdk/downloads)
* Visual Studio 2022 (17.8+) **or** the `dotnet` CLI

## Getting Started

Clone the repository:

```bash
git clone https://github.com/sstce/MiniT.git
cd MiniT
```

Restore dependencies:

```bash
dotnet restore
```

Build for x64:

```bash
dotnet build -f net8.0-windows10.0.19041.0 -r win-x64
```

Run:

```bash
dotnet run -f net8.0-windows10.0.19041.0 -r win-x64
```

Alternatively, open `MiniT.csproj` in **Visual Studio 2022**, select the **x64** platform, and press **F5**.

## Keyboard Shortcuts

| Key                 | Action                                  |
| ------------------- | --------------------------------------- |
| `Ctrl+K`            | Open search                             |
| `Escape`            | Close search / close active tab         |
| `Ctrl+1` … `Ctrl+3` | Switch to the 1st / 2nd / 3rd open tool |

## Tool Types

MiniT supports two main tool types: **Online** and **Local**.

| Type                 | What it is                                             | How it opens                                |
| -------------------- | ------------------------------------------------------ | ------------------------------------------- |
| **Online**           | A tool available at a web URL                          | Embedded in WebView2                        |
| **Local**            | A local HTML tool stored on disk                       | Embedded in WebView2 from a local HTML file |
| **Local (built-in)** | A tool implemented directly in MiniT using XAML and C# | Rendered natively as a WinUI page           |

`Local (built-in)` is technically represented by the same `Local` tool type. MiniT determines how to open it based on the tool definition: local HTML tools provide a local entry path, while built-in tools use an `Entry` value that maps to a page registered in `BuiltInToolViewFactory`.

## Adding Tools

### From the UI

The **Add Tool** button allows you to add Online and Local HTML tools.

#### Online tools

Provide:

* Name
* URL
* Description
* Category

The tool is opened inside MiniT using WebView2.

#### Local tools

Provide:

* Name
* Description
* Category
* A folder containing the tool

MiniT searches the selected folder for `index.html` or `index.htm`. If neither is found, it can fall back to another `.html` file, including files one level deep in folders such as `dist` or `build`.

The selected tool is then opened through WebView2.

User-added tools are stored under:

```text
%LocalAppData%\MiniT\Tools\<id>\
```

with their corresponding `manifest.json`.

## Built-in Tools

Built-in tools are implemented directly inside the MiniT source code using XAML and C#.

To add a new built-in tool:

1. Create a new page in `UI/Pages/`:

```text
UI/Pages/MyToolPage.xaml
UI/Pages/MyToolPage.xaml.cs
```

2. Register the page in `BuiltInToolViewFactory.cs`:

```csharp
"MyToolPage" => new MyToolPage(),
```

3. Add the corresponding tool definition to `Registry/tools.json` if it should be included in the tool registry.

4. Add the tool to a Space through MiniT's configuration.

## Architecture

```text
MiniT/
├── Core/
│   ├── Models/
│   │   ├── MiniTSettings.cs
│   │   ├── SpaceDefinition.cs
│   │   └── ToolDefinition.cs
│   └── Services/
│       ├── BuiltInToolViewFactory.cs
│       ├── SettingsService.cs
│       ├── SpaceService.cs
│       ├── ToolLauncher.cs
│       └── ToolRegistry.cs
│
├── UI/
│   ├── Controls/
│   │   ├── SimpleWrapPanel.cs
│   │   ├── ToolCard.xaml
│   │   └── ToolCard.xaml.cs
│   ├── Helpers/
│   │   ├── AddToSpaceMenu.cs
│   │   └── Motion.cs
│   ├── Pages/
│   │   ├── SettingsPage.xaml
│   │   ├── SpaceContentPage.xaml
│   │   ├── UuidGeneratorPage.xaml
│   │   └── WebToolPage.xaml
│   └── ViewModels/
│       ├── MainViewModel.cs
│       ├── SpaceViewModel.cs
│       ├── TabItem.cs
│       ├── ToolCardVM.cs
│       └── ToolSearchItemVM.cs
│
├── Registry/
│   └── tools.json
│
├── App.xaml
├── MainWindow.xaml
├── MiniT.csproj
└── app.manifest
```

## Data Location

MiniT keeps user data locally:

```text
%LocalAppData%\MiniT\
├── config\
│   ├── settings.json
│   └── spaces.json
│
└── Tools\
    └── <user-installed-tool>\
        └── manifest.json
```

MiniT does not upload or synchronize this data.

## Contributing

Contributions, bug reports, and feature ideas are welcome.

If you find a bug or have an idea for MiniT, feel free to open an issue or submit a pull request.

## License

MiniT is licensed under the MIT License.
