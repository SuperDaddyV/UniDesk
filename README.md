# UniDesk

UniDesk is a lightweight, customizable, and clean Windows desktop sidebar that brings time and weather, hardware monitoring, shortcuts, todos, quick notes, and quick text into one convenient desktop workspace.

<p align="center">
  English ·
  <a href="README.zh-CN.md">简体中文</a> ·
  <a href="README.ja-JP.md">日本語</a> ·
  <a href="README.es-ES.md">Español</a>
</p>

![UniDesk product showcase](images/unidesk-hero.png)

## ✨ Key Features

### Time & Weather

- Shows the current time, date, and lunar calendar information.
- Shows weather, temperature, air quality, humidity, and city information.
- Includes a desktop calendar for quick solar and lunar date lookup.

### Hardware Monitor

- Monitors CPU, memory, and GPU usage in real time.
- Shows CPU / GPU temperature.
- Shows whole-machine upload / download network speed.
- GPU temperature is read from available driver and hardware-monitoring sources when possible; unavailable values are shown safely as `--`.

### Shortcuts

- Add frequently used apps, files, and folders.
- Drag apps, files, or folders from the desktop or File Explorer to add them.
- Reorder shortcuts freely.
- Customize how many shortcuts are shown on the main panel.

### Todos

- Create, edit, complete, and delete todo items.
- Show due time and priority.
- Store data locally for daily task tracking.

### Quick Notes

- Manage multiple notes.
- Supports auto save, pinning, copy, and delete.
- Useful for temporary ideas, drafts, meeting notes, and reminders.

### Quick Text

- Supports clipboard history.
- Supports reusable text snippets.
- Supports one-click copy.
- Includes sensitive-content filtering to reduce accidental storage of verification codes, passwords, tokens, cookies, and similar text.

### Module Management

- Show or hide modules.
- Reorder modules freely.
- Build a desktop panel that matches your own workflow.

### Personalization

- Adjust theme colors, window transparency, panel width, panel height, and font size.
- Customize the top display title.
- Supports topmost mode, lock, collapse, startup launch, and shortcut count settings.
- Restore the default layout or default settings when needed.

### Backup & Restore

- Supports local data backup.
- Supports restoring todos, quick notes, clipboard history, and text snippets.
- Helps recover commonly used data after reinstalling Windows or moving to another PC.

## 🖼️ Preview

### Core Features

![UniDesk feature overview](images/unidesk-features.png)

### Personalization

![UniDesk personalization preview](images/unidesk-customization.png)

## 🚀 Who Is It For?

UniDesk is for Windows users who want a clean desktop while keeping quick access to information, tools, todos, and notes.

Common use cases:

- Daily office work
- Personal productivity
- Desktop quick launch
- System status monitoring
- Lightweight todos and notes
- Quick copy of frequently used text

## 📦 Installation

Download the latest installer from [GitHub Releases](https://github.com/SuperDaddyV/UniDesk/releases/latest).

Current installer example:

```powershell
UniDesk_Setup_1.3.7.exe
```

It is recommended to exit any running UniDesk instance before installing or upgrading.

System requirements:

- Windows 10 version 1903 or later
- Windows 11

## 🛠️ Build From Source

Requirements:

- .NET 9 SDK
- Windows 10 version 1903 or later
- Visual Studio 2022, JetBrains Rider, or another .NET / WPF-capable development environment
- Inno Setup 6, only required for building the installer

Build and run:

```powershell
git clone https://github.com/SuperDaddyV/UniDesk.git
cd UniDesk

dotnet restore UniDesk.sln
dotnet build UniDesk.sln -c Release
dotnet run --project UniDesk\UniDesk.csproj
```

Publish:

```powershell
dotnet publish .\UniDesk\UniDesk.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o publish\win-x64
```

Build the installer:

```powershell
ISCC.exe .\UniDesk.iss
```

The installer will be generated in the `installer` directory.

## 🧰 Tech Stack

| Technology | Purpose |
| --- | --- |
| .NET 9 | Application runtime |
| WPF | Windows desktop UI |
| SQLite | Local data storage |
| CommunityToolkit.Mvvm | UI and data binding helpers |
| LibreHardwareMonitorLib | Hardware information reading |
| Hardcodet.NotifyIcon.Wpf | System tray support |
| Inno Setup | Windows installer |

## 🔐 Data & Privacy

UniDesk is local-first. User data is stored on the local machine, including settings, shortcuts, todos, quick notes, quick text, and icon cache.

Clipboard history includes sensitive-content filtering to reduce accidental storage of verification codes, passwords, tokens, cookies, and similar text. This lowers risk, but it should not be treated as an absolute security guarantee. If you handle highly sensitive content, consider disabling clipboard history or clearing it regularly.

## 🆕 Highlights

Recent versions include:

- Module management with show / hide and ordering.
- Shortcut drag-to-add and free ordering.
- Quick Notes with multiple notes, auto save, pinning, and copy.
- Quick Text with clipboard history, text snippets, and sensitive-content filtering.
- Improved hardware monitor layout for CPU, memory, GPU, temperature, and RX / TX network speed.
- Improved GPU temperature reading for more hardware and driver environments.
- Improved personalization settings and main panel scrolling.

## 📌 Roadmap

- More theme presets.
- More detailed hardware information.
- More flexible module extension options.
- Better installation and update experience.

## 🙏 Credits

UniDesk is developed based on [Happyeveryweek/LumiDesk](https://github.com/Happyeveryweek/LumiDesk). Thanks to the original author for the idea, foundation, and desktop widget experience.

## 📄 License

This project is licensed under the [MIT License](LICENSE). Please also respect the licenses and copyright notices of the original project and third-party dependencies.
