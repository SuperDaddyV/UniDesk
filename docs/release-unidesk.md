# UniDesk Release Notes

## Code signing policy

Public packages follow the [UniDesk code signing policy](../CODE_SIGNING_POLICY.md) and [privacy policy](../PRIVACY.md). `v2.1.0` uses the explicitly approved unsigned-stable exception; it must be rebuilt from the documented public source revision, pass the unsigned readiness gate and manual matrix, and disclose `Authenticode: NotSigned` plus Windows SmartScreen or enterprise-policy risk.

Free code signing provided by SignPath.io, certificate by SignPath Foundation.

## v2.1.0

This release makes complete hardware monitoring installable without elevating the main app, closes weather and clipboard privacy gaps, and moves the supported runtime to .NET 10 LTS.

Distribution notice: the `v2.1.0` installer and first-party binaries are unsigned. Verify the published SHA-256 before use; Windows SmartScreen or enterprise policy may warn or block installation.

### Changes

- Added an opt-in Model Radar module backed only by ModelDial's fixed public `latest.json` endpoint. It presents the publisher's overall leader and first official `value` recommendation, stable Top 5 views for overall／backend／frontend／knowledge scores, explicit cache and offline states, bounded refresh, cancellation, and fixed attribution links without calling any model or changing model-tool configuration.
- Fresh installations now enable Time & Weather, Hardware Monitor, Todos, and Quick Notes while leaving Shortcuts, Quick Text, and Model Radar disabled. Upgrades preserve every saved module switch and ordering choice.
- Added a default-selected, fully disclosed hardware component that installs the pinned PawnIO driver and a narrowly scoped read-only Windows service while keeping `UniDesk.exe` at normal-user privilege.
- Added a dedicated administrator-only repair helper with argument-safe service registration, stable exit codes, local logs, and a versioned IPC health check. The main app waits for repair completion and then refreshes the component state.
- Added bounded service initialization retry, four parallel named-pipe accept loops, detailed service／driver／protocol diagnostics, and localized repair states.
- Isolated LibreHardwareMonitor's AMD GPU update path after it was observed terminating the hardware service with a native access violation. AMD usage and temperature continue through AMD ADL when supported, Windows GPU Engine remains the usage fallback, and unavailable values are shown as unavailable instead of crashing the service or reporting a false zero.
- Fixed global clipboard search to search DPAPI-decrypted content instead of encrypted database values; repeated searches now cancel older work and database search runs off the UI thread.
- Fresh installs now enable start-with-Windows and automatic weather location by default, while upgrades preserve existing choices. Coordinates are sent only to the configured QWeather HTTPS API for city lookup; manual city remains the fallback, legacy placeholder values are ignored, and Settings provides a direct Windows location-settings shortcut.
- Treats a missing or invalid automatic-location setting as disabled, preventing an implicit Windows location request after incomplete or legacy settings.
- Added a visible QWeather attribution link in both expanded and collapsed weather views, plus public code-signing and bilingual privacy policies for release transparency.
- Restricted user weather hosts to dedicated HTTPS `*.qweatherapi.com` domains, requires Host and Key as a pair, validates changed credentials before saving, and excludes both values from backup and restore.
- Migrated all projects and CI to .NET 10 LTS, upgraded Microsoft packages, pinned the patched SQLite native dependency, and added transitive vulnerability and version-consistency gates.
- Added four-language installer disclosure, a signed-artifact release-readiness script, and zero-warning Release builds.
- Restricted the installer to native Windows x64 so ARM64 emulation cannot accept an incompatible x64 kernel driver. Hardware-component failures now produce a non-fatal diagnostic warning while the base application remains installed, and setup auto-launches the app with the original normal-user token.
- Added a persistent single-selection highlight for the clipboard-history retention limit and redesigned the collapsed panel as a compact single-layer dashboard with focused actions and a dedicated todo status strip.
- Grouped CPU usage, memory usage, CPU temperature, and GPU temperature into a compact unframed central summary. Manual city and automatic location are now mutually exclusive, and the visible city-format hint directs users to enter a city or district rather than only a country name.
- Quoted the executable path stored for the `LocalSystem` hardware service, closing the unquoted-service-path risk in installation, upgrade, and repair flows.
- Added clean-worktree release payload generation and a manual two-stage SignPath workflow that signs first-party EXE and managed-code DLL files before building and signing the installer; every action in the secret-bearing signing workflow is pinned to an immutable commit SHA, and the workflow verifies source revision, versions, signatures, and SHA-256 values without publishing automatically.
- Restricted setup to Windows `10.0.18362` or newer, documented the fresh-install clipboard-history default directly in settings and the privacy policy, bounded backup import size, section counts, and field lengths, and aligned all four compact hardware rows to one label/value typography grid.
- Made the compact dashboard independent of expanded-module ordering and visibility, enabled `PerMonitorV2` DPI awareness, capped transparent windows to the logical Windows work area for `1366×768` at `150%` scaling, and localized QWeather failures without exposing internal Chinese messages in other interface languages.
- The installer now passes its selected language to a fresh first launch; a launch without an installer hint maps the current Windows UI language, while upgrades preserve the saved choice.
- Completed the self-contained distribution notices for .NET／WindowsDesktop Runtime, direct NuGet packages, LibreHardwareMonitor dependencies, QWeather Icons and the PawnIO GPL exception; the full texts are installed under `licenses`.

### 中文说明

分发提示：`v2.1.0` 安装包和一方二进制文件未签名。使用前请核对公开 SHA-256；Windows SmartScreen 或企业策略可能警告或阻止安装。

- 新增默认关闭的「模型雷达」模块，运行时仅访问 ModelDial 固定公开 `latest.json` 接口；展示发布方综合第一、首个官方 `value` 性价比推荐，以及综合／后端／前端／知识 Top 5，并提供明确的缓存、离线、刷新取消和固定署名链接。该模块不调用模型、不进行本地评测，也不修改模型工具配置。
- 全新安装默认启用时间天气、硬件监视、待办事项和快速便签，默认关闭快捷方式、快捷文本和模型雷达；覆盖安装与升级完整保留用户已保存的模块开关和顺序。
- 新增默认勾选且明确披露的完整硬件监控组件：安装固定版本的 PawnIO 驱动和边界收紧的只读 Windows 服务，`UniDesk.exe` 仍以普通用户权限运行。
- 新增独立的管理员权限修复助手，以参数数组安全注册服务，提供稳定退出码、本地日志和版本化 IPC 健康检查；主程序等待修复结束后刷新组件状态，且始终保持普通权限。
- 硬件服务增加有上限的初始化重试、四路并发命名管道接收，以及服务／驱动／协议的细分诊断和本地化修复状态。
- 隔离已在受影响设备上触发硬件服务原生访问冲突的 LibreHardwareMonitor AMD GPU 更新路径；支持的 AMD GPU 继续通过 AMD ADL 读取使用率和温度，并保留 Windows GPU Engine 使用率兜底。无法读取时明确显示不可用，不再让服务崩溃或报告错误的零值。
- 收紧最终稳定性边界：修复助手启动后等待真实终态而不误报取消；快捷方式图标先写临时文件、完整校验后原子发布，并按内容所有权安全清理；备份导入拒绝零次使用的剪贴板记录和空白文本片段分类。
- 未签名正式版门禁会独立核对当前 `HEAD`、包含未跟踪文件的干净工作区、严格布尔清单状态，以及安装包版本资源绑定的载荷清单 SHA-256；调用方不能仅靠伪造 `release-source.json` 获得发布就绪结论。
- 修复全局剪贴板搜索：先解密 DPAPI 内容再匹配；新搜索会取消旧任务，数据库检索不再阻塞 UI 线程。
- 全新安装默认开启开机自启和 Windows 自动定位，覆盖安装与升级保留既有选择；经纬度仅发送到已配置的和风天气 HTTPS API 查询城市，手动城市仍作为兜底，旧版占位值会被忽略，设置页可直接打开 Windows 位置设置。
- 自动定位设置缺失或无效时统一按关闭处理，避免旧数据缺项时隐式请求 Windows 位置权限。
- 展开态和收缩态天气区域均新增可见的和风天气来源链接，并补充公开代码签名政策和双语隐私政策。
- 用户天气 Host 仅允许 HTTPS 的 `*.qweatherapi.com` 专属域名；Host 与 Key 必须成对配置并在保存前验证，备份导出和恢复均排除两者。
- 全部项目和 CI 迁移到 .NET 10 LTS，升级 Microsoft 依赖、钉住已修复的 SQLite 原生包，并增加传递依赖漏洞和版本一致性门禁。
- 增加四语安装披露、签名制品发布就绪检查脚本和零警告 Release 构建。
- 安装器限制为原生 Windows x64，避免 ARM64 模拟环境接受不兼容的 x64 内核驱动；硬件组件失败改为带诊断信息的非致命警告，基础应用仍正常完成安装，并以安装前的原始普通用户令牌自动启动主程序。
- 剪贴板历史最大保存量增加持久单选高亮；收缩面板重做为单层玻璃迷你仪表盘，精简顶部操作，底部状态条专用于下一项待办。
- CPU 使用率、内存使用率、CPU 温度和 GPU 温度集中到紧凑且无独立背景框的中部摘要；手动城市与自动定位双向互斥，并直接显示城市／区县及国外具体城市名的填写提示。
- 安装、覆盖安装和修复流程写入 `LocalSystem` 硬件服务的可执行文件路径时统一保留双引号，消除未加引号服务路径风险。
- 新增干净工作区发布载荷构建和手动两阶段 SignPath 流程：先签全部一方 EXE 和实际承载托管代码的 DLL，再构建并签署安装包；含密钥的签名工作流全部 Action 固定到不可变 commit SHA，流程核验源码提交、版本、签名和 SHA-256，但不会自动公开发布。
- 安装器明确限制为 Windows `10.0.18362` 或更高版本；设置页与隐私政策直接披露全新安装默认开启剪贴板历史；备份导入增加文件大小、分区条数和字段长度上限；收缩态四行硬件指标统一为同一套标签／数值排版。
- 收缩态固定使用专用迷你仪表盘，不受展开态模块排序和启用状态影响；启用 `PerMonitorV2` DPI 感知，透明主窗口和设置窗口会按 Windows 逻辑工作区限制高度，覆盖 `1366×768`、`150%` 缩放；非中文界面不再直接显示和风天气客户端的内部中文错误。
- 安装器把用户选择的界面语言传给全新首次启动；没有安装器提示时按当前 Windows UI 语言映射，覆盖安装继续保留用户已保存语言。
- 补齐自包含分发所需的 .NET／WindowsDesktop Runtime、直接 NuGet 依赖、LibreHardwareMonitor 传递依赖、QWeather Icons 和 PawnIO GPL 例外许可文本，并统一安装到 `licenses` 目录。

### Installer integrity / 安装包校验

- `UniDesk_Setup_2.1.0.exe`
- SHA-256：以 GitHub Release 同页发布的 `SHA256SUMS.txt` 为准
- Authenticode：`NotSigned`；公开发布说明必须披露 SmartScreen／企业策略风险

## v2.0.0

This major release summarizes the net changes since v1.4.2: the Glass 2.0 interface, global search and theme controls, stronger hardware monitoring, safer local data, and a modularized architecture.

### Changes

- Redesigned the main panel and seven-page Settings center with the shared Glass 2.0 visual system.
- Added global search across Quick Notes, Todos, clipboard history, snippets, and shortcuts.
- Added follow-system light/dark mode with separate color-scheme choices for each system appearance.
- Added configurable global hotkeys that can be recorded, restored to default, or fully disabled, with conflict rollback that preserves the previous working shortcut.
- Strengthened Hardware Monitor compatibility with Windows GPU Engine fallback, device-scoped multi-GPU selection, shared LibreHardwareMonitor snapshots, invalid-value filtering, retry backoff, source and availability tooltips, and sanitized diagnostic export.
- Centered the Hardware Monitor RX and TX label-value groups for a more balanced network layout.
- Added privacy-safe v5 backups with semantic validation, import preview, and transactional restore behavior.
- Protected the weather API key and clipboard history with Windows DPAPI, including automatic migration of existing plaintext values.
- Removed the insecure IP-location fallback and retained only secure location providers.
- Improved runtime reliability with background system-metric sampling, single-instance activation, fatal-exception coordination, settings and database persistence safeguards, and seven-day log retention.
- Added confirmation before deleting Todos.
- Renamed the Quick Note editor's primary action from Close to Done.
- Modularized the six dashboard modules and hardware readers into focused services, view models, and WPF controls, backed by expanded automated tests and Windows CI.
- Excluded debug symbol files from the installer.

### 中文说明

- 将主面板和七页设置中心升级为统一的 Glass 2.0 毛玻璃视觉系统。
- 新增全局搜索，可统一检索便签、待办、剪贴板历史、快捷文本和快捷方式。
- 新增跟随 Windows 深浅色模式，并可分别设置浅色与深色主题配色。
- 全局热键支持自定义、恢复默认和完全禁用；发生热键冲突时保留原有可用设置。
- 强化硬件监视兼容性：新增 Windows GPU Engine 使用率兜底，改进 AMD、NVIDIA、Intel 及多显卡环境的数据选择，避免不同物理显卡的数据被错误组合，并增加异常值过滤、失败退避、数据来源提示和脱敏硬件诊断导出。
- 调整硬件监视网络区域布局，使接收 RX 与发送 TX 的标签和数值组分别居中显示。
- 新增隐私安全的 v5 备份、导入语义校验、导入预览和事务化恢复；导入失败时不会留下部分数据。
- 使用 Windows DPAPI 加密天气 API Key 和剪贴板历史，并自动迁移已有明文数据。
- 移除不安全的 IP 定位兜底，仅保留安全定位方式。
- 改进后台硬件采样、单实例启动、致命异常处理、设置与数据库持久化保护和 7 天日志管理。
- 待办删除操作新增确认提示。
- 便签编辑器主操作由「关闭」调整为「完成」。
- 将六个主界面模块和硬件读取器拆分为独立 Service、ViewModel 与 WPF 控件，并补充自动化测试与 Windows CI。
- 安装包不再包含调试符号文件。

### Installer integrity / 安装包校验

- `UniDesk_Setup_2.0.0.exe`
- SHA-256: `8CAC14F98705012FB50DF590B9CE829038BAAEA9D820C5C9042AC3E2C018A202`

## v1.4.2

This patch release improves clipboard copy reliability, hardware monitor compatibility, and panel height settings after v1.4.1.

### Changes

- Reworked Quick Text clipboard writes to use a Win32 clipboard writer with async retry, reducing false copy failures and UI stalls when the system clipboard is temporarily busy.
- Kept Quick Text copy success independent from history refresh and usage-count updates, so post-copy bookkeeping failures no longer report as copy failures.
- Expanded the panel height limit from `920px` to `1040px`.
- Refactored CPU temperature selection into multiple providers: LibreHardwareMonitor CPU sensors, LibreHardwareMonitor motherboard fallback sensors, and a low-priority Windows ACPI Thermal Zone fallback.
- Improved CPU temperature sensor matching for Intel `CPU IA / IA Cores` and AMD `Die` naming, while excluding non-CPU sources such as PCH, VRM, chipset, GPU, and memory temperatures.
- Added release-build hardware diagnostics for CPU temperature source selection, including CPU name, process privilege, sensor candidates, and final selected source.
- Updated application, installer, and README version references to `1.4.2`.

### 中文说明

- 重做快捷文本复制写入逻辑，改用 Win32 剪贴板写入与异步重试，降低系统剪贴板短暂占用时的复制失败和界面卡顿。
- 将快捷文本复制成功与历史刷新、使用次数更新解耦，复制后的附属更新失败不会再误报为复制失败。
- 将面板高度上限从 `920px` 调整为 `1040px`。
- 将 CPU 温度选择改为多来源：LibreHardwareMonitor CPU 传感器、LibreHardwareMonitor 主板兜底传感器、低优先级 Windows ACPI Thermal Zone 兜底。
- 增强 Intel `CPU IA / IA Cores` 和 AMD `Die` 等温度传感器命名匹配，并排除 PCH、VRM、芯片组、GPU、内存等非 CPU 温度来源。
- 新增 Release 构建下的 CPU 温度诊断日志，记录 CPU 名称、进程权限、候选传感器和最终选择来源。
- 将应用、安装包和 README 版本引用更新为 `1.4.2`。

## v1.4.1

This release adds multilingual UI support, update checking, and hardware monitor compatibility improvements after v1.3.7.

### Changes

- Added UI language switching for Simplified Chinese, English, Japanese, and Spanish, using direct language option buttons in Settings.
- Added GitHub release update checking with a browser redirect fallback when the GitHub API is unavailable or rate-limited.
- Improved CPU temperature fallback detection for AMD Ryzen systems, including Ryzen 9000 series sensor naming.
- Improved memory and GPU metric fallback handling when the primary hardware sensor source is unavailable.
- Updated application, installer, and README version references to `1.4.1`.

### 中文说明

- 新增界面语言切换，支持简体中文、English、日本語、Español，并在设置页改为直接点击语言选项。
- 新增 GitHub Release 检查更新，在 GitHub API 不可用或触发限流时使用网页跳转兜底。
- 改进 AMD Ryzen 平台 CPU 温度兜底识别，覆盖 Ryzen 9000 系列传感器命名。
- 改进内存和 GPU 指标兜底逻辑，降低主传感器源不可用时数据显示失败的概率。
- 将应用、安装包和 README 版本引用更新为 `1.4.1`。

## v1.3.7

This patch release fixes the remaining shortcut ordering interaction issues found after v1.3.6.

### Changes

- Renamed shortcut context menu actions from "Move up" and "Move down" to "Move forward" and "Move backward" to match the grid ordering behavior.
- Reworked shortcut edit-mode drag sorting to use direct pointer hit testing instead of WPF native drag-and-drop events, so icon reordering responds reliably.
- Fixed shortcut item hit testing so the edit-mode drag handlers receive mouse input from the whole shortcut tile.
- Updated application, installer, and README version references to `1.3.7`.

### 中文说明

- 将快捷方式右键菜单中的「上移」「下移」调整为「前移」「后移」，更符合三列网格中的实际排序行为。
- 重做快捷方式编辑模式拖拽排序，改为直接鼠标命中检测，不再依赖 WPF 原生拖放事件，提升拖动排序可靠性。
- 修复快捷方式格子的鼠标命中区域，让编辑模式拖拽处理器能接收到整个快捷方式格子的鼠标输入。
- 将应用、安装包和 README 版本引用更新为 `1.3.7`。

## v1.3.6

This patch release fixes shortcut ordering regressions found after v1.3.5.

### Changes

- Fixed the shortcut context menu ordering actions so right-click "Move up", "Move down", "Move to first", and "Move to last" work outside edit mode.
- Fixed shortcut edit mode dragging so icon reordering is not intercepted by the shortcut module scroll panning behavior.
- Updated application, installer, and README version references to `1.3.6`.

### 中文说明

- 修复快捷方式右键菜单排序操作，正常模式下「上移」「下移」「移到最前」「移到最后」可以生效。
- 修复快捷方式编辑模式拖拽排序，避免图标拖动被快捷方式模块滚动拖拽逻辑拦截。
- 将应用、安装包和 README 版本引用更新为 `1.3.6`。

## v1.3.5

This patch release improves hardware monitor compatibility across CPU, memory, GPU, and network metrics.

### Changes

- Improved CPU temperature reading with LibreHardwareMonitor fallback for Intel and AMD processors.
- Added Intel-friendly CPU temperature sensor selection for CPU Package, Package, Core Max, Core Average, CPU Core, and Core #n sensors.
- Added CPU usage fallback selection from LibreHardwareMonitor load sensors when Windows performance counters are unavailable.
- Improved GPU usage and temperature selection for NVIDIA, AMD, and Intel GPUs.
- Added safer filtering for invalid sensor values such as null, NaN, Infinity, invalid percentages, and abnormal temperatures.
- Improved memory metric validation and Debug-only diagnostic logs for hardware sensors.
- Updated application, installer, and README version references to `1.3.5`.

### 中文说明

- 改进 CPU 温度读取，增加 LibreHardwareMonitor 兜底，兼容 Intel 和 AMD 处理器。
- 增强 Intel CPU 温度传感器匹配，支持 CPU Package、Package、Core Max、Core Average、CPU Core 和 Core #n。
- 当 Windows 性能计数器不可用时，增加 CPU 使用率传感器兜底选择。
- 改进 NVIDIA、AMD、Intel GPU 的使用率和温度选择策略。
- 统一过滤 null、NaN、Infinity、异常百分比和异常温度。
- 改进内存指标校验，并增加 Debug 模式硬件传感器诊断日志。
- 将应用、安装包和 README 版本引用更新为 `1.3.5`。

## v1.3.4

This patch release focuses on visual polish for the main panel.

### Changes

- Further reduced the empty bottom area in the Hardware Monitor module.
- Kept hardware monitor content auto-sized while relying on the main panel scroll area when the overall panel is short.
- Updated application, installer, and README version references to `1.3.4`.

### 中文说明

- 进一步减少「硬件监视」模块底部留白。
- 保持硬件监视内容自适应高度，整体面板高度不足时继续由主面板滚动承接。
- 将应用、安装包和 README 版本引用更新为 `1.3.4`。

## v1.3.3

This release prepares the current main branch for a new public installer after the v1.3.2 release.

### Changes

- Expanded data backup and restore to include settings, module configuration, and shortcuts.
- Preserved compatibility with older todo-only backup files.
- Removed optional `secrets.json` packaging from the project and installer output.
- Improved settings persistence during app exit.
- Improved cleanup of network resources used by location lookup.
- Polished the hardware monitor layout by removing unnecessary empty space.
- Polished shortcut item alignment so icons and labels stay visually centered.
- Updated application, installer, and README version references to `1.3.3`.

### 中文说明

- 扩展数据备份与还原范围，新增设置、模块配置和快捷方式。
- 保持对旧版仅待办备份文件的兼容。
- 移除项目和安装包中可选打包 `secrets.json` 的规则。
- 改进应用退出时的设置保存可靠性。
- 改进定位服务使用的网络资源释放。
- 优化硬件监视模块布局，减少不必要留白。
- 优化快捷方式图标和名称对齐，让列表视觉更整齐。
- 将应用、安装包和 README 版本引用更新为 `1.3.3`。

## v1.3.2

This release updates UniDesk into a more complete desktop sidebar tool.

### Changes

- Added module management with show/hide controls and module ordering.
- Added shortcut drag-to-add support and shortcut ordering.
- Added Quick Notes with multiple notes, auto save, pinning, copy, delete, backup, and restore support.
- Added Quick Text with clipboard history, text snippets, one-click copy, sensitive content filtering, backup, and restore support.
- Improved the main panel layout so hardware monitoring and network speed remain readable as modules grow.
- Improved GPU temperature reading with AMD ADL, NVIDIA NVML, and LibreHardwareMonitor fallback support.
- Improved personalization settings including panel height, font size, and custom display title.
- Updated README screenshots and project description for the current public release.

### 中文说明

- 新增模块管理，支持模块显示 / 隐藏和排序。
- 新增快捷方式拖拽添加和快捷方式排序。
- 新增快速便签，支持多条便签、自动保存、置顶、复制、删除、备份和还原。
- 新增快捷文本，支持剪贴板历史、常用短语、一键复制、敏感内容过滤、备份和还原。
- 优化主面板布局，模块增多时硬件监视和网速仍能完整显示。
- 优化 GPU 温度读取，支持 AMD ADL、NVIDIA NVML 和 LibreHardwareMonitor 兜底读取。
- 优化个性化设置，支持面板高度、字体大小和自定义显示标题。
- 更新 README 宣传图和项目介绍，使其匹配当前正式版本。

## v1.1.1

This release focuses on presentation and polish for the public GitHub release.

### Changes

- Updated the README main screenshot to show the integrated network speed monitor.
- Fixed the left-side module title icons for Hardware Monitor, Quick Shortcuts, and Todo List.
- The module title icons now follow the current theme text color instead of reverting to the old blue image assets.
- Removed unused legacy blue module icon assets from the package.
- Updated installer and documentation references to `UniDesk_Setup_1.1.1.exe`.

### 中文说明

- 更新 GitHub 首页主界面截图，现在能看到实时网速监测。
- 修复「硬件监视」「快捷方式」「待办事项」左侧小图标颜色。
- 三个模块图标现在会跟随当前主题文字颜色，不再固定为旧的蓝色图标。
- 移除了不再使用的旧蓝色图标资源。
- 安装包和文档版本更新为 `1.1.1`。

## v1.1.0

### Highlights

- Renamed the desktop widget project to UniDesk.
- Integrated the hardware monitor into the main panel.
- Added CPU, memory, GPU, temperature, and network speed display.
- Kept the hardware monitor under the same theme, transparency, and panel width settings as other modules.
- Preserved local user data by migrating compatible legacy data into the UniDesk data directory.
