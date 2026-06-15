; UniDesk Inno Setup installer script
; Build the app first, then compile this script with Inno Setup.
;
; Recommended build command:
;   dotnet publish UniDesk\UniDesk.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o publish\win-x64

#define MyAppName "UniDesk"
#define MyAppVersion "1.3.7"
#define MyAppPublisher "UniDesk"
#define MyAppURL "https://github.com/SuperDaddyV/UniDesk"
#define MyAppExeName "UniDesk.exe"
#define MyAppSourceDir "publish\win-x64"
#define MyAppIconSourceDir "UniDesk\icon"
#define MyAppIconName "unidesk1-removebg-preview.ico"
#define MyAppMutex "UniDesk_SingleInstance_Mutex_6B9BD6F1-8E3A-4C5D-9F2B-1A7C8D3E5F9A"

[Setup]
; AppId uniquely identifies this application. Do not reuse it for other apps.
AppId={{4B0F3B03-7F5D-4B5D-B2F4-6816B931C7D2}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=installer
OutputBaseFilename={#MyAppName}_Setup_{#MyAppVersion}
Compression=lzma
SolidCompression=yes
WizardStyle=modern
ShowLanguageDialog=no
CloseApplications=yes
RestartApplications=no
AppMutex={#MyAppMutex}
SetupIconFile={#MyAppIconSourceDir}\{#MyAppIconName}
UninstallDisplayIcon={app}\icon\{#MyAppIconName}

[Languages]
Name: "chinesesimp"; MessagesFile: "installer-assets\ChineseSimplified.isl"

[CustomMessages]
chinesesimp.LaunchProgram=启动 %1

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startupicon"; Description: "开机自动启动"; GroupDescription: "启动选项"; Flags: unchecked

[Dirs]
; UniDesk stores user data in LocalAppData. Keep these directories after uninstall by default.
Name: "{localappdata}\UniDesk"
Name: "{localappdata}\UniDesk\icons"
Name: "{localappdata}\UniDesk\logs"
Name: "{localappdata}\UniDesk\cache"

[Files]
; Package the release output, including dlls, runtimeconfig, deps and native runtimes.
Source: "{#MyAppSourceDir}\*"; DestDir: "{app}"; Excludes: "icon\*"; Flags: ignoreversion recursesubdirs createallsubdirs
; Package the complete icon assets from the project source directory.
Source: "{#MyAppIconSourceDir}\*"; DestDir: "{app}\icon"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\icon\{#MyAppIconName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\icon\{#MyAppIconName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#MyAppName}"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: startupicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent runasoriginaluser

[UninstallRun]
Filename: "{sys}\schtasks.exe"; Parameters: "/Delete /TN ""\{#MyAppName}"" /F"; Flags: runhidden; RunOnceId: "DeleteUniDeskTask"
Filename: "{sys}\schtasks.exe"; Parameters: "/Delete /TN ""\LumiDesk"" /F"; Flags: runhidden; RunOnceId: "DeleteLegacyLumiDeskTask"
Filename: "{sys}\schtasks.exe"; Parameters: "/Delete /TN ""\VsirDesk"" /F"; Flags: runhidden; RunOnceId: "DeleteLegacyVsirDeskTask"
