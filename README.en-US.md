# UniDesk

UniDesk is a lightweight, customizable, and clean Windows desktop sidebar that brings time and weather, hardware monitoring, Model Radar, shortcuts, todos, quick notes, and quick text into one convenient desktop workspace.

<p align="center">
  <a href="README.md">简体中文</a> ·
  English ·
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

### Model Radar

- Leads with Overall leader and Value recommendation decision cards, followed by Overall / Backend / Frontend / Knowledge Top 5 reference lists.
- Data comes from [ModelDial Radar](https://modeldial.com/radar) public evaluations ([CC BY 4.0](https://modeldial.com/data-license)). Value recommendation uses only the publisher's official `value` tag; UniDesk does not recompute scores or costs.
- Disabled by default and makes no network requests while disabled. While UniDesk is running and the module is enabled, it loads local cache first, checks for updates at most once every six hours through `https://modeldial.com/api/v1/radar/latest.json`, and also supports manual refresh.
- This is a read-only decision reference: UniDesk does not call models, run local evaluations, or change model-tool configuration. Public evaluation data does not imply partnership or endorsement; actual performance may vary by account, route, and endpoint.

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

### Compact Dashboard

Collapsed mode keeps the time, weather, hardware status, and next todo visible while UniDesk stays quietly on the desktop.

![UniDesk compact dashboard](images/unidesk-compact-dashboard.png)

### Core Features

![UniDesk feature overview](images/unidesk-features.png)

### Model Radar

Two decision cards identify the current overall leader and ModelDial's official value recommendation before the task-specific Top 5 lists.

![UniDesk Model Radar showcase](images/unidesk-model-radar.png)

The showcase uses the public batch from August 30, 2026; in-app results update with ModelDial Radar data.

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

The official distribution point for the `v2.2.1` stable installer is the [v2.2.1 GitHub Release](https://github.com/SuperDaddyV/UniDesk/releases/tag/v2.2.1). This exact version uses a separately approved unsigned-stable exception. The installer is `Authenticode: NotSigned`, so Windows may show a SmartScreen or enterprise-policy prompt. Download it from the official Release and verify the published SHA-256. The installer filename is:

```powershell
UniDesk_Setup_2.2.1.exe
```

It is recommended to exit any running UniDesk instance before installing or upgrading.

Double-click the installer normally and approve the Windows UAC prompt; a standard user must supply administrator credentials, with no need to use **Run as administrator**. The main application can be installed in a safe directory on any local drive, and an upgrade reuses the previous location by default. The hardware service, repair tool, and uninstaller remain in the protected Windows `Common Program Files\UniDesk` directory. Network paths, drive roots, reparse-point paths, and unrelated non-empty directories are not accepted.

The installer selects both the desktop shortcut and complete hardware monitoring by default and clearly discloses that it installs the shared PawnIO driver and a read-only Windows service running as `LocalSystem`. `UniDesk.exe` itself remains a normal-user process. The completion page selects launching UniDesk by default and starts it with the original normal-user identity. Clearing the component or a component installation failure leaves weather, notes, shortcuts, and other base features available, but low-level metrics such as CPU temperature may be unavailable; diagnostics and repair remain available from Settings.

System requirements:

- A supported Windows 11 x64 release
- Windows 10 Enterprise or IoT Enterprise LTSC 2021 or later on x64

The project keeps a Windows 10 version 1903 API compatibility baseline. LTSC 2019 is below that baseline, and out-of-support consumer Windows 10 releases are not part of the official support promise.

## 🛠️ Build From Source

Requirements:

- .NET 10 SDK
- A supported Windows 11 or Windows 10 LTSC development environment
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

Build an unsigned local release candidate from a clean worktree:

```powershell
.\scripts\Build-Release.ps1 -Version 2.2.1
```

The script publishes the application, hardware service, and repair helper into a fresh versioned directory before compiling the installer from those exact inputs. An unsigned `v2.2.1` artifact built from a clean worktree must pass `Test-UnsignedReleaseReadiness.ps1`, the applicable manual matrix, and final project-owner approval before publication. The SignPath workflow remains available for future signed releases and never creates a GitHub Release automatically.

## 🧰 Tech Stack

| Technology | Purpose |
| --- | --- |
| .NET 10 LTS | Application runtime |
| WPF | Windows desktop UI |
| SQLite | Local data storage |
| CommunityToolkit.Mvvm | UI and data binding helpers |
| LibreHardwareMonitorLib | Hardware information reading |
| Hardcodet.NotifyIcon.Wpf | System tray support |
| Inno Setup | Windows installer |

## 🔐 Data & Privacy

UniDesk is local-first. User data is stored on the local machine, including settings, shortcuts, todos, quick notes, quick text, and icon cache.

Clipboard history includes sensitive-content filtering to reduce accidental storage of verification codes, passwords, tokens, cookies, and similar text. This lowers risk, but it should not be treated as an absolute security guarantee. If you handle highly sensitive content, consider disabling clipboard history or clearing it regularly.

## Code signing policy

Public packages follow the [code signing policy](CODE_SIGNING_POLICY.md) and [privacy policy](PRIVACY.md). `v2.2.1` uses a separately approved exception for this exact version: it must come from the exact public revision, receive manual approval, pass complete payload and SHA-256 checks, and clearly state `Authenticode: NotSigned` plus the possibility of a Windows SmartScreen or enterprise-policy prompt.

Free code signing provided by SignPath.io, certificate by SignPath Foundation.

## 🆕 Highlights

Recent versions include:

- Model Radar with the overall leader, official value recommendation, and Overall / Backend / Frontend / Knowledge Top 5 lists.
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

This project is licensed under the [MIT License](LICENSE). The dependencies, license texts, and sources distributed by the self-contained installer are listed in [Third-party notices](THIRD-PARTY-NOTICES.md).
