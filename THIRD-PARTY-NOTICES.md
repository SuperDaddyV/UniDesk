# Third-party notices

This file describes the third-party components distributed in the UniDesk
2.1.0 self-contained Windows installer. The complete license texts and notices
listed below are installed in the `licenses` directory.

## .NET 10 Runtime and Microsoft libraries

- Components: Microsoft.NETCore.App Runtime 10.0.10,
  Microsoft.WindowsDesktop.App Runtime 10.0.10, Microsoft.Data.Sqlite 10.0.10,
  Microsoft.Extensions.DependencyInjection 10.0.10,
  Microsoft.Extensions.Hosting 10.0.10,
  Microsoft.Extensions.Hosting.WindowsServices 10.0.10 and
  System.Management 10.0.10 (the hardware-service graph also contains 10.0.2).
- License: MIT.
- Files: `DotNet-Runtime-LICENSE.txt`,
  `WindowsDesktop-Runtime-LICENSE.txt` and
  `DotNet-Runtime-THIRD-PARTY-NOTICES.txt`.

The .NET third-party-notices payload also covers the common Microsoft runtime
and extensions code distributed by the self-contained application and service.

## CommunityToolkit.Mvvm 8.4.2

- Project: https://github.com/CommunityToolkit/dotnet
- License: MIT.
- Files: `CommunityToolkit.Mvvm-LICENSE.txt` and
  `CommunityToolkit.Mvvm-THIRD-PARTY-NOTICES.txt`.

## Hardcodet.NotifyIcon.Wpf 1.1.0

- Project: https://github.com/hardcodet/wpf-notifyicon
- Copyright: Philipp Sumi and contributors.
- License: Code Project Open License 1.02.
- File: `Hardcodet.NotifyIcon.Wpf-CPOL-1.02.txt`.

## SQLitePCLRaw.bundle_e_sqlite3 2.1.12, SQLitePCLRaw and SQLite

- Project: https://github.com/ericsink/SQLitePCL.raw
- Copyright: SourceGear, LLC.
- License for SQLitePCLRaw: Apache License 2.0.
- SQLite itself is dedicated to the public domain by its authors.
- File: `SQLitePCLRaw-Apache-2.0.txt`.

## LibreHardwareMonitorLib 0.9.6 and runtime dependencies

- Project: https://github.com/LibreHardwareMonitor/LibreHardwareMonitor
- Release source: https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/tree/v0.9.6
- License: Mozilla Public License 2.0.
- Files: `LibreHardwareMonitor-MPL-2.0.txt` and the packaged
  `LibreHardwareMonitor-THIRD-PARTY-NOTICES.txt`.

The hardware-service dependency graph also distributes the following MPL 2.0
components. These stable commit links provide the corresponding Source Code Form
identified by the package repository metadata; UniDesk distributes the upstream
binaries without modifying these components.

- DiskInfoToolkit 1.1.2:
  https://github.com/Blacktempel/DiskInfoToolkit/commit/25319eae5781e75bcf141e844ceab2afe94d40ea
- RAMSPDToolkit-NDD 1.4.2:
  https://github.com/Blacktempel/RAMSPDToolkit/commit/3b47b960e0830fef344624ad5e389675d5f0a1ce
- BlackSharp.Core 1.0.7:
  https://github.com/Blacktempel/BlackSharp/commit/c70b735c6cec123ee8a046ac4a0bc6c606f52cf0

Their applicable license text is provided by
`LibreHardwareMonitor-MPL-2.0.txt`. HidSharp 2.6.4 is distributed under Apache
2.0 and Mono.Posix.NETStandard 1.0.0 under the MIT license; their texts are
provided by `HidSharp-Apache-2.0.txt` and
`Mono.Posix.NETStandard-LICENSE.txt`.

## QWeather Icons 1.8.0

- Project: https://github.com/qwd/Icons
- Copyright: QWeather.
- License: MIT for code and font support files; Creative Commons Attribution
  4.0 International for the weather icons.
- File: `QWeather-Icons-LICENSE.txt`.

UniDesk uses the unmodified icons and provides visible QWeather attribution in
the expanded and collapsed weather displays.

## PawnIO 2.1.0.0

- Project: https://github.com/namazso/PawnIO
- Binary source: `LibreHardwareMonitor/Resources/PawnIO_setup.exe` from the
  official LibreHardwareMonitor `v0.9.6` source archive.
- SHA-256: `a3a46226c5e2824f4cdd42be0eecbabfc672c86f7889710f5ab1e6ad385b47a0`.
- Authenticode signer: `namazso.eu`.
- Corresponding source revision:
  https://github.com/namazso/PawnIO/tree/5cdf470831fdfff3f7f1d06363ca6b230f3bf35a
- License: GNU GPL version 2 or later, with the upstream special exception for
  independent modules that communicate with PawnIO solely through the device IO control interface.
  The exception does not extend to modules loaded through the Pawn interface.
- Files: `PawnIO-COPYING.txt` and `PawnIO-LICENSE-EXCEPTION.txt`.

PawnIO is installed as a shared system component only when the user keeps the
installer task selected. UniDesk removes its own Windows service on uninstall,
but preserves PawnIO by default because other hardware tools may also use it.
