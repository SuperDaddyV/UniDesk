# UniDesk Release Notes

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
