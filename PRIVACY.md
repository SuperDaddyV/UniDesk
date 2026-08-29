# UniDesk Privacy Policy

Last updated: 2026-08-30

This program will not transfer any information to other networked systems unless specifically requested by the user or the person installing or operating it.

UniDesk is a local-first Windows desktop application. It has no user account system, advertising, analytics, telemetry, or cloud synchronization. Its limited network operations are part of the user-operated features and choices described below.

## Information stored locally

UniDesk stores settings, shortcuts, todos, quick notes, quick text, clipboard history, cached weather, Model Radar cache, icon cache, and application logs under `%LOCALAPPDATA%\UniDesk`. Model Radar public evaluation data is cached at `%LOCALAPPDATA%\UniDesk\cache\modeldial-radar.json` and is excluded from user backups because it can be downloaded again. Clipboard history is enabled by default for fresh installations; upgrades preserve the existing choice. Its content stays on this device, and users can disable it under **Settings > Desktop Experience** or clear it under **Settings > Data & Backup**. Hardware-component maintenance writes a local diagnostic log under `%ProgramData%\UniDesk\logs`.

Weather API credentials and clipboard-history content are protected at rest with Windows Data Protection API for the current Windows user. Clipboard sensitive-content filtering reduces accidental collection but cannot guarantee that every secret is detected. Users handling sensitive content should disable clipboard history or clear it after use.

Local backups exclude weather API credentials. Clipboard history is excluded by default and is included only when the user explicitly selects that option; an exported backup containing clipboard history should therefore be treated as sensitive.

## Network operations

### Weather and manual city lookup

The time-and-weather module and automatic location are enabled by default for fresh installations; upgrades preserve the existing choice. While that module is enabled and valid weather credentials are available, UniDesk refreshes weather when the module starts, approximately every 30 minutes, after relevant settings change, or when the user manually refreshes it. UniDesk sends the configured manual city name to the active QWeather HTTPS API. When automatic location is enabled, UniDesk instead asks Windows for device coordinates and sends those coordinates to the QWeather HTTPS API to resolve a city and retrieve weather data. UniDesk does not send clipboard history, notes, todos, shortcuts, or hardware readings with a weather request. Users can disable automatic location in settings; disabling the time-and-weather module stops its periodic refresh.

QWeather processes these requests under the [QWeather Privacy Policy](https://www.qweather.com/terms/privacy).

### Windows location

Automatic location is enabled by default for fresh installations and can be disabled in settings; upgrades do not overwrite the existing choice. Windows controls location permission and may process location according to the [Microsoft Privacy Statement](https://www.microsoft.com/en-us/privacy/privacystatement). UniDesk cannot bypass or enable Windows location permission. Enabling automatic location clears the manually entered city; manually entering a city disables automatic location.

### Model Radar

Model Radar is disabled by default for fresh installations and upgrades. While the user has enabled the module, UniDesk reads its local cache first and accesses only the fixed ModelDial HTTPS endpoint `https://modeldial.com/api/v1/radar/latest.json` when a refresh is due or the user manually refreshes. The request uses the system proxy and identifies the application version; it does not include clipboard history, notes, todos, shortcuts, weather location, hardware readings, model-tool configuration, or model prompts. Disabling the module stops scheduled refreshes and cancels any request in progress. UniDesk does not run models or perform local evaluation. The downloaded public evaluation data is provided under [CC BY 4.0](https://modeldial.com/data-license), and leaderboard links open only after a user action.

### Update checks

UniDesk contacts the GitHub Releases API only when the user selects **Check for updates**. The request is subject to the [GitHub General Privacy Statement](https://docs.github.com/en/site-policy/privacy-policies/github-general-privacy-statement). Opening a release page or another external link is also a user-initiated browser action.

## Hardware monitoring component

The optional hardware monitoring component runs locally as a read-only Windows service. It exchanges a versioned, sanitized sensor snapshot with the ordinary-permission UniDesk process through a local named pipe. It does not upload hardware readings or accept arbitrary commands, scripts, or file paths.

## Retention and deletion

Users can clear clipboard and quick-text history from UniDesk settings. Uninstalling the application intentionally preserves `%LOCALAPPDATA%\UniDesk` so a later reinstall or upgrade can retain user data. To remove all retained data, exit UniDesk and delete `%LOCALAPPDATA%\UniDesk`; hardware-repair logs can be removed separately from `%ProgramData%\UniDesk\logs`.

## Contact

Privacy questions and defect reports can be submitted through [GitHub Issues](https://github.com/SuperDaddyV/UniDesk/issues).

---

# UniDesk 隐私政策

最后更新：2026-08-30

除非用户或安装、运行本程序的人明确请求，否则本程序不会向其他联网系统传输任何信息。

UniDesk 是一款本地优先的 Windows 桌面应用，不提供用户账户、广告、分析、遥测或云同步。其有限网络请求仅用于下述由用户操作和选择的功能。

## 本地存储的信息

UniDesk 将设置、快捷方式、待办事项、快速便签、快捷文本、剪贴板历史、天气缓存、模型雷达缓存、图标缓存和应用日志存储在 `%LOCALAPPDATA%\UniDesk`。模型雷达公共评测数据缓存位于 `%LOCALAPPDATA%\UniDesk\cache\modeldial-radar.json`，属于可重新下载的数据，不进入用户备份。全新安装默认启用剪贴板历史，覆盖安装和升级保留原有选择；历史正文只保存在本机，用户可在「设置 > 桌面体验」中关闭，并在「设置 > 数据与备份」中清理。硬件组件维护日志存储在 `%ProgramData%\UniDesk\logs`。

天气 API 凭据和剪贴板历史正文使用 Windows 当前用户范围的 DPAPI 保护。剪贴板敏感内容过滤只能降低误存风险，无法保证识别全部秘密；处理敏感内容时应关闭剪贴板历史或及时清理。

本地备份始终排除天气 API 凭据。剪贴板历史默认不进入备份，只有用户明确选择后才会包含；包含剪贴板历史的备份文件应按敏感文件管理。

## 网络请求

### 天气与手动城市查询

时间天气模块默认启用。全新安装默认启用自动定位；覆盖安装和升级保留用户原有选择。模块启用且存在有效天气凭据时，UniDesk 会在模块启动、约每 30 分钟、相关设置变更后或用户手动刷新时请求天气。应用会将已配置的手动城市名称发送到当前有效的和风天气 HTTPS API；自动定位启用时，应用会改为向 Windows 请求设备坐标，再将坐标发送到和风天气 HTTPS API，用于解析城市并获取天气。天气请求不会附带剪贴板历史、便签、待办事项、快捷方式或硬件读数。用户可在设置中关闭自动定位，关闭时间天气模块会停止其定时刷新。

和风天气按照其[隐私政策](https://www.qweather.com/terms/privacy)处理这些请求。

### Windows 定位

全新安装默认开启自动定位，用户可随时在设置中关闭；覆盖安装和升级不会改写原有选择。定位权限由 Windows 管理，Windows 可能按照 [Microsoft 隐私声明](https://www.microsoft.com/en-us/privacy/privacystatement)处理位置，UniDesk 不能自行绕过或开启系统定位权限。开启自动定位会清除手动城市；手动输入城市会关闭自动定位。

### 模型雷达

模型雷达默认关闭，包括全新安装和覆盖升级。只有用户启用模块后，UniDesk 才会先读取本地缓存，并在到期刷新或用户手动刷新时访问固定的 ModelDial HTTPS 接口 `https://modeldial.com/api/v1/radar/latest.json`。请求使用系统代理并标识应用版本，不会附带剪贴板历史、便签、待办事项、快捷方式、天气位置、硬件读数、模型工具配置或模型提示词。关闭模块会停止计划刷新并取消在途请求。UniDesk 不调用模型，也不执行本地评测。下载的公共评测数据依据 [CC BY 4.0](https://modeldial.com/data-license)提供，完整榜单链接只在用户主动点击后打开。

### 更新检查

只有用户点击「检查更新」时，UniDesk 才会访问 GitHub Releases API。该请求适用 [GitHub 通用隐私声明](https://docs.github.com/en/site-policy/privacy-policies/github-general-privacy-statement)。打开发布页面或其他外部链接同样属于用户主动触发的浏览器操作。

## 硬件监控组件

可选硬件监控组件以本地只读 Windows 服务运行，通过本机命名管道与普通权限的 UniDesk 进程交换版本化、脱敏的传感器快照。它不上传硬件读数，也不接受任意命令、脚本或文件路径。

## 保留与删除

用户可以在 UniDesk 设置中清理剪贴板和快捷文本历史。卸载应用时会有意保留 `%LOCALAPPDATA%\UniDesk`，以便重新安装或升级时保留用户数据。若需彻底删除，请退出 UniDesk 后手动删除 `%LOCALAPPDATA%\UniDesk`；硬件修复日志可从 `%ProgramData%\UniDesk\logs` 单独删除。

## 联系方式

隐私问题和缺陷可通过 [GitHub Issues](https://github.com/SuperDaddyV/UniDesk/issues)提交。
