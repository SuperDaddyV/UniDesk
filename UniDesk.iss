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
DisableDirPage=yes
UsePreviousAppDir=no
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
chinesesimp.HardwareServiceRemoveFailed=未能删除 UniDesk 硬件监控服务（退出码 %1）。卸载将继续；请查看 ProgramData\UniDesk\logs\hardware-repair.log 并以管理员身份删除 UniDeskHardwareService。
chinesesimp.HardwareServiceStopFailed=无法停止现有硬件监控服务（退出码 %1）。请关闭 UniDesk 后重试安装。
chinesesimp.HardwareServiceOwnershipFailed=发现同名硬件服务，但它不属于当前 UniDesk 安装。为避免影响其他软件，安装已停止。
chinesesimp.HardwareAclFailed=无法安全保护 UniDesk 安装目录。安装已停止，请改用默认安装位置后重试。
chinesesimp.HardwareProtectedLocationRequired=为保护管理员卸载器和硬件维护组件，UniDesk 必须安装在系统 Program Files 的默认 UniDesk 目录中。安装已停止，请重新运行安装器。
chinesesimp.HardwareUnsafeServiceRetired=旧版 UniDesk 已安全迁移到受保护位置，可确认属于旧版的启动项已清理。安装器没有运行旧卸载程序，也没有删除旧目录；您现在可以在资源管理器中手动删除旧程序文件夹，用户数据不受影响。
chinesesimp.HardwareUnsafeServiceRetirementFailed=检测到旧版 UniDesk 在非受保护目录运行硬件服务，但无法确认它已安全停止并禁用。安装已停止；请不要继续使用旧硬件服务，并查看安装日志后手动删除 UniDeskHardwareService。
chinesesimp.LegacyMigrationCleanupFailed=新版 UniDesk 已安装到受保护位置，但未能完整清理旧版启动项。请不要运行旧卸载程序；请查看安装日志，确认旧版不再开机启动后，再在资源管理器中手动删除旧程序文件夹。
chinesesimp.RemovePawnIoPrompt=是否同时卸载共享的 PawnIO 驱动？PawnIO 可能也被其他硬件监控或风扇控制软件使用。建议选择“否”并保留；只有确认没有其他程序依赖它时才选择“是”。
chinesesimp.PawnIoRemoveFailed=PawnIO 卸载失败。UniDesk 将继续卸载，但 PawnIO 会保留在系统中。
english.CompleteHardwareTask=Install complete hardware monitoring (recommended; installs the PawnIO driver and a read-only LocalSystem service)
english.HardwareMonitoringGroup=Hardware monitoring
english.HardwareRepairFailed=The UniDesk base application was installed, but hardware monitoring installation or repair did not complete (exit code %1). Base features remain available; export hardware diagnostics from Settings or retry later. Details are in ProgramData\UniDesk\logs\hardware-repair.log.
english.HardwareServiceRemoveFailed=The UniDesk hardware service could not be removed (exit code %1). Uninstall will continue; review ProgramData\UniDesk\logs\hardware-repair.log and remove UniDeskHardwareService as an administrator.
english.HardwareServiceStopFailed=The existing hardware service could not be stopped (exit code %1). Close UniDesk and retry setup.
english.HardwareServiceOwnershipFailed=A service with the same name exists but does not belong to this UniDesk installation. Setup stopped to avoid changing another application.
english.HardwareAclFailed=The UniDesk installation directory could not be secured. Setup has stopped; retry with the default installation location.
english.HardwareProtectedLocationRequired=To protect the administrator uninstaller and hardware maintenance tools, UniDesk must use its default directory under the system Program Files folder. Setup stopped; run it again without overriding the install directory.
english.HardwareUnsafeServiceRetired=The older UniDesk installation was safely migrated to the protected location, and strictly owned legacy startup entries were cleaned. Setup did not run the old uninstaller or delete the old directory. You can now delete the old program folder manually in File Explorer; user data is unaffected.
english.HardwareUnsafeServiceRetirementFailed=An older UniDesk hardware service is running from an unprotected directory, but setup could not confirm that it was safely stopped and disabled. Setup stopped; do not keep using the old service, and remove UniDeskHardwareService manually after reviewing the setup log.
english.LegacyMigrationCleanupFailed=The new UniDesk version was installed in the protected location, but legacy startup cleanup did not complete. Do not run the old uninstaller. Review the setup log, confirm the old version no longer starts with Windows, and then delete the old program folder manually in File Explorer.
english.RemovePawnIoPrompt=Also uninstall the shared PawnIO driver? Other hardware-monitoring or fan-control applications may use PawnIO. Keep it unless you are certain that no other application depends on it.
english.PawnIoRemoveFailed=PawnIO could not be uninstalled. UniDesk uninstall will continue and PawnIO will remain installed.
japanese.CompleteHardwareTask=完全なハードウェア監視をインストール（推奨。PawnIO ドライバーと LocalSystem の読み取り専用サービスをインストールします）
japanese.HardwareMonitoringGroup=ハードウェア監視
japanese.HardwareRepairFailed=UniDesk 本体はインストールされましたが、ハードウェア監視のインストールまたは修復を完了できませんでした（終了コード %1）。基本機能は使用できます。設定から診断をエクスポートするか、後でもう一度お試しください。詳細ログ：ProgramData\UniDesk\logs\hardware-repair.log。
japanese.HardwareServiceRemoveFailed=UniDesk ハードウェア監視サービスを削除できませんでした（終了コード %1）。アンインストールは続行します。ProgramData\UniDesk\logs\hardware-repair.log を確認し、管理者として UniDeskHardwareService を削除してください。
japanese.HardwareServiceStopFailed=既存のハードウェア監視サービスを停止できませんでした（終了コード %1）。UniDesk を終了してからセットアップを再試行してください。
japanese.HardwareServiceOwnershipFailed=同名のサービスがありますが、この UniDesk インストールのものではありません。他のアプリを変更しないよう、インストールを中止しました。
japanese.HardwareAclFailed=UniDesk のインストール先を安全に保護できませんでした。インストールを中止しました。既定の場所で再試行してください。
japanese.HardwareProtectedLocationRequired=管理者権限のアンインストーラーとハードウェア保守ツールを保護するため、UniDesk はシステムの Program Files 内の既定フォルダーにインストールする必要があります。インストールを中止しました。場所を上書きせずに再実行してください。
japanese.HardwareUnsafeServiceRetired=旧版 UniDesk を保護された場所へ安全に移行し、旧版に厳密に属するスタートアップ項目をクリーンアップしました。旧アンインストーラーの実行や旧フォルダーの削除は行っていません。エクスプローラーで旧プログラムフォルダーを手動削除できます。ユーザーデータには影響しません。
japanese.HardwareUnsafeServiceRetirementFailed=保護されていない場所の旧版 UniDesk ハードウェアサービスを安全に停止して無効化できたことを確認できません。インストールを中止しました。ログを確認し、UniDeskHardwareService を手動で削除してください。
japanese.LegacyMigrationCleanupFailed=新版 UniDesk は保護された場所にインストールされましたが、旧版のスタートアップ項目を完全にクリーンアップできませんでした。旧アンインストーラーは実行しないでください。ログを確認し、旧版が自動起動しないことを確認してから、エクスプローラーで旧プログラムフォルダーを手動削除してください。
japanese.RemovePawnIoPrompt=共有 PawnIO ドライバーもアンインストールしますか？他のハードウェア監視ソフトやファン制御ソフトが使用している場合があります。他のソフトが依存していないことを確認できる場合だけ削除してください。
japanese.PawnIoRemoveFailed=PawnIO をアンインストールできませんでした。UniDesk のアンインストールは続行し、PawnIO はシステムに残ります。
spanish.CompleteHardwareTask=Instalar supervisión completa de hardware (recomendado; instala el controlador PawnIO y un servicio de solo lectura LocalSystem)
spanish.HardwareMonitoringGroup=Supervisión de hardware
spanish.HardwareRepairFailed=La aplicación base UniDesk se instaló, pero no se completó la instalación o reparación de la supervisión de hardware (código %1). Las funciones básicas siguen disponibles; exporte el diagnóstico desde Configuración o vuelva a intentarlo más tarde. Registro: ProgramData\UniDesk\logs\hardware-repair.log.
spanish.HardwareServiceRemoveFailed=No se pudo eliminar el servicio de hardware de UniDesk (código %1). La desinstalación continuará; revise ProgramData\UniDesk\logs\hardware-repair.log y elimine UniDeskHardwareService como administrador.
spanish.HardwareServiceStopFailed=No se pudo detener el servicio de hardware existente (código %1). Cierre UniDesk y vuelva a ejecutar el instalador.
spanish.HardwareServiceOwnershipFailed=Existe un servicio con el mismo nombre, pero no pertenece a esta instalación de UniDesk. La instalación se detuvo para no modificar otra aplicación.
spanish.HardwareAclFailed=No se pudo proteger el directorio de instalación de UniDesk. La instalación se detuvo; vuelva a intentarlo con la ubicación predeterminada.
spanish.HardwareProtectedLocationRequired=Para proteger el desinstalador administrativo y las herramientas de mantenimiento, UniDesk debe usar su carpeta predeterminada en Program Files del sistema. La instalación se detuvo; vuelva a ejecutarla sin cambiar la carpeta.
spanish.HardwareUnsafeServiceRetired=La instalación antigua de UniDesk se migró de forma segura a la ubicación protegida y se limpiaron las entradas de inicio que pertenecían estrictamente a ella. El instalador no ejecutó el desinstalador antiguo ni eliminó su carpeta. Ahora puede eliminar manualmente la carpeta antigua desde el Explorador; los datos de usuario no se ven afectados.
spanish.HardwareUnsafeServiceRetirementFailed=No se pudo confirmar que el servicio de hardware antiguo de UniDesk se detuviera y deshabilitara de forma segura. La instalación se detuvo; revise el registro y elimine UniDeskHardwareService manualmente.
spanish.LegacyMigrationCleanupFailed=La nueva versión de UniDesk se instaló en la ubicación protegida, pero no se completó la limpieza de las entradas de inicio antiguas. No ejecute el desinstalador antiguo. Revise el registro, confirme que la versión anterior ya no se inicia con Windows y elimine manualmente su carpeta desde el Explorador.
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
Source: "{#MyHardwareServiceSourceDir}\*"; DestDir: "{app}\HardwareService"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#MyHardwareRepairSourceDir}\*"; DestDir: "{app}\HardwareRepair"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "installer-assets\PawnIO_setup.exe"; DestDir: "{app}\Hardware"; Flags: ignoreversion
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
  LegacyStartupMigrationMarkerName = '.unidesk-legacy-startup-path';

var
  LegacyMigrationPath: String;
  LegacyRegisteredPathInvalid: Boolean;
  ValidatedAclTarget: String;
  StoppedOwnedHardwareService: Boolean;

function CleanupOwnedStartupEntries: Boolean; forward;
function SetEnvironmentVariable(Name, Value: String): Boolean;
  external 'SetEnvironmentVariableW@kernel32.dll stdcall';

function HardwareServiceExists: Boolean;
begin
  Result := RegKeyExists(
    HKLM,
    'SYSTEM\CurrentControlSet\Services\{#HardwareServiceName}');
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

procedure InstallHardwareComponent;
var
  ResultCode: Integer;
  HelperPath: String;
begin
  HelperPath := ExpandConstant('{app}\HardwareRepair\UniDesk.HardwareRepair.exe');
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

  if (not Exec(
    HelperPath,
    '--install-or-repair',
    '',
    SW_HIDE,
    ewWaitUntilTerminated,
    ResultCode)) or (ResultCode <> 0) then
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

function IsHardwareServiceOwnedAt(const ApplicationPath: String): Boolean;
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
      AddBackslash(ApplicationPath) +
      'HardwareService\UniDesk.HardwareService.exe'))) = 0;
end;

function IsHardwareServiceOwned: Boolean;
begin
  Result := IsHardwareServiceOwnedAt(ExpandConstant('{app}'));
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

function IsProtectedInstallTargetAllowed: Boolean;
begin
  Result := IsSameDirectory(
    ExpandConstant('{app}'),
    ExpandConstant('{autopf}\{#MyAppName}'));
end;

function IsProtectedHardwareInstallTarget(const DirectoryPath: String): Boolean;
begin
  Result := IsSameDirectory(
    DirectoryPath,
    ExpandConstant('{autopf}\{#MyAppName}'));
end;

function VerifyProtectedInstallAncestorAcl: Boolean;
var
  Parameters: String;
  ProtectedRoot: String;
  ResultCode: Integer;
begin
  ProtectedRoot := NormalizeDirectoryPath(ExpandConstant('{autopf}'));
  if not SetEnvironmentVariable('UNIDESK_PROTECTED_ROOT', ProtectedRoot) then
  begin
    Log('Could not bind the protected install root for ancestor ACL verification.');
    Result := False;
    Exit;
  end;

  Parameters :=
    '-NoLogo -NoProfile -NonInteractive -Command "' +
    '$trusted=@(''S-1-5-18'',''S-1-5-32-544'',''S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464''); ' +
    '$danger=[Security.AccessControl.FileSystemRights]::Delete -bor [Security.AccessControl.FileSystemRights]::DeleteSubdirectoriesAndFiles -bor [Security.AccessControl.FileSystemRights]::ChangePermissions -bor [Security.AccessControl.FileSystemRights]::TakeOwnership; ' +
    '$current=Get-Item -LiteralPath $env:UNIDESK_PROTECTED_ROOT -Force -ErrorAction Stop; ' +
    'while($null -ne $current){ ' +
    'if(($current.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){exit 20}; ' +
    '$acl=Get-Acl -LiteralPath $current.FullName -ErrorAction Stop; ' +
    '$owner=$acl.GetOwner([Security.Principal.SecurityIdentifier]).Value; ' +
    'if($trusted -notcontains $owner){exit 21}; ' +
    'foreach($rule in $acl.GetAccessRules($true,$true,[Security.Principal.SecurityIdentifier])){ ' +
    'if(($rule.AccessControlType -eq [Security.AccessControl.AccessControlType]::Allow) -and ' +
    '(($rule.PropagationFlags -band [Security.AccessControl.PropagationFlags]::InheritOnly) -eq 0) -and ' +
    '(($rule.FileSystemRights -band $danger) -ne 0) -and ' +
    '($trusted -notcontains $rule.IdentityReference.Value)){exit 22} }; ' +
    '$current=$current.Parent }; exit 0"';
  ResultCode := -1;
  Result := Exec(
    ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
    Parameters,
    '',
    SW_HIDE,
    ewWaitUntilTerminated,
    ResultCode) and (ResultCode = 0);
  SetEnvironmentVariable('UNIDESK_PROTECTED_ROOT', '');
  if not Result then
    Log('Protected install ancestor ACL verification failed with exit code ' +
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
    IsSameDirectory(NormalizedPath, ExpandConstant('{commonappdata}')) or
    IsSameDirectory(NormalizedPath, ExpandConstant('{userprofile}')) or
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

function GetRegisteredUniDeskInstallationDirectory(var RegisteredPath: String): Boolean;
begin
  Result := RegQueryStringValue(
    HKLM64,
    'Software\Microsoft\Windows\CurrentVersion\Uninstall\{4B0F3B03-7F5D-4B5D-B2F4-6816B931C7D2}_is1',
    'InstallLocation',
    RegisteredPath);
  if not Result then
    Result := RegQueryStringValue(
      HKLM32,
      'Software\Microsoft\Windows\CurrentVersion\Uninstall\{4B0F3B03-7F5D-4B5D-B2F4-6816B931C7D2}_is1',
      'InstallLocation',
      RegisteredPath);
end;

function HasLegacyUnsafeRegisteredInstallation(
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

  Result := not IsProtectedHardwareInstallTarget(RegisteredPath);
end;

function IsKnownUniDeskInstallationDirectory(const NormalizedPath: String): Boolean;
var
  RegisteredPath: String;
begin
  Result := GetRegisteredUniDeskInstallationDirectory(RegisteredPath);
  Result := Result and IsSameDirectory(NormalizedPath, RegisteredPath);
end;

function IsSafeInstallationTargetForAcl(const DirectoryPath: String): Boolean;
var
  NormalizedPath: String;
begin
  NormalizedPath := NormalizeDirectoryPath(DirectoryPath);
  if (NormalizedPath = '') or IsProtectedBroadDirectory(NormalizedPath) then
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

function GetLegacyUnsafeOwnedServicePath(var OwnedApplicationPath: String): Boolean;
var
  RegisteredPath: String;
begin
  Result := False;
  OwnedApplicationPath := '';
  if not HardwareServiceExists then
    Exit;

  if (not IsProtectedHardwareInstallTarget(ExpandConstant('{app}'))) and
    IsHardwareServiceOwnedAt(ExpandConstant('{app}')) then
  begin
    OwnedApplicationPath := ExpandConstant('{app}');
    Result := True;
    Exit;
  end;

  if GetRegisteredUniDeskInstallationDirectory(RegisteredPath) and
    (not IsProtectedHardwareInstallTarget(RegisteredPath)) and
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

function RetireUnsafeOwnedHardwareService(
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
    Log('Legacy hardware service ownership changed before retirement; refusing to act.');
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
  Log('Legacy unsafe hardware service retirement: disabled=' +
    BooleanLogValue(DisableSafe) + '; stopped=' + BooleanLogValue(StopSafe) +
    '; deleted=' + BooleanLogValue(DeleteSafe) + '.');
end;

function RetireLegacyUnsafeHardwareService(var Retired: Boolean): Boolean;
var
  OwnedApplicationPath: String;
begin
  Retired := GetLegacyUnsafeOwnedServicePath(OwnedApplicationPath);
  if not Retired then
  begin
    Result := True;
    Exit;
  end;

  Log('Retiring owned hardware service from legacy unsafe path: ' +
    OwnedApplicationPath);
  Result := RetireUnsafeOwnedHardwareService(OwnedApplicationPath);
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

function HardenInstallationPayload: Boolean;
var
  AppPath: String;
begin
  AppPath := ExpandConstant('{app}');
  if ((ValidatedAclTarget = '') and not IsSafeInstallationTargetForAcl(AppPath)) or
    ((ValidatedAclTarget <> '') and not IsSameDirectory(AppPath, ValidatedAclTarget)) then
  begin
    Log('Refusing recursive ACL hardening for unsafe or unowned install target: ' + AppPath);
    Result := False;
    Exit;
  end;

  Result :=
    ForceDirectories(AppPath) and
    ForceDirectories(AddBackslash(AppPath) + 'HardwareService') and
    ForceDirectories(AddBackslash(AppPath) + 'HardwareRepair') and
    ForceDirectories(AddBackslash(AppPath) + 'Hardware');
  if not Result then
    Exit;

  Result :=
    HardenDirectoryAcl(AppPath) and
    HardenDirectoryAcl(AddBackslash(AppPath) + 'HardwareService') and
    HardenDirectoryAcl(AddBackslash(AppPath) + 'HardwareRepair') and
    HardenDirectoryAcl(AddBackslash(AppPath) + 'Hardware');
  if Result and (ValidatedAclTarget = '') then
    ValidatedAclTarget := NormalizeDirectoryPath(AppPath);
end;

function PersistLegacyStartupMigrationMarker: Boolean;
var
  MarkerPath: String;
begin
  if LegacyMigrationPath = '' then
  begin
    Result := True;
    Exit;
  end;

  MarkerPath := AddBackslash(ExpandConstant('{app}')) +
    LegacyStartupMigrationMarkerName;
  Result := SaveStringToFile(MarkerPath, LegacyMigrationPath, False);
  if not Result then
    Log('Could not persist the protected legacy startup migration marker.');
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
  LegacyUnsafeInstallation: Boolean;
  RetiredLegacyService: Boolean;
  ResultCode: Integer;
  TaskKillCode: Integer;
begin
  Result := '';
  LegacyUnsafeInstallation := HasLegacyUnsafeRegisteredInstallation(
    LegacyInstallPath);
  if not RetireLegacyUnsafeHardwareService(RetiredLegacyService) then
  begin
    Result := ExpandConstant('{cm:HardwareUnsafeServiceRetirementFailed}');
    Exit;
  end;
  if LegacyRegisteredPathInvalid then
  begin
    Log('The registered legacy installation directory is empty or dangerously broad.');
    Result := ExpandConstant('{cm:HardwareAclFailed}');
    Exit;
  end;
  if LegacyUnsafeInstallation then
  begin
    LegacyMigrationPath := LegacyInstallPath;
    Log('Preparing trusted migration from legacy unsafe installation directory: ' +
      LegacyMigrationPath + '. Owned hardware service retired=' +
      BooleanLogValue(RetiredLegacyService) + '.');
  end
  else
    LegacyMigrationPath := '';

  if not IsProtectedInstallTargetAllowed then
  begin
    Log('Complete hardware monitoring was rejected outside the protected Program Files target.');
    Result := ExpandConstant('{cm:HardwareProtectedLocationRequired}');
    Exit;
  end;

  if not VerifyProtectedInstallAncestorAcl then
  begin
    Result := ExpandConstant('{cm:HardwareAclFailed}');
    Exit;
  end;

  if not IsSafeInstallationTargetForAcl(ExpandConstant('{app}')) then
  begin
    Log('Refusing installation into unsafe or unowned target: ' + ExpandConstant('{app}'));
    Result := ExpandConstant('{cm:HardwareAclFailed}');
    Exit;
  end;

  if not HardenInstallationPayload then
  begin
    Result := ExpandConstant('{cm:HardwareAclFailed}');
    Exit;
  end;

  if not PersistLegacyStartupMigrationMarker then
  begin
    Result := ExpandConstant('{cm:HardwareAclFailed}');
    Exit;
  end;

  if HardwareServiceExists and (not RetiredLegacyService) then
  begin
    if not IsHardwareServiceOwned then
    begin
      Log('Refusing to stop a foreign service named {#HardwareServiceName}.');
      Result := ExpandConstant('{cm:HardwareServiceOwnershipFailed}');
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
        ExpandConstant('{cm:HardwareServiceStopFailed}'), [IntToStr(ResultCode)]);
      Exit;
    end;
    StoppedOwnedHardwareService := ResultCode = 0;
    if HardwareServiceExists then
    begin
      if not IsHardwareServiceOwned then
      begin
        Result := ExpandConstant('{cm:HardwareServiceOwnershipFailed}');
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
          ExpandConstant('{cm:HardwareServiceStopFailed}'), [IntToStr(TaskKillCode)]);
        Exit;
      end;
    end;
  end;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if ((CurPageID = wpSelectDir) or (CurPageID = wpSelectTasks)) and
    not IsProtectedInstallTargetAllowed then
  begin
    Log('Complete hardware monitoring target rejected on the task selection page.');
    MsgBox(ExpandConstant('{cm:HardwareProtectedLocationRequired}'), mbError, MB_OK);
    Result := False;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep <> ssPostInstall then
    Exit;

  if not HardenInstallationPayload then
  begin
    RestartOwnedHardwareServiceIfNeeded;
    ReportHardwareComponentFailure(90);
    Exit;
  end;

  InstallHardwareComponent;
  RestartOwnedHardwareServiceIfNeeded;
  if LegacyMigrationPath <> '' then
  begin
    if CleanupOwnedStartupEntries then
    begin
      Log('Trusted legacy installation migration completed.');
      if not WizardSilent then
        MsgBox(ExpandConstant('{cm:HardwareUnsafeServiceRetired}'), mbInformation, MB_OK);
    end
    else
    begin
      Log('Trusted legacy installation migration completed with startup cleanup warnings.');
      if not WizardSilent then
        MsgBox(ExpandConstant('{cm:LegacyMigrationCleanupFailed}'), mbError, MB_OK);
    end;
  end;
end;

procedure DeinitializeSetup;
begin
  RestartOwnedHardwareServiceIfNeeded;
end;

procedure RemoveOwnedHardwareService;
var
  ResultCode: Integer;
  HelperPath: String;
  MessageText: String;
begin
  HelperPath := ExpandConstant('{app}\HardwareRepair\UniDesk.HardwareRepair.exe');
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
  if (not RetireUnsafeOwnedHardwareService(ExpandConstant('{app}'))) or
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
  HelperPath := ExpandConstant('{app}\HardwareRepair\UniDesk.HardwareRepair.exe');
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
  ResultCode: Integer;
  PawnIoInstaller: String;
begin
  if CurUninstallStep <> usUninstall then
    Exit;

  if not CleanupOwnedStartupEntries then
    Log('Uninstall startup cleanup did not complete.');
  RemoveOwnedHardwareService;

  PawnIoInstaller := ExpandConstant('{app}\Hardware\PawnIO_setup.exe');
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
