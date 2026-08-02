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
Name: "chinesesimp"; MessagesFile: "installer-assets\ChineseSimplified.isl"; InfoBeforeFile: "installer-assets\HardwareMonitoringDisclosure.zh-CN.txt"
Name: "english"; MessagesFile: "compiler:Default.isl"; InfoBeforeFile: "installer-assets\HardwareMonitoringDisclosure.en-US.txt"
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"; InfoBeforeFile: "installer-assets\HardwareMonitoringDisclosure.ja-JP.txt"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"; InfoBeforeFile: "installer-assets\HardwareMonitoringDisclosure.es-ES.txt"

[CustomMessages]
chinesesimp.LaunchProgram=启动 %1
chinesesimp.CompleteHardwareTask=安装完整硬件监控组件（推荐，将安装 PawnIO 驱动和以 LocalSystem 运行的只读硬件监控服务）
chinesesimp.HardwareMonitoringGroup=硬件监控
chinesesimp.HardwareRepairFailed=UniDesk 基础程序已安装，但硬件监控组件未能完成安装或修复（退出码 %1）。基础功能仍可正常使用；请在 UniDesk 设置中导出硬件诊断或稍后重试。详细日志位于 ProgramData\UniDesk\logs\hardware-repair.log。
chinesesimp.HardwareServiceRemoveFailed=未能删除 UniDesk 硬件监控服务（退出码 %1）。卸载将继续；请查看 ProgramData\UniDesk\logs\hardware-repair.log 并以管理员身份删除 UniDeskHardwareService。
chinesesimp.HardwareServiceStopFailed=无法停止现有硬件监控服务（退出码 %1）。请关闭 UniDesk 后重试安装。
chinesesimp.RemovePawnIoPrompt=是否同时卸载共享的 PawnIO 驱动？PawnIO 可能也被其他硬件监控或风扇控制软件使用。建议选择“否”并保留；只有确认没有其他程序依赖它时才选择“是”。
chinesesimp.PawnIoRemoveFailed=PawnIO 卸载失败。UniDesk 将继续卸载，但 PawnIO 会保留在系统中。
english.CompleteHardwareTask=Install complete hardware monitoring (recommended; installs the PawnIO driver and a read-only LocalSystem service)
english.HardwareMonitoringGroup=Hardware monitoring
english.HardwareRepairFailed=The UniDesk base application was installed, but hardware monitoring installation or repair did not complete (exit code %1). Base features remain available; export hardware diagnostics from Settings or retry later. Details are in ProgramData\UniDesk\logs\hardware-repair.log.
english.HardwareServiceRemoveFailed=The UniDesk hardware service could not be removed (exit code %1). Uninstall will continue; review ProgramData\UniDesk\logs\hardware-repair.log and remove UniDeskHardwareService as an administrator.
english.HardwareServiceStopFailed=The existing hardware service could not be stopped (exit code %1). Close UniDesk and retry setup.
english.RemovePawnIoPrompt=Also uninstall the shared PawnIO driver? Other hardware-monitoring or fan-control applications may use PawnIO. Keep it unless you are certain that no other application depends on it.
english.PawnIoRemoveFailed=PawnIO could not be uninstalled. UniDesk uninstall will continue and PawnIO will remain installed.
japanese.CompleteHardwareTask=完全なハードウェア監視をインストール（推奨。PawnIO ドライバーと LocalSystem の読み取り専用サービスをインストールします）
japanese.HardwareMonitoringGroup=ハードウェア監視
japanese.HardwareRepairFailed=UniDesk 本体はインストールされましたが、ハードウェア監視のインストールまたは修復を完了できませんでした（終了コード %1）。基本機能は使用できます。設定から診断をエクスポートするか、後でもう一度お試しください。詳細ログ：ProgramData\UniDesk\logs\hardware-repair.log。
japanese.HardwareServiceRemoveFailed=UniDesk ハードウェア監視サービスを削除できませんでした（終了コード %1）。アンインストールは続行します。ProgramData\UniDesk\logs\hardware-repair.log を確認し、管理者として UniDeskHardwareService を削除してください。
japanese.HardwareServiceStopFailed=既存のハードウェア監視サービスを停止できませんでした（終了コード %1）。UniDesk を終了してからセットアップを再試行してください。
japanese.RemovePawnIoPrompt=共有 PawnIO ドライバーもアンインストールしますか？他のハードウェア監視ソフトやファン制御ソフトが使用している場合があります。他のソフトが依存していないことを確認できる場合だけ削除してください。
japanese.PawnIoRemoveFailed=PawnIO をアンインストールできませんでした。UniDesk のアンインストールは続行し、PawnIO はシステムに残ります。
spanish.CompleteHardwareTask=Instalar supervisión completa de hardware (recomendado; instala el controlador PawnIO y un servicio de solo lectura LocalSystem)
spanish.HardwareMonitoringGroup=Supervisión de hardware
spanish.HardwareRepairFailed=La aplicación base UniDesk se instaló, pero no se completó la instalación o reparación de la supervisión de hardware (código %1). Las funciones básicas siguen disponibles; exporte el diagnóstico desde Configuración o vuelva a intentarlo más tarde. Registro: ProgramData\UniDesk\logs\hardware-repair.log.
spanish.HardwareServiceRemoveFailed=No se pudo eliminar el servicio de hardware de UniDesk (código %1). La desinstalación continuará; revise ProgramData\UniDesk\logs\hardware-repair.log y elimine UniDeskHardwareService como administrador.
spanish.HardwareServiceStopFailed=No se pudo detener el servicio de hardware existente (código %1). Cierre UniDesk y vuelva a ejecutar el instalador.
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
Source: "THIRD-PARTY-NOTICES.md"; DestDir: "{app}\licenses"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\icon\{#MyAppIconName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\icon\{#MyAppIconName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent runasoriginaluser

[UninstallRun]
Filename: "{sys}\schtasks.exe"; Parameters: "/Delete /TN ""\{#MyAppName}"" /F"; Flags: runhidden; RunOnceId: "DeleteUniDeskTask"
Filename: "{sys}\schtasks.exe"; Parameters: "/Delete /TN ""\LumiDesk"" /F"; Flags: runhidden; RunOnceId: "DeleteLegacyLumiDeskTask"
Filename: "{sys}\schtasks.exe"; Parameters: "/Delete /TN ""\VsirDesk"" /F"; Flags: runhidden; RunOnceId: "DeleteLegacyVsirDeskTask"

[Code]
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

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  Result := '';
  if HardwareServiceExists then
  begin
    if (not Exec(
      ExpandConstant('{sys}\sc.exe'),
      'stop {#HardwareServiceName}',
      '',
      SW_HIDE,
      ewWaitUntilTerminated,
      ResultCode)) or ((ResultCode <> 0) and (ResultCode <> 1062)) then
    begin
      Result := FmtMessage(
        ExpandConstant('{cm:HardwareServiceStopFailed}'), [IntToStr(ResultCode)]);
      Exit;
    end;
    Sleep(1500);
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep <> ssPostInstall then
    Exit;

  InstallHardwareComponent;
end;

procedure RemoveOwnedHardwareService;
var
  ResultCode: Integer;
  StopCode: Integer;
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

  Log('Hardware repair helper could not remove the owned service; using fixed sc.exe fallback.');
  StopCode := -1;
  Exec(
    ExpandConstant('{sys}\sc.exe'),
    'stop {#HardwareServiceName}',
    '',
    SW_HIDE,
    ewWaitUntilTerminated,
    StopCode);

  ResultCode := -1;
  if (not Exec(
    ExpandConstant('{sys}\sc.exe'),
    'delete {#HardwareServiceName}',
    '',
    SW_HIDE,
    ewWaitUntilTerminated,
    ResultCode)) or ((ResultCode <> 0) and (ResultCode <> 1060)) then
  begin
    MessageText := FmtMessage(
      ExpandConstant('{cm:HardwareServiceRemoveFailed}'), [IntToStr(ResultCode)]);
    Log(MessageText);
    if not UninstallSilent then
      MsgBox(MessageText, mbError, MB_OK);
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ResultCode: Integer;
  PawnIoInstaller: String;
begin
  if CurUninstallStep <> usUninstall then
    Exit;

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
