; UniDesk Inno Setup installer script
; Use scripts\Build-Release.ps1 for local release candidates. The signing workflow
; overrides all source and output directories so historical publish output is never reused.

#define MyAppName "UniDesk"
#define MyAppVersion "2.1.0"
#define MyAppPublisher "UniDesk"
#define MyAppURL "https://github.com/SuperDaddyV/UniDesk"
#define MyAppExeName "UniDesk.exe"
#ifndef MyAppSourceDir
  #define MyAppSourceDir "publish\win-x64-clean"
#endif
#ifndef MyHardwareServiceSourceDir
  #define MyHardwareServiceSourceDir "publish\hardware-service-win-x64-clean"
#endif
#ifndef MyHardwareRepairSourceDir
  #define MyHardwareRepairSourceDir "publish\hardware-repair-win-x64-clean"
#endif
#ifndef MyOutputDir
  #define MyOutputDir "installer"
#endif
#define MyAppIconSourceDir "UniDesk\icon"
#define MyAppIconName "unidesk1-removebg-preview.ico"
#define MyAppMutex "UniDesk_SingleInstance_Mutex_6B9BD6F1-8E3A-4C5D-9F2B-1A7C8D3E5F9A"
#define HardwareServiceName "UniDeskHardwareService"

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
DisableDirPage=no
UsePreviousAppDir=yes
UninstallFilesDir={commoncf}\UniDesk\Uninstall
AllowNetworkDrive=no
AllowUNCPath=no
AllowRootDirectory=no
SetupLogging=yes
PrivilegesRequired=admin
MinVersion=10.0.18362
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
OutputDir={#MyOutputDir}
OutputBaseFilename={#MyAppName}_Setup_{#MyAppVersion}
Compression=lzma
SolidCompression=yes
WizardStyle=modern
ShowLanguageDialog=yes
CloseApplications=yes
RestartApplications=no
UsePreviousTasks=no
AppMutex={#MyAppMutex}
SetupIconFile={#MyAppIconSourceDir}\{#MyAppIconName}
UninstallDisplayIcon={app}\icon\{#MyAppIconName}

[Languages]
Name: "chinesesimp"; MessagesFile: "installer-assets\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[CustomMessages]
chinesesimp.LaunchProgram=启动 %1
chinesesimp.CompleteHardwareTask=安装完整硬件监控组件（推荐，将安装 PawnIO 驱动和以 LocalSystem 运行的只读硬件监控服务）
chinesesimp.HardwareMonitoringGroup=硬件监控
chinesesimp.HardwareRepairFailed=UniDesk 基础程序已安装，但硬件监控组件未能完成安装或修复（退出码 %1）。基础功能仍可正常使用；请在 UniDesk 设置中导出硬件诊断或稍后重试。详细日志位于 ProgramData\UniDesk\logs\hardware-repair.log。
chinesesimp.HardwareCompatibilityMode=UniDesk 已安装，并将使用 Windows 和硬件厂商的兼容数据来源。CPU、GPU、内存和网络仍可显示；部分主板传感器可能缺失。
chinesesimp.ProtectedComponentLocationNotice=您可以选择主程序的安装位置；离线硬件修复与卸载组件仍会在系统受保护目录占用约 220 MB。
chinesesimp.HardwareServiceRemoveFailed=未能删除 UniDesk 硬件监控服务（退出码 %1）。卸载将继续；请查看 ProgramData\UniDesk\logs\hardware-repair.log 并以管理员身份删除 UniDeskHardwareService。
chinesesimp.HardwareServiceStopFailed=无法停止现有硬件监控服务（退出码 %1）。请关闭 UniDesk 后重试安装。安装日志：%2
chinesesimp.HardwareServiceOwnershipFailed=发现同名硬件服务，但它不属于当前 UniDesk 安装。为避免影响其他软件，安装已停止。安装日志：%1
chinesesimp.HardwareAclFailed=无法安全准备 UniDesk 系统组件目录。安装已停止，请检查 Windows 的 Common Files 目录后重试。安装日志：%1
chinesesimp.ApplicationLocationInvalid=所选主程序目录无法安全使用。请选择一个新的空目录，或原有的 UniDesk 安装目录。
chinesesimp.ApplicationLocationNetwork=主程序只能安装到本地固定磁盘，请不要选择网络盘或可移动磁盘。
chinesesimp.ApplicationLocationAclUnsupported=所选磁盘不支持 UniDesk 所需的安全权限，请选择 NTFS 或 ReFS 本地磁盘。
chinesesimp.LegacyUninstallerCleanupFailed=新版 UniDesk 已安装且可以正常卸载，但旧目录中的卸载组件未能清理。请不要运行旧卸载程序；确认新版正常后可手动删除旧程序文件夹。安装日志：%1
chinesesimp.ApplicationLocationMigrationComplete=UniDesk 已安装到新位置，并已清理可确认属于旧位置的启动项。安装器没有运行旧卸载程序或删除旧目录；确认新版正常后，可手动删除旧程序文件夹，用户数据不受影响。
chinesesimp.HardwareUnsafeServiceRetirementFailed=检测到旧版 UniDesk 在非受保护目录运行硬件服务，但无法确认它已安全停止并禁用。安装已停止；请不要继续使用旧硬件服务，并查看安装日志后手动删除 UniDeskHardwareService。安装日志：%1
chinesesimp.LegacyMigrationCleanupFailed=UniDesk 已安装到新位置，但未能完整清理旧位置的启动项。请不要运行旧卸载程序；请查看安装日志并确认旧版不再开机启动。安装日志：%1
chinesesimp.RemovePawnIoPrompt=是否同时卸载共享的 PawnIO 驱动？PawnIO 可能也被其他硬件监控或风扇控制软件使用。建议选择“否”并保留；只有确认没有其他程序依赖它时才选择“是”。
chinesesimp.PawnIoRemoveFailed=PawnIO 卸载失败。UniDesk 将继续卸载，但 PawnIO 会保留在系统中。
english.CompleteHardwareTask=Install complete hardware monitoring (recommended; installs the PawnIO driver and a read-only LocalSystem service)
english.HardwareMonitoringGroup=Hardware monitoring
english.HardwareRepairFailed=The UniDesk base application was installed, but hardware monitoring installation or repair did not complete (exit code %1). Base features remain available; export hardware diagnostics from Settings or retry later. Details are in ProgramData\UniDesk\logs\hardware-repair.log.
english.HardwareCompatibilityMode=UniDesk is installed and will use compatible Windows and hardware-vendor data sources. CPU, GPU, memory, and network data remain available; some motherboard sensors may be missing.
english.ProtectedComponentLocationNotice=You can choose the main app location. Offline hardware repair and uninstall files still use about 220 MB in a protected system directory.
english.HardwareServiceRemoveFailed=The UniDesk hardware service could not be removed (exit code %1). Uninstall will continue; review ProgramData\UniDesk\logs\hardware-repair.log and remove UniDeskHardwareService as an administrator.
english.HardwareServiceStopFailed=The existing hardware service could not be stopped (exit code %1). Close UniDesk and retry setup. Setup log: %2
english.HardwareServiceOwnershipFailed=A service with the same name exists but does not belong to this UniDesk installation. Setup stopped to avoid changing another application. Setup log: %1
english.HardwareAclFailed=The UniDesk system component directory could not be prepared securely. Setup stopped; check the Windows Common Files directory and retry. Setup log: %1
english.ApplicationLocationInvalid=The selected application directory cannot be used safely. Choose a new empty directory or an existing UniDesk installation directory.
english.ApplicationLocationNetwork=The main app can only be installed on a local fixed disk. Do not select a network or removable drive.
english.ApplicationLocationAclUnsupported=The selected disk does not support the security permissions UniDesk requires. Choose a local NTFS or ReFS disk.
english.LegacyUninstallerCleanupFailed=The new UniDesk installation can be uninstalled normally, but the old uninstall files could not be cleaned. Do not run the old uninstaller; after verifying the new version, you may delete the old program folder manually. Setup log: %1
english.ApplicationLocationMigrationComplete=UniDesk was installed in the new location and strictly owned startup entries for the previous location were cleaned. Setup did not run the old uninstaller or delete the old folder. After verifying the new version, you can delete the old program folder manually; user data is unaffected.
english.HardwareUnsafeServiceRetirementFailed=An older UniDesk hardware service is running from an unprotected directory, but setup could not confirm that it was safely stopped and disabled. Setup stopped; do not keep using the old service, and remove UniDeskHardwareService manually after reviewing the setup log. Setup log: %1
english.LegacyMigrationCleanupFailed=UniDesk was installed in the new location, but startup cleanup for the previous location did not complete. Do not run the old uninstaller; review the setup log and confirm that the old version no longer starts with Windows. Setup log: %1
english.RemovePawnIoPrompt=Also uninstall the shared PawnIO driver? Other hardware-monitoring or fan-control applications may use PawnIO. Keep it unless you are certain that no other application depends on it.
english.PawnIoRemoveFailed=PawnIO could not be uninstalled. UniDesk uninstall will continue and PawnIO will remain installed.
japanese.CompleteHardwareTask=完全なハードウェア監視をインストール（推奨。PawnIO ドライバーと LocalSystem の読み取り専用サービスをインストールします）
japanese.HardwareMonitoringGroup=ハードウェア監視
japanese.HardwareRepairFailed=UniDesk 本体はインストールされましたが、ハードウェア監視のインストールまたは修復を完了できませんでした（終了コード %1）。基本機能は使用できます。設定から診断をエクスポートするか、後でもう一度お試しください。詳細ログ：ProgramData\UniDesk\logs\hardware-repair.log。
japanese.HardwareCompatibilityMode=UniDesk はインストールされ、Windows とハードウェアベンダーの互換データソースを使用します。CPU、GPU、メモリ、ネットワークは引き続き表示されますが、一部のマザーボードセンサーは表示されない場合があります。
japanese.ProtectedComponentLocationNotice=メインアプリの場所を選択できます。オフライン修復とアンインストール用ファイルは、保護されたシステムフォルダーで約 220 MB 使用します。
japanese.HardwareServiceRemoveFailed=UniDesk ハードウェア監視サービスを削除できませんでした（終了コード %1）。アンインストールは続行します。ProgramData\UniDesk\logs\hardware-repair.log を確認し、管理者として UniDeskHardwareService を削除してください。
japanese.HardwareServiceStopFailed=既存のハードウェア監視サービスを停止できませんでした（終了コード %1）。UniDesk を終了してから再試行してください。セットアップログ：%2
japanese.HardwareServiceOwnershipFailed=同名のサービスがありますが、この UniDesk インストールのものではありません。他のアプリを変更しないよう、インストールを中止しました。セットアップログ：%1
japanese.HardwareAclFailed=UniDesk のシステムコンポーネント用フォルダーを安全に準備できませんでした。Windows の Common Files を確認してください。セットアップログ：%1
japanese.ApplicationLocationInvalid=選択したアプリフォルダーは安全に使用できません。新しい空のフォルダーまたは既存の UniDesk インストール先を選択してください。
japanese.ApplicationLocationNetwork=メインアプリはローカル固定ディスクにのみインストールできます。ネットワークドライブやリムーバブルドライブは選択しないでください。
japanese.ApplicationLocationAclUnsupported=選択したディスクは UniDesk に必要なセキュリティ権限をサポートしていません。ローカルの NTFS または ReFS ディスクを選択してください。
japanese.LegacyUninstallerCleanupFailed=新版 UniDesk は正常にアンインストールできますが、旧フォルダーのアンインストールファイルを削除できませんでした。旧アンインストーラーは実行せず、新版を確認後に旧プログラムフォルダーを手動削除してください。セットアップログ：%1
japanese.ApplicationLocationMigrationComplete=UniDesk を新しい場所にインストールし、旧場所に厳密に属するスタートアップ項目をクリーンアップしました。旧アンインストーラーの実行や旧フォルダーの削除は行っていません。新版を確認後、旧フォルダーを手動削除できます。ユーザーデータには影響しません。
japanese.HardwareUnsafeServiceRetirementFailed=保護されていない場所の旧版 UniDesk ハードウェアサービスを安全に停止して無効化できたことを確認できません。インストールを中止しました。ログを確認し、UniDeskHardwareService を手動で削除してください。セットアップログ：%1
japanese.LegacyMigrationCleanupFailed=UniDesk は新しい場所にインストールされましたが、旧場所のスタートアップ項目を完全にクリーンアップできませんでした。旧アンインストーラーは実行せず、ログを確認してください。セットアップログ：%1
japanese.RemovePawnIoPrompt=共有 PawnIO ドライバーもアンインストールしますか？他のハードウェア監視ソフトやファン制御ソフトが使用している場合があります。他のソフトが依存していないことを確認できる場合だけ削除してください。
japanese.PawnIoRemoveFailed=PawnIO をアンインストールできませんでした。UniDesk のアンインストールは続行し、PawnIO はシステムに残ります。
spanish.CompleteHardwareTask=Instalar supervisión completa de hardware (recomendado; instala el controlador PawnIO y un servicio de solo lectura LocalSystem)
spanish.HardwareMonitoringGroup=Supervisión de hardware
spanish.HardwareRepairFailed=La aplicación base UniDesk se instaló, pero no se completó la instalación o reparación de la supervisión de hardware (código %1). Las funciones básicas siguen disponibles; exporte el diagnóstico desde Configuración o vuelva a intentarlo más tarde. Registro: ProgramData\UniDesk\logs\hardware-repair.log.
spanish.HardwareCompatibilityMode=UniDesk está instalado y usará fuentes compatibles de Windows y del fabricante. Los datos de CPU, GPU, memoria y red seguirán disponibles; pueden faltar algunos sensores de la placa base.
spanish.ProtectedComponentLocationNotice=Puede elegir la ubicación de la aplicación. Los archivos de reparación sin conexión y desinstalación usan unos 220 MB en una carpeta protegida del sistema.
spanish.HardwareServiceRemoveFailed=No se pudo eliminar el servicio de hardware de UniDesk (código %1). La desinstalación continuará; revise ProgramData\UniDesk\logs\hardware-repair.log y elimine UniDeskHardwareService como administrador.
spanish.HardwareServiceStopFailed=No se pudo detener el servicio de hardware existente (código %1). Cierre UniDesk y vuelva a intentarlo. Registro de instalación: %2
spanish.HardwareServiceOwnershipFailed=Existe un servicio con el mismo nombre, pero no pertenece a esta instalación de UniDesk. La instalación se detuvo para no modificar otra aplicación. Registro: %1
spanish.HardwareAclFailed=No se pudo preparar de forma segura la carpeta de componentes del sistema de UniDesk. Revise Common Files de Windows. Registro: %1
spanish.ApplicationLocationInvalid=No se puede usar con seguridad la carpeta seleccionada. Elija una carpeta nueva y vacía o una instalación existente de UniDesk.
spanish.ApplicationLocationNetwork=La aplicación solo se puede instalar en un disco fijo local. No seleccione una unidad de red o extraíble.
spanish.ApplicationLocationAclUnsupported=El disco seleccionado no admite los permisos de seguridad necesarios. Elija un disco local NTFS o ReFS.
spanish.LegacyUninstallerCleanupFailed=La nueva instalación se puede desinstalar normalmente, pero no se pudieron limpiar los archivos de desinstalación antiguos. No ejecute el desinstalador anterior; tras verificar la nueva versión, puede eliminar manualmente la carpeta antigua. Registro: %1
spanish.ApplicationLocationMigrationComplete=UniDesk se instaló en la nueva ubicación y se limpiaron las entradas de inicio que pertenecían estrictamente a la ubicación anterior. El instalador no ejecutó el desinstalador antiguo ni eliminó su carpeta. Tras verificar la nueva versión, puede eliminar manualmente la carpeta anterior; los datos de usuario no se ven afectados.
spanish.HardwareUnsafeServiceRetirementFailed=No se pudo confirmar que el servicio de hardware antiguo de UniDesk se detuviera y deshabilitara de forma segura. La instalación se detuvo; revise el registro y elimine UniDeskHardwareService manualmente. Registro: %1
spanish.LegacyMigrationCleanupFailed=UniDesk se instaló en la nueva ubicación, pero no se completó la limpieza de las entradas de inicio anteriores. No ejecute el desinstalador antiguo; revise el registro y confirme que la versión anterior ya no se inicia con Windows. Registro: %1
spanish.RemovePawnIoPrompt=¿Desinstalar también el controlador compartido PawnIO? Otras aplicaciones de supervisión de hardware o control de ventiladores pueden utilizarlo. Consérvelo salvo que esté seguro de que ninguna otra aplicación depende de él.
spanish.PawnIoRemoveFailed=No se pudo desinstalar PawnIO. La desinstalación de UniDesk continuará y PawnIO permanecerá instalado.

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "completehardware"; Description: "{cm:CompleteHardwareTask}"; GroupDescription: "{cm:HardwareMonitoringGroup}"

[Files]
; Package the release output, including dlls, runtimeconfig, deps and native runtimes.
Source: "{#MyAppSourceDir}\*"; DestDir: "{app}"; Excludes: "icon\*;*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs
; Package the complete icon assets from the project source directory.
Source: "{#MyAppIconSourceDir}\*"; DestDir: "{app}\icon"; Flags: ignoreversion recursesubdirs createallsubdirs
; Keep the repair payload installed even when the optional component is not selected,
; so Settings can add or repair it later without retaining the full setup package.
Source: "{#MyHardwareServiceSourceDir}\*"; DestDir: "{commoncf}\UniDesk\HardwareService"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#MyHardwareRepairSourceDir}\*"; DestDir: "{commoncf}\UniDesk\HardwareRepair"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "installer-assets\PawnIO_setup.exe"; DestDir: "{commoncf}\UniDesk\Hardware"; Flags: ignoreversion
Source: "installer-assets\PawnIO-COPYING.txt"; DestDir: "{app}\licenses"; Flags: ignoreversion
Source: "installer-assets\LibreHardwareMonitor-THIRD-PARTY-NOTICES.txt"; DestDir: "{app}\licenses"; Flags: ignoreversion
Source: "installer-assets\licenses\*"; DestDir: "{app}\licenses"; Flags: ignoreversion
Source: "LICENSE"; DestDir: "{app}\licenses"; DestName: "UniDesk-MIT-LICENSE.txt"; Flags: ignoreversion
Source: "THIRD-PARTY-NOTICES.md"; DestDir: "{app}\licenses"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\icon\{#MyAppIconName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\icon\{#MyAppIconName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Parameters: "--initial-language={language}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent runasoriginaluser

[Code]
const
  FileAttributeDirectory = $10;
  FileAttributeReparsePoint = $400;
  FileShareRead = $1;
  FileShareWrite = $2;
  OpenExisting = 3;
  FileFlagOpenReparsePoint = $00200000;
  FileFlagBackupSemantics = $02000000;
  InvalidHandleValue = -1;
  DriveFixed = 3;
  FilePersistentAcls = $00000008;
  HardwareCompatibilityExitCode = 31;
  CurrentApplicationMarkerName = '.unidesk-application-path';
  LegacyStartupMigrationMarkerName = '.unidesk-legacy-startup-path';

var
  ApplicationPathLockHandles: array of THandle;
  ApplicationPathLocksHeld: Boolean;
  LockedApplicationPath: String;
  LegacyMigrationPath: String;
  LegacyUninstallerCleanupPath: String;
  LegacyRegisteredUninstallerPath: String;
  LegacyRegisteredUninstallerInvalid: Boolean;
  LegacyRegisteredPathInvalid: Boolean;
  LastApplicationLocationError: String;
  ValidatedAclTarget: String;
  StoppedOwnedHardwareService: Boolean;
  HardwareCompatibilityMode: Boolean;

function CleanupOwnedStartupEntries: Boolean; forward;
function SetEnvironmentVariable(Name, Value: String): Boolean;
  external 'SetEnvironmentVariableW@kernel32.dll stdcall';
function CreateFile(
  FileName: String;
  DesiredAccess, ShareMode: Cardinal;
  SecurityAttributes: LongWord;
  CreationDisposition, FlagsAndAttributes: Cardinal;
  TemplateFile: THandle): THandle;
  external 'CreateFileW@kernel32.dll stdcall';
function WindowsCloseHandle(Handle: THandle): Boolean;
  external 'CloseHandle@kernel32.dll stdcall';
function GetDriveType(RootPathName: String): Cardinal;
  external 'GetDriveTypeW@kernel32.dll stdcall';
function GetVolumeInformation(
  RootPathName: String;
  VolumeNameBuffer: String;
  VolumeNameSize: Cardinal;
  var VolumeSerialNumber: Cardinal;
  var MaximumComponentLength: Cardinal;
  var FileSystemFlags: Cardinal;
  FileSystemNameBuffer: String;
  FileSystemNameSize: Cardinal): Boolean;
  external 'GetVolumeInformationW@kernel32.dll stdcall';

procedure InitializeWizard;
begin
  WizardForm.SelectDirBrowseLabel.Caption :=
    ExpandConstant('{cm:ProtectedComponentLocationNotice}');
end;

function HardwareServiceExists: Boolean;
begin
  Result := RegKeyExists(
    HKLM,
    'SYSTEM\CurrentControlSet\Services\{#HardwareServiceName}');
end;

function GetProtectedComponentRoot: String;
begin
  Result := RemoveBackslashUnlessRoot(ExpandConstant('{commoncf}\UniDesk'));
end;

function GetHardwareRepairHelperPath: String;
begin
  Result := AddBackslash(GetProtectedComponentRoot) +
    'HardwareRepair\UniDesk.HardwareRepair.exe';
end;

procedure ReportHardwareComponentFailure(ResultCode: Integer);
var
  MessageText: String;
begin
  MessageText := FmtMessage(
    ExpandConstant('{cm:HardwareRepairFailed}'), [IntToStr(ResultCode)]);
  Log(MessageText);
  if not WizardSilent then
    MsgBox(MessageText, mbError, MB_OK);
end;

procedure ReportHardwareCompatibilityMode;
begin
  HardwareCompatibilityMode := True;
  Log(ExpandConstant('{cm:HardwareCompatibilityMode}'));
end;

procedure InstallHardwareComponent;
var
  ResultCode: Integer;
  HelperPath: String;
begin
  HelperPath := GetHardwareRepairHelperPath;
  ResultCode := -1;
  if not WizardIsTaskSelected('completehardware') then
  begin
    if HardwareServiceExists then
      if (not Exec(
        HelperPath,
        '--remove-service',
        '',
        SW_HIDE,
        ewWaitUntilTerminated,
        ResultCode)) or (ResultCode <> 0) then
        ReportHardwareComponentFailure(ResultCode);
    Exit;
  end;

  if not Exec(
    HelperPath,
    '--install-or-repair',
    '',
    SW_HIDE,
    ewWaitUntilTerminated,
    ResultCode) then
  begin
    ReportHardwareComponentFailure(ResultCode);
    Exit;
  end;

  if ResultCode = HardwareCompatibilityExitCode then
    ReportHardwareCompatibilityMode
  else if ResultCode <> 0 then
    ReportHardwareComponentFailure(ResultCode);
end;

function TryExtractExecutablePath(const Command: String; var ExecutablePath: String): Boolean;
var
  Boundary: Integer;
  ClosingQuote: Integer;
  LowerCommand: String;
  TrimmedCommand: String;
begin
  Result := False;
  ExecutablePath := '';
  TrimmedCommand := Trim(Command);
  if TrimmedCommand = '' then
    Exit;

  if TrimmedCommand[1] = '"' then
  begin
    ClosingQuote := Pos('"', Copy(TrimmedCommand, 2, Length(TrimmedCommand)));
    if ClosingQuote = 0 then
      Exit;
    ClosingQuote := ClosingQuote + 1;
    if (ClosingQuote < Length(TrimmedCommand)) and
      (TrimmedCommand[ClosingQuote + 1] <> ' ') and
      (TrimmedCommand[ClosingQuote + 1] <> #9) then
      Exit;
    ExecutablePath := Copy(TrimmedCommand, 2, ClosingQuote - 2);
  end
  else
  begin
    LowerCommand := Lowercase(TrimmedCommand);
    Boundary := Pos('.exe', LowerCommand);
    if Boundary = 0 then
      Exit;
    Boundary := Boundary + 3;
    if (Boundary < Length(TrimmedCommand)) and
      (TrimmedCommand[Boundary + 1] <> ' ') and
      (TrimmedCommand[Boundary + 1] <> #9) then
      Exit;
    ExecutablePath := Copy(TrimmedCommand, 1, Boundary);
  end;

  Result := CompareText(ExtractFileExt(ExecutablePath), '.exe') = 0;
end;

function IsHardwareServiceOwnedAt(const ComponentOrLegacyAppPath: String): Boolean;
var
  ExecutablePath: String;
  ImagePath: String;
begin
  Result := False;
  if not HardwareServiceExists then
    Exit;
  if not RegQueryStringValue(
    HKLM,
    'SYSTEM\CurrentControlSet\Services\{#HardwareServiceName}',
    'ImagePath',
    ImagePath) then
    Exit;
  if not TryExtractExecutablePath(ImagePath, ExecutablePath) then
    Exit;

  Result := CompareText(
    RemoveBackslashUnlessRoot(ExpandFileName(ExecutablePath)),
    RemoveBackslashUnlessRoot(ExpandFileName(
      AddBackslash(ComponentOrLegacyAppPath) +
      'HardwareService\UniDesk.HardwareService.exe'))) = 0;
end;

function IsHardwareServiceOwned: Boolean;
begin
  Result := IsHardwareServiceOwnedAt(GetProtectedComponentRoot);
end;

function RunIcacls(const Parameters: String): Boolean;
var
  ResultCode: Integer;
begin
  ResultCode := -1;
  Result := Exec(
    ExpandConstant('{sys}\icacls.exe'),
    Parameters,
    '',
    SW_HIDE,
    ewWaitUntilTerminated,
    ResultCode) and (ResultCode = 0);
  if not Result then
    Log('icacls failed with exit code ' + IntToStr(ResultCode) + ': ' + Parameters);
end;

function NormalizeDirectoryPath(const DirectoryPath: String): String;
begin
  Result := RemoveBackslashUnlessRoot(ExpandFileName(DirectoryPath));
end;

function IsSameDirectory(const LeftPath, RightPath: String): Boolean;
begin
  Result := CompareText(
    NormalizeDirectoryPath(LeftPath),
    NormalizeDirectoryPath(RightPath)) = 0;
end;

function VerifyProtectedComponentRootAcl: Boolean;
var
  Parameters: String;
  ProtectedParent: String;
  ProtectedRoot: String;
  ResultCode: Integer;
begin
  ProtectedRoot := NormalizeDirectoryPath(ExpandConstant('{commoncf}'));
  ProtectedParent := NormalizeDirectoryPath(ExtractFileDir(ProtectedRoot));
  if (ProtectedParent = '') or IsSameDirectory(ProtectedParent, ProtectedRoot) then
  begin
    Log('Could not resolve the protected Program Files parent directory.');
    Result := False;
    Exit;
  end;
  if not SetEnvironmentVariable('UNIDESK_PROTECTED_ROOT', ProtectedRoot) then
  begin
    Log('Could not bind the protected component root for ACL verification.');
    Result := False;
    Exit;
  end;
  if not SetEnvironmentVariable('UNIDESK_PROTECTED_PARENT', ProtectedParent) then
  begin
    SetEnvironmentVariable('UNIDESK_PROTECTED_ROOT', '');
    Log('Could not bind the protected component parent for ACL verification.');
    Result := False;
    Exit;
  end;

  Parameters :=
    '-NoLogo -NoProfile -NonInteractive -Command "' +
    '$trusted=@(''S-1-5-18'',''S-1-5-32-544'',''S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464''); ' +
    '$danger=[Security.AccessControl.FileSystemRights]::Delete -bor [Security.AccessControl.FileSystemRights]::DeleteSubdirectoriesAndFiles -bor [Security.AccessControl.FileSystemRights]::ChangePermissions -bor [Security.AccessControl.FileSystemRights]::TakeOwnership; ' +
    '$sections=[Security.AccessControl.AccessControlSections]::Access -bor [Security.AccessControl.AccessControlSections]::Owner; ' +
    '$paths=@($env:UNIDESK_PROTECTED_ROOT,$env:UNIDESK_PROTECTED_PARENT); foreach($path in $paths){ ' +
    '$current=[IO.DirectoryInfo]::new($path); if(-not $current.Exists){exit 23}; ' +
    'if(($current.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){exit 20}; ' +
    '$acl=$current.GetAccessControl($sections); ' +
    '$owner=$acl.GetOwner([Security.Principal.SecurityIdentifier]).Value; ' +
    'if($trusted -notcontains $owner){exit 21}; ' +
    'foreach($rule in $acl.GetAccessRules($true,$true,[Security.Principal.SecurityIdentifier])){ ' +
    'if(($rule.AccessControlType -eq [Security.AccessControl.AccessControlType]::Allow) -and ' +
    '(($rule.PropagationFlags -band [Security.AccessControl.PropagationFlags]::InheritOnly) -eq 0) -and ' +
    '(($rule.FileSystemRights -band $danger) -ne 0) -and ' +
    '($trusted -notcontains $rule.IdentityReference.Value)){exit 22} } }; exit 0"';
  ResultCode := -1;
  Result := Exec(
    ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
    Parameters,
    '',
    SW_HIDE,
    ewWaitUntilTerminated,
    ResultCode) and (ResultCode = 0);
  SetEnvironmentVariable('UNIDESK_PROTECTED_ROOT', '');
  SetEnvironmentVariable('UNIDESK_PROTECTED_PARENT', '');
  if not Result then
    Log('Protected component root ACL verification failed with exit code ' +
      IntToStr(ResultCode) + '.');
end;

function IsProtectedBroadDirectory(const NormalizedPath: String): Boolean;
var
  DriveRoot: String;
begin
  DriveRoot := ExtractFileDrive(NormalizedPath) + '\';
  Result :=
    IsSameDirectory(NormalizedPath, DriveRoot) or
    IsSameDirectory(NormalizedPath, ExpandConstant('{win}')) or
    IsSameDirectory(NormalizedPath, ExpandConstant('{pf}')) or
    IsSameDirectory(NormalizedPath, ExpandConstant('{pf32}')) or
    IsSameDirectory(NormalizedPath, ExpandConstant('{commoncf}')) or
    IsSameDirectory(NormalizedPath, ExpandConstant('{commonappdata}')) or
    IsSameDirectory(NormalizedPath, ExpandConstant('{%USERPROFILE}')) or
    IsSameDirectory(NormalizedPath, ExpandConstant('{localappdata}')) or
    IsSameDirectory(NormalizedPath, ExpandConstant('{userappdata}'));
end;

function IsDirectoryEmpty(const DirectoryPath: String): Boolean;
var
  FindRec: TFindRec;
begin
  Result := True;
  if not FindFirst(AddBackslash(DirectoryPath) + '*', FindRec) then
    Exit;

  try
    repeat
      if (FindRec.Name <> '.') and (FindRec.Name <> '..') then
      begin
        Result := False;
        Exit;
      end;
    until not FindNext(FindRec);
  finally
    FindClose(FindRec);
  end;
end;

function ContainsReparsePoint(const DirectoryPath: String): Boolean;
var
  ChildPath: String;
  FindRec: TFindRec;
begin
  Result := False;
  if FindFirst(DirectoryPath, FindRec) then
  begin
    try
      if (FindRec.Attributes and FileAttributeReparsePoint) <> 0 then
      begin
        Result := True;
        Exit;
      end;
    finally
      FindClose(FindRec);
    end;
  end;

  if not FindFirst(AddBackslash(DirectoryPath) + '*', FindRec) then
    Exit;
  try
    repeat
      if (FindRec.Name <> '.') and (FindRec.Name <> '..') then
      begin
        ChildPath := AddBackslash(DirectoryPath) + FindRec.Name;
        if (FindRec.Attributes and FileAttributeReparsePoint) <> 0 then
        begin
          Result := True;
          Exit;
        end;
        if ((FindRec.Attributes and FileAttributeDirectory) <> 0) and
          ContainsReparsePoint(ChildPath) then
        begin
          Result := True;
          Exit;
        end;
      end;
    until not FindNext(FindRec);
  finally
    FindClose(FindRec);
  end;
end;

function ContainsReparsePointInExistingAncestorChain(
  const DirectoryPath: String): Boolean;
var
  CurrentPath: String;
  FindRec: TFindRec;
  ParentPath: String;
begin
  Result := False;
  CurrentPath := NormalizeDirectoryPath(DirectoryPath);

  while (CurrentPath <> '') and (not DirExists(CurrentPath)) do
  begin
    ParentPath := NormalizeDirectoryPath(ExtractFileDir(CurrentPath));
    if (ParentPath = '') or IsSameDirectory(CurrentPath, ParentPath) then
      Exit;
    CurrentPath := ParentPath;
  end;

  while CurrentPath <> '' do
  begin
    if FindFirst(CurrentPath, FindRec) then
    begin
      try
        if (FindRec.Attributes and FileAttributeReparsePoint) <> 0 then
        begin
          Result := True;
          Exit;
        end;
      finally
        FindClose(FindRec);
      end;
    end;

    ParentPath := NormalizeDirectoryPath(ExtractFileDir(CurrentPath));
    if (ParentPath = '') or IsSameDirectory(CurrentPath, ParentPath) then
      Exit;
    CurrentPath := ParentPath;
  end;
end;

procedure ReleaseApplicationPathLocks;
var
  Index: Integer;
begin
  if GetArrayLength(ApplicationPathLockHandles) > 0 then
    for Index := GetArrayLength(ApplicationPathLockHandles) - 1 downto 0 do
      if ApplicationPathLockHandles[Index] <> InvalidHandleValue then
        WindowsCloseHandle(ApplicationPathLockHandles[Index]);
  SetArrayLength(ApplicationPathLockHandles, 0);
  ApplicationPathLocksHeld := False;
  LockedApplicationPath := '';
end;

function AcquireApplicationPathLocks(const DirectoryPath: String): Boolean;
var
  CurrentPath: String;
  DirectoryHandle: THandle;
  Index: Integer;
  ParentPath: String;
  Paths: array of String;
begin
  ReleaseApplicationPathLocks;
  CurrentPath := NormalizeDirectoryPath(DirectoryPath);
  while CurrentPath <> '' do
  begin
    Index := GetArrayLength(Paths);
    SetArrayLength(Paths, Index + 1);
    Paths[Index] := CurrentPath;

    ParentPath := NormalizeDirectoryPath(ExtractFileDir(CurrentPath));
    if (ParentPath = '') or IsSameDirectory(CurrentPath, ParentPath) then
      Break;
    CurrentPath := ParentPath;
  end;

  for Index := GetArrayLength(Paths) - 1 downto 0 do
  begin
    DirectoryHandle := CreateFile(
      Paths[Index],
      0,
      FileShareRead or FileShareWrite,
      0,
      OpenExisting,
      FileFlagBackupSemantics or FileFlagOpenReparsePoint,
      0);
    if DirectoryHandle = InvalidHandleValue then
    begin
      Log('Could not lock application path against delete or rename: ' + Paths[Index]);
      ReleaseApplicationPathLocks;
      Result := False;
      Exit;
    end;

    SetArrayLength(
      ApplicationPathLockHandles,
      GetArrayLength(ApplicationPathLockHandles) + 1);
    ApplicationPathLockHandles[GetArrayLength(ApplicationPathLockHandles) - 1] :=
      DirectoryHandle;
  end;

  if ContainsReparsePoint(DirectoryPath) then
  begin
    Log('Application path became a reparse point while acquiring directory locks.');
    ReleaseApplicationPathLocks;
    Result := False;
    Exit;
  end;

  ApplicationPathLocksHeld := True;
  LockedApplicationPath := NormalizeDirectoryPath(DirectoryPath);
  Result := True;
end;

function GetRegisteredUniDeskUninstallValue(
  const ValueName: String;
  var Value: String): Boolean;
begin
  Result := RegQueryStringValue(
    HKLM64,
    'Software\Microsoft\Windows\CurrentVersion\Uninstall\{4B0F3B03-7F5D-4B5D-B2F4-6816B931C7D2}_is1',
    ValueName,
    Value);
  if not Result then
    Result := RegQueryStringValue(
      HKLM32,
      'Software\Microsoft\Windows\CurrentVersion\Uninstall\{4B0F3B03-7F5D-4B5D-B2F4-6816B931C7D2}_is1',
      ValueName,
      Value);
end;

function GetRegisteredUniDeskInstallationDirectory(var RegisteredPath: String): Boolean;
begin
  Result := GetRegisteredUniDeskUninstallValue('InstallLocation', RegisteredPath);
end;

function GetRegisteredUniDeskUninstallerPath(var UninstallerPath: String): Boolean;
var
  UninstallCommand: String;
begin
  UninstallerPath := '';
  LegacyRegisteredUninstallerInvalid := False;
  if not GetRegisteredUniDeskUninstallValue('UninstallString', UninstallCommand) then
  begin
    Result := False;
    Exit;
  end;

  if not TryExtractExecutablePath(UninstallCommand, UninstallerPath) then
  begin
    LegacyRegisteredUninstallerInvalid := True;
    Result := False;
    Exit;
  end;

  UninstallerPath := ExpandFileName(UninstallerPath);
  Result := True;
end;

function IsAsciiDigit(const Character: Char): Boolean;
begin
  Result := (Character >= '0') and (Character <= '9');
end;

function IsLegacyUninstallerFileName(const FileName: String): Boolean;
var
  LowerFileName: String;
  Extension: String;
begin
  LowerFileName := Lowercase(FileName);
  if (Length(LowerFileName) <> 12) or
    (Copy(LowerFileName, 1, 5) <> 'unins') or
    (not IsAsciiDigit(LowerFileName[6])) or
    (not IsAsciiDigit(LowerFileName[7])) or
    (not IsAsciiDigit(LowerFileName[8])) then
  begin
    Result := False;
    Exit;
  end;

  Extension := Copy(LowerFileName, 9, 4);
  Result := (Extension = '.exe') or (Extension = '.dat') or (Extension = '.msg');
end;

function PrepareLegacyRegisteredUninstallerCleanup(
  const RegisteredPath: String): Boolean;
var
  RegisteredUninstallerDirectory: String;
  RegisteredUninstallerPath: String;
begin
  Result := False;
  LegacyUninstallerCleanupPath := '';
  LegacyRegisteredUninstallerPath := '';
  if not GetRegisteredUniDeskUninstallerPath(RegisteredUninstallerPath) then
  begin
    Result := not LegacyRegisteredUninstallerInvalid;
    Exit;
  end;

  RegisteredUninstallerDirectory := ExtractFileDir(RegisteredUninstallerPath);
  if not IsLegacyUninstallerFileName(ExtractFileName(RegisteredUninstallerPath)) or
    ((not IsSameDirectory(RegisteredUninstallerDirectory, RegisteredPath)) and
     (not IsSameDirectory(
       RegisteredUninstallerDirectory,
       AddBackslash(GetProtectedComponentRoot) + 'Uninstall'))) then
  begin
    Log('The registered UniDesk uninstaller is outside an owned uninstall directory.');
    Exit;
  end;

  if IsSameDirectory(RegisteredUninstallerDirectory, RegisteredPath) then
  begin
    LegacyUninstallerCleanupPath := NormalizeDirectoryPath(RegisteredPath);
    LegacyRegisteredUninstallerPath := RegisteredUninstallerPath;
  end;
  Result := True;
end;

function RemoveLegacyRegisteredUninstallerFiles(
  const RegisteredPath: String): Boolean;
var
  FindRec: TFindRec;
  RegisteredUninstallerDirectory: String;
  UninstallerPath: String;
begin
  Result := False;
  if LegacyUninstallerCleanupPath = '' then
  begin
    Result := True;
    Exit;
  end;
  if (not ApplicationPathLocksHeld) or
    (not IsSameDirectory(RegisteredPath, LockedApplicationPath)) or
    (not IsSameDirectory(RegisteredPath, LegacyUninstallerCleanupPath)) then
  begin
    Log('Refusing legacy uninstaller cleanup without a lock on the registered application directory.');
    Exit;
  end;

  RegisteredUninstallerDirectory := ExtractFileDir(LegacyRegisteredUninstallerPath);
  if (LegacyRegisteredUninstallerPath = '') or
    (not IsLegacyUninstallerFileName(
      ExtractFileName(LegacyRegisteredUninstallerPath))) or
    (not IsSameDirectory(RegisteredUninstallerDirectory, RegisteredPath)) then
  begin
    Log('The captured legacy UniDesk uninstaller is no longer an owned application file.');
    Exit;
  end;

  if not FindFirst(AddBackslash(RegisteredPath) + 'unins*.*', FindRec) then
  begin
    Result := True;
    Exit;
  end;

  try
    repeat
      if ((FindRec.Attributes and FileAttributeDirectory) = 0) and
        IsLegacyUninstallerFileName(FindRec.Name) then
      begin
        UninstallerPath := AddBackslash(RegisteredPath) + FindRec.Name;
        if FileExists(UninstallerPath) and (not DeleteFile(UninstallerPath)) then
        begin
          Log('Could not delete the verified legacy uninstaller file: ' + UninstallerPath);
          Exit;
        end;
        Log('Deleted verified legacy uninstaller file: ' + UninstallerPath);
      end;
    until not FindNext(FindRec);
  finally
    FindClose(FindRec);
  end;

  Result := True;
end;

function GetPreviousUniDeskApplicationDirectory(
  var RegisteredPath: String): Boolean;
begin
  LegacyRegisteredPathInvalid := False;
  RegisteredPath := '';
  if not GetRegisteredUniDeskInstallationDirectory(RegisteredPath) then
  begin
    Result := False;
    Exit;
  end;

  RegisteredPath := NormalizeDirectoryPath(RegisteredPath);
  if (RegisteredPath = '') or IsProtectedBroadDirectory(RegisteredPath) then
  begin
    LegacyRegisteredPathInvalid := True;
    Result := False;
    Exit;
  end;

  Result := True;
end;

function IsKnownUniDeskInstallationDirectory(const NormalizedPath: String): Boolean;
var
  RegisteredPath: String;
begin
  Result := GetRegisteredUniDeskInstallationDirectory(RegisteredPath);
  Result := Result and IsSameDirectory(NormalizedPath, RegisteredPath);
end;

function IsSupportedApplicationVolume(const DriveRoot: String): Boolean;
var
  DriveKind: Cardinal;
  FileSystemFlags: Cardinal;
  FileSystemName: String;
  MaximumComponentLength: Cardinal;
  VolumeName: String;
  VolumeSerialNumber: Cardinal;
begin
  Result := False;
  DriveKind := GetDriveType(DriveRoot);
  if DriveKind <> DriveFixed then
  begin
    LastApplicationLocationError :=
      ExpandConstant('{cm:ApplicationLocationNetwork}');
    Exit;
  end;

  SetLength(VolumeName, 261);
  SetLength(FileSystemName, 261);
  FileSystemFlags := 0;
  MaximumComponentLength := 0;
  VolumeSerialNumber := 0;
  if (not GetVolumeInformation(
      DriveRoot,
      VolumeName,
      261,
      VolumeSerialNumber,
      MaximumComponentLength,
      FileSystemFlags,
      FileSystemName,
      261)) or
    ((FileSystemFlags and FilePersistentAcls) = 0) then
  begin
    LastApplicationLocationError :=
      ExpandConstant('{cm:ApplicationLocationAclUnsupported}');
    Exit;
  end;

  Result := True;
end;

function IsSafeApplicationInstallTarget(const DirectoryPath: String): Boolean;
var
  DrivePath: String;
  NormalizedPath: String;
begin
  LastApplicationLocationError :=
    ExpandConstant('{cm:ApplicationLocationInvalid}');
  NormalizedPath := NormalizeDirectoryPath(DirectoryPath);
  if (NormalizedPath = '') or IsProtectedBroadDirectory(NormalizedPath) then
  begin
    Result := False;
    Exit;
  end;

  DrivePath := ExtractFileDrive(NormalizedPath);
  if Length(ExtractFileDrive(NormalizedPath)) <> 2 then
  begin
    Result := False;
    Exit;
  end;
  if DrivePath[2] <> ':' then
  begin
    Result := False;
    Exit;
  end;
  if not IsSupportedApplicationVolume(DrivePath + '\') then
  begin
    Result := False;
    Exit;
  end;

  if ContainsReparsePointInExistingAncestorChain(NormalizedPath) then
  begin
    Result := False;
    Exit;
  end;

  if not DirExists(NormalizedPath) then
  begin
    Result := True;
    Exit;
  end;

  if ContainsReparsePoint(NormalizedPath) then
  begin
    Result := False;
    Exit;
  end;

  Result := IsDirectoryEmpty(NormalizedPath) or
    IsKnownUniDeskInstallationDirectory(NormalizedPath);
end;

function IsSafeProtectedComponentTargetForAcl(
  const DirectoryPath: String): Boolean;
var
  NormalizedPath: String;
begin
  NormalizedPath := NormalizeDirectoryPath(DirectoryPath);
  if not IsSameDirectory(NormalizedPath, GetProtectedComponentRoot) then
  begin
    Result := False;
    Exit;
  end;

  Result := (not DirExists(NormalizedPath)) or
    (not ContainsReparsePoint(NormalizedPath));
end;

function GetLegacyAppHostedOwnedServicePath(
  var OwnedApplicationPath: String): Boolean;
var
  RegisteredPath: String;
begin
  Result := False;
  OwnedApplicationPath := '';
  if not HardwareServiceExists then
    Exit;

  if IsHardwareServiceOwned then
    Exit;

  if IsHardwareServiceOwnedAt(ExpandConstant('{app}')) then
  begin
    OwnedApplicationPath := ExpandConstant('{app}');
    Result := True;
    Exit;
  end;

  if GetRegisteredUniDeskInstallationDirectory(RegisteredPath) and
    IsHardwareServiceOwnedAt(RegisteredPath) then
  begin
    OwnedApplicationPath := RegisteredPath;
    Result := True;
  end;
end;

function BooleanLogValue(const Value: Boolean): String;
begin
  if Value then
    Result := 'true'
  else
    Result := 'false';
end;

function IsHardwareServiceStopped: Boolean;
var
  ResultCode: Integer;
begin
  ResultCode := -1;
  Result := Exec(
    ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
    '-NoLogo -NoProfile -NonInteractive -Command "$s = Get-Service -Name ''{#HardwareServiceName}'' -ErrorAction SilentlyContinue; if (($null -eq $s) -or ($s.Status -eq ''Stopped'')) { exit 0 } else { exit 1 }"',
    '',
    SW_HIDE,
    ewWaitUntilTerminated,
    ResultCode) and (ResultCode = 0);
end;

function WaitForHardwareServiceStopped: Boolean;
var
  Attempt: Integer;
begin
  Result := False;
  for Attempt := 1 to 20 do
  begin
    if IsHardwareServiceStopped then
    begin
      Result := True;
      Exit;
    end;
    Sleep(500);
  end;
end;

function RetireOwnedHardwareServiceAt(
  const OwnedApplicationPath: String): Boolean;
var
  DeleteCode: Integer;
  DeleteSafe: Boolean;
  DisableCode: Integer;
  DisableSafe: Boolean;
  StopCode: Integer;
  StopSafe: Boolean;
  TaskKillCode: Integer;
begin
  if not IsHardwareServiceOwnedAt(OwnedApplicationPath) then
  begin
    Log('Hardware service ownership changed before retirement; refusing to act.');
    Result := False;
    Exit;
  end;

  DisableCode := -1;
  DisableSafe := Exec(
    ExpandConstant('{sys}\sc.exe'),
    'config {#HardwareServiceName} start= disabled',
    '',
    SW_HIDE,
    ewWaitUntilTerminated,
    DisableCode) and (DisableCode = 0);

  StopCode := -1;
  Exec(
    ExpandConstant('{sys}\sc.exe'),
    'stop {#HardwareServiceName}',
    '',
    SW_HIDE,
    ewWaitUntilTerminated,
    StopCode);
  Sleep(500);
  TaskKillCode := -1;
  Exec(
    ExpandConstant('{sys}\taskkill.exe'),
    '/F /FI "SERVICES eq {#HardwareServiceName}"',
    '',
    SW_HIDE,
    ewWaitUntilTerminated,
    TaskKillCode);
  StopSafe := WaitForHardwareServiceStopped;
  DeleteCode := -1;
  DeleteSafe := Exec(
    ExpandConstant('{sys}\sc.exe'),
    'delete {#HardwareServiceName}',
    '',
    SW_HIDE,
    ewWaitUntilTerminated,
    DeleteCode) and ((DeleteCode = 0) or (DeleteCode = 1060));

  Result := StopSafe and (DeleteSafe or DisableSafe);
  Log('Owned hardware service retirement: disabled=' +
    BooleanLogValue(DisableSafe) + '; stopped=' + BooleanLogValue(StopSafe) +
    '; deleted=' + BooleanLogValue(DeleteSafe) + '.');
end;

function RetireLegacyAppHostedHardwareService(var Retired: Boolean): Boolean;
var
  OwnedApplicationPath: String;
begin
  Retired := GetLegacyAppHostedOwnedServicePath(OwnedApplicationPath);
  if not Retired then
  begin
    Result := True;
    Exit;
  end;

  Log('Retiring owned hardware service from legacy application path: ' +
    OwnedApplicationPath);
  Result := RetireOwnedHardwareServiceAt(OwnedApplicationPath);
end;

function ResetDirectoryChildrenAcl(const DirectoryPath: String): Boolean;
var
  FindRec: TFindRec;
begin
  if not FindFirst(AddBackslash(DirectoryPath) + '*', FindRec) then
  begin
    Result := True;
    Exit;
  end;

  try
    Result := RunIcacls(
      '"' + AddBackslash(DirectoryPath) + '*" /reset /T /C /L /Q');
  finally
    FindClose(FindRec);
  end;
end;

function HardenDirectoryAcl(const DirectoryPath: String): Boolean;
var
  QuotedPath: String;
begin
  QuotedPath := '"' + DirectoryPath + '"';

  Result :=
    RunIcacls(QuotedPath + ' /setowner *S-1-5-32-544 /T /C /L /Q') and
    RunIcacls(QuotedPath + ' /inheritance:r /C /L /Q') and
    RunIcacls(QuotedPath + ' /remove:g *S-1-1-0 *S-1-5-11 *S-1-5-32-545 /C /L /Q') and
    RunIcacls(
      QuotedPath +
      ' /grant:r *S-1-5-18:(OI)(CI)F *S-1-5-32-544:(OI)(CI)F *S-1-5-32-545:(OI)(CI)RX /C /L /Q') and
    ResetDirectoryChildrenAcl(DirectoryPath);
end;

function HardenProtectedComponentPayload: Boolean;
var
  ComponentPath: String;
begin
  ComponentPath := GetProtectedComponentRoot;
  if ((ValidatedAclTarget = '') and
      not IsSafeProtectedComponentTargetForAcl(ComponentPath)) or
    ((ValidatedAclTarget <> '') and
      not IsSameDirectory(ComponentPath, ValidatedAclTarget)) then
  begin
    Log('Refusing recursive ACL hardening outside the fixed protected component target: ' +
      ComponentPath);
    Result := False;
    Exit;
  end;

  Result :=
    ForceDirectories(ComponentPath) and
    ForceDirectories(AddBackslash(ComponentPath) + 'HardwareService') and
    ForceDirectories(AddBackslash(ComponentPath) + 'HardwareRepair') and
    ForceDirectories(AddBackslash(ComponentPath) + 'Hardware') and
    ForceDirectories(AddBackslash(ComponentPath) + 'Uninstall');
  if not Result then
    Exit;

  Result :=
    HardenDirectoryAcl(ComponentPath) and
    HardenDirectoryAcl(AddBackslash(ComponentPath) + 'HardwareService') and
    HardenDirectoryAcl(AddBackslash(ComponentPath) + 'HardwareRepair') and
    HardenDirectoryAcl(AddBackslash(ComponentPath) + 'Hardware') and
    HardenDirectoryAcl(AddBackslash(ComponentPath) + 'Uninstall');
  if Result and (ValidatedAclTarget = '') then
    ValidatedAclTarget := NormalizeDirectoryPath(ComponentPath);
end;

function HardenLockedApplicationDirectory(const DirectoryPath: String): Boolean;
var
  AppPath: String;
begin
  AppPath := NormalizeDirectoryPath(DirectoryPath);
  if (not ApplicationPathLocksHeld) or
    (not IsSameDirectory(AppPath, LockedApplicationPath)) or
    (not IsSafeApplicationInstallTarget(AppPath)) or
    ContainsReparsePoint(AppPath) then
  begin
    Log('Refusing application ACL hardening without a locked, validated target: ' + AppPath);
    Result := False;
    Exit;
  end;

  Result := HardenDirectoryAcl(AppPath) and
    (not ContainsReparsePoint(AppPath));
end;

function HardenApplicationPayload: Boolean;
begin
  Result := HardenLockedApplicationDirectory(ExpandConstant('{app}'));
end;

function PersistProtectedApplicationMarkers: Boolean;
var
  CurrentMarkerPath: String;
  LegacyMarkerPath: String;
  LegacyMarkerValue: String;
begin
  CurrentMarkerPath := AddBackslash(GetProtectedComponentRoot) +
    CurrentApplicationMarkerName;
  LegacyMarkerPath := AddBackslash(GetProtectedComponentRoot) +
    LegacyStartupMigrationMarkerName;
  if LegacyMigrationPath = '' then
    LegacyMarkerValue := NormalizeDirectoryPath(ExpandConstant('{app}'))
  else
    LegacyMarkerValue := LegacyMigrationPath;

  Result :=
    SaveStringToFile(
      CurrentMarkerPath,
      NormalizeDirectoryPath(ExpandConstant('{app}')),
      False) and
    SaveStringToFile(LegacyMarkerPath, LegacyMarkerValue, False);
  if not Result then
    Log('Could not persist protected application path markers.');
end;

procedure RestartOwnedHardwareServiceIfNeeded;
var
  ResultCode: Integer;
begin
  if not StoppedOwnedHardwareService then
    Exit;

  StoppedOwnedHardwareService := False;
  if not HardwareServiceExists then
    Exit;
  if not IsHardwareServiceOwned then
  begin
    Log('Refusing to restart a same-named service whose ownership changed during setup.');
    Exit;
  end;

  ResultCode := -1;
  if (not Exec(
    ExpandConstant('{sys}\sc.exe'),
    'start {#HardwareServiceName}',
    '',
    SW_HIDE,
    ewWaitUntilTerminated,
    ResultCode)) or ((ResultCode <> 0) and (ResultCode <> 1056)) then
    Log('Could not restore the previously running hardware service; exit code ' +
      IntToStr(ResultCode) + '.');
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  LegacyInstallPath: String;
  PreviousInstallationExists: Boolean;
  RetiredLegacyService: Boolean;
  ResultCode: Integer;
  TaskKillCode: Integer;
begin
  Result := '';
  LegacyUninstallerCleanupPath := '';
  LegacyRegisteredUninstallerPath := '';
  PreviousInstallationExists := GetPreviousUniDeskApplicationDirectory(
    LegacyInstallPath);
  if LegacyRegisteredPathInvalid then
  begin
    Log('The registered application directory is empty or dangerously broad.');
    Result := ExpandConstant('{cm:ApplicationLocationInvalid}');
    Exit;
  end;
  if PreviousInstallationExists and
    (not IsSameDirectory(LegacyInstallPath, ExpandConstant('{app}'))) then
  begin
    LegacyMigrationPath := LegacyInstallPath;
    Log('Preparing startup entry migration from the previous application directory: ' +
      LegacyMigrationPath + '.');
  end
  else
    LegacyMigrationPath := '';

  if not IsSafeApplicationInstallTarget(ExpandConstant('{app}')) then
  begin
    Log('Refusing the selected application target: ' + ExpandConstant('{app}'));
    Result := LastApplicationLocationError;
    Exit;
  end;

  if PreviousInstallationExists and DirExists(LegacyInstallPath) then
  begin
    if (not IsSafeApplicationInstallTarget(LegacyInstallPath)) or
      (not AcquireApplicationPathLocks(LegacyInstallPath)) or
      (not HardenLockedApplicationDirectory(LegacyInstallPath)) then
    begin
      Log('Could not lock and secure the registered legacy application directory.');
      Result := ExpandConstant('{cm:ApplicationLocationInvalid}');
      Exit;
    end;

    if not PrepareLegacyRegisteredUninstallerCleanup(LegacyInstallPath) then
    begin
      Log('The registered legacy uninstaller failed ownership validation.');
      Result := ExpandConstant('{cm:ApplicationLocationInvalid}');
      Exit;
    end;

    if not IsSameDirectory(LegacyInstallPath, ExpandConstant('{app}')) then
      ReleaseApplicationPathLocks;
  end;

  if (not ForceDirectories(ExpandConstant('{app}'))) or
    ((not ApplicationPathLocksHeld) and
      (not AcquireApplicationPathLocks(ExpandConstant('{app}')))) or
    (not HardenApplicationPayload) then
  begin
    Log('Could not lock and secure the selected application target.');
    Result := ExpandConstant('{cm:ApplicationLocationInvalid}');
    Exit;
  end;

  if not VerifyProtectedComponentRootAcl then
  begin
    Result := FmtMessage(
      ExpandConstant('{cm:HardwareAclFailed}'), [ExpandConstant('{log}')]);
    Exit;
  end;

  if not IsSafeProtectedComponentTargetForAcl(GetProtectedComponentRoot) then
  begin
    Log('Refusing the fixed protected component target: ' + GetProtectedComponentRoot);
    Result := FmtMessage(
      ExpandConstant('{cm:HardwareAclFailed}'), [ExpandConstant('{log}')]);
    Exit;
  end;

  if not HardenProtectedComponentPayload then
  begin
    Result := FmtMessage(
      ExpandConstant('{cm:HardwareAclFailed}'), [ExpandConstant('{log}')]);
    Exit;
  end;

  if not RetireLegacyAppHostedHardwareService(RetiredLegacyService) then
  begin
    Result := FmtMessage(
      ExpandConstant('{cm:HardwareUnsafeServiceRetirementFailed}'), [ExpandConstant('{log}')]);
    Exit;
  end;

  if HardwareServiceExists and (not RetiredLegacyService) then
  begin
    if not IsHardwareServiceOwned then
    begin
      Log('Refusing to stop a foreign service named {#HardwareServiceName}.');
      Result := FmtMessage(
        ExpandConstant('{cm:HardwareServiceOwnershipFailed}'), [ExpandConstant('{log}')]);
      Exit;
    end;

    if (not Exec(
      ExpandConstant('{sys}\sc.exe'),
      'stop {#HardwareServiceName}',
      '',
      SW_HIDE,
      ewWaitUntilTerminated,
      ResultCode)) or
      ((ResultCode <> 0) and (ResultCode <> 1060) and (ResultCode <> 1062)) then
    begin
      Result := FmtMessage(
        ExpandConstant('{cm:HardwareServiceStopFailed}'), [IntToStr(ResultCode), ExpandConstant('{log}')]);
      Exit;
    end;
    StoppedOwnedHardwareService := ResultCode = 0;
    if HardwareServiceExists then
    begin
      if not IsHardwareServiceOwned then
      begin
        Result := FmtMessage(
          ExpandConstant('{cm:HardwareServiceOwnershipFailed}'), [ExpandConstant('{log}')]);
        Exit;
      end;
      TaskKillCode := -1;
      Exec(
        ExpandConstant('{sys}\taskkill.exe'),
        '/F /FI "SERVICES eq {#HardwareServiceName}"',
        '',
        SW_HIDE,
        ewWaitUntilTerminated,
        TaskKillCode);
      if not WaitForHardwareServiceStopped then
      begin
        Result := FmtMessage(
          ExpandConstant('{cm:HardwareServiceStopFailed}'), [IntToStr(TaskKillCode), ExpandConstant('{log}')]);
        Exit;
      end;
    end;
  end;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if (CurPageID = wpSelectDir) and
    not IsSafeApplicationInstallTarget(ExpandConstant('{app}')) then
  begin
    Log('Selected application directory was rejected on the location page.');
    MsgBox(LastApplicationLocationError, mbError, MB_OK);
    Result := False;
  end;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if (CurPageID = wpFinished) and HardwareCompatibilityMode then
    WizardForm.FinishedLabel.Caption :=
      ExpandConstant('{cm:HardwareCompatibilityMode}');
end;

procedure CleanupLegacyUninstallerAfterInstall;
var
  CleanupSucceeded: Boolean;
  FailureMessage: String;
begin
  if LegacyUninstallerCleanupPath = '' then
    Exit;

  CleanupSucceeded := False;
  if ApplicationPathLocksHeld and
    IsSameDirectory(LegacyUninstallerCleanupPath, LockedApplicationPath) then
    CleanupSucceeded := RemoveLegacyRegisteredUninstallerFiles(
      LegacyUninstallerCleanupPath)
  else
  begin
    ReleaseApplicationPathLocks;
    if IsSafeApplicationInstallTarget(LegacyUninstallerCleanupPath) and
      AcquireApplicationPathLocks(LegacyUninstallerCleanupPath) and
      HardenLockedApplicationDirectory(LegacyUninstallerCleanupPath) then
      CleanupSucceeded := RemoveLegacyRegisteredUninstallerFiles(
        LegacyUninstallerCleanupPath);
  end;

  if CleanupSucceeded then
  begin
    Log('Legacy uninstall files were removed after the new uninstaller was created.');
    LegacyUninstallerCleanupPath := '';
    LegacyRegisteredUninstallerPath := '';
  end
  else
  begin
    FailureMessage := FmtMessage(
      ExpandConstant('{cm:LegacyUninstallerCleanupFailed}'), [ExpandConstant('{log}')]);
    Log(FailureMessage);
    if not WizardSilent then
      MsgBox(FailureMessage, mbError, MB_OK);
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep <> ssPostInstall then
    Exit;

  if not HardenProtectedComponentPayload then
  begin
    RestartOwnedHardwareServiceIfNeeded;
    ReportHardwareComponentFailure(90);
    Exit;
  end;

  if not PersistProtectedApplicationMarkers then
  begin
    RestartOwnedHardwareServiceIfNeeded;
    ReportHardwareComponentFailure(91);
    Exit;
  end;

  CleanupLegacyUninstallerAfterInstall;
  InstallHardwareComponent;
  RestartOwnedHardwareServiceIfNeeded;
  if LegacyMigrationPath <> '' then
  begin
    if CleanupOwnedStartupEntries then
    begin
      Log('Trusted legacy installation migration completed.');
      if not WizardSilent then
        MsgBox(ExpandConstant('{cm:ApplicationLocationMigrationComplete}'), mbInformation, MB_OK);
    end
    else
    begin
      Log('Trusted legacy installation migration completed with startup cleanup warnings.');
      if not WizardSilent then
        MsgBox(
          FmtMessage(
            ExpandConstant('{cm:LegacyMigrationCleanupFailed}'), [ExpandConstant('{log}')]),
          mbError,
          MB_OK);
    end;
  end;
end;

procedure DeinitializeSetup;
begin
  RestartOwnedHardwareServiceIfNeeded;
  ReleaseApplicationPathLocks;
end;

function InitializeUninstall: Boolean;
begin
  if not DirExists(ExpandConstant('{app}')) then
  begin
    Log('The application directory is already absent; protected component cleanup will continue.');
    Result := True;
    Exit;
  end;

  Result :=
    IsSafeApplicationInstallTarget(ExpandConstant('{app}')) and
    AcquireApplicationPathLocks(ExpandConstant('{app}')) and
    HardenApplicationPayload;
  if not Result then
  begin
    Log('Uninstall refused an unlocked or unsafe application directory.');
    if not UninstallSilent then
      MsgBox(ExpandConstant('{cm:ApplicationLocationInvalid}'), mbError, MB_OK);
    ReleaseApplicationPathLocks;
  end;
end;

procedure RemoveOwnedHardwareService;
var
  ResultCode: Integer;
  HelperPath: String;
  MessageText: String;
begin
  HelperPath := GetHardwareRepairHelperPath;
  ResultCode := -1;
  if FileExists(HelperPath) and
    Exec(
      HelperPath,
      '--remove-service',
      '',
      SW_HIDE,
      ewWaitUntilTerminated,
    ResultCode) and (ResultCode = 0) then
    Exit;

  if not HardwareServiceExists then
    Exit;
  if not IsHardwareServiceOwned then
  begin
    MessageText := FmtMessage(
      ExpandConstant('{cm:HardwareServiceRemoveFailed}'), ['27']);
    Log(MessageText + ' Refusing sc.exe fallback for a foreign service.');
    if not UninstallSilent then
      MsgBox(MessageText, mbError, MB_OK);
    Exit;
  end;

  Log('Hardware repair helper could not remove the owned service; using the strict owned stop-and-delete fallback.');
  if (not RetireOwnedHardwareServiceAt(GetProtectedComponentRoot)) or
    HardwareServiceExists then
  begin
    MessageText := FmtMessage(
      ExpandConstant('{cm:HardwareServiceRemoveFailed}'), [IntToStr(ResultCode)]);
    Log(MessageText);
    if not UninstallSilent then
      MsgBox(MessageText, mbError, MB_OK);
  end;
end;

function CleanupOwnedStartupEntries: Boolean;
var
  HelperPath: String;
  ResultCode: Integer;
begin
  HelperPath := GetHardwareRepairHelperPath;
  ResultCode := -1;
  if not FileExists(HelperPath) then
  begin
    Log('Startup cleanup helper is missing; loaded user hives could not be inspected.');
    Result := False;
    Exit;
  end;

  Result := Exec(
    HelperPath,
    '--cleanup-startup',
    '',
    SW_HIDE,
    ewWaitUntilTerminated,
    ResultCode) and (ResultCode = 0);
  if not Result then
    Log('Restricted startup cleanup returned exit code ' + IntToStr(ResultCode) + '.');
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ApplicationPath: String;
  ResultCode: Integer;
  PawnIoInstaller: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    ApplicationPath := NormalizeDirectoryPath(ExpandConstant('{app}'));
    ReleaseApplicationPathLocks;
    if DirExists(ApplicationPath) and
      IsDirectoryEmpty(ApplicationPath) and
      (not ContainsReparsePoint(ApplicationPath)) and
      (not ContainsReparsePointInExistingAncestorChain(ApplicationPath)) then
    begin
      if RemoveDir(ApplicationPath) then
        Log('Removed the empty application directory after releasing path locks.')
      else
        Log('The empty application directory could not be removed.');
    end;
    Exit;
  end;

  if CurUninstallStep <> usUninstall then
    Exit;

  if not CleanupOwnedStartupEntries then
    Log('Uninstall startup cleanup did not complete.');
  RemoveOwnedHardwareService;

  PawnIoInstaller := AddBackslash(GetProtectedComponentRoot) +
    'Hardware\PawnIO_setup.exe';
  if (not UninstallSilent) and FileExists(PawnIoInstaller) and
    (MsgBox(
      ExpandConstant('{cm:RemovePawnIoPrompt}'),
      mbConfirmation,
      MB_YESNO or MB_DEFBUTTON2) = IDYES) then
  begin
    if (not Exec(
      PawnIoInstaller,
      '-uninstall -silent',
      '',
      SW_HIDE,
      ewWaitUntilTerminated,
      ResultCode)) or (ResultCode <> 0) then
    begin
      MsgBox(ExpandConstant('{cm:PawnIoRemoveFailed}'), mbError, MB_OK);
    end;
  end;
end;

procedure DeinitializeUninstall;
begin
  ReleaseApplicationPathLocks;
end;
