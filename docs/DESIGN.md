# UniDesk 设计文档

**版本**: 2.1.0
**最后更新**: 2026年8月31日
**项目**: UniDesk - Windows 桌面侧边助手应用

---

## 目录

1. [系统概述](#系统概述)
2. [质量目标](#质量目标)
3. [架构设计](#架构设计)
4. [模块设计](#模块设计)
5. [数据模型](#数据模型)
6. [数据库设计](#数据库设计)
7. [UI/UX 设计](#uiux-设计)
8. [技术栈](#技术栈)
9. [项目结构](#项目结构)
10. [关键特性实现](#关键特性实现)
11. [开发计划](#开发计划)

---

## 系统概述

### 应用定位
UniDesk 是运行于 Windows 11 的桌面侧边助手应用，以悬浮右侧面板形式呈现，集成时钟天气、硬件监视、快捷方式、待办事项、快速便签、快捷文本和模型雷达等核心功能，帮助用户在不中断主要工作流的情况下快速访问信息和启动应用。模型雷达是默认关闭的只读决策参考，不执行模型调用或修改模型工具配置。

### 核心目标
- **顺滑稳定**：耗时任务不阻塞 UI 线程，交互与动画顺滑，未处理 UI 异常会提示并安全终止，避免在未知状态下继续运行
- **美观现代**：遵循 Fluent Design（Windows 11 风格），统一圆角、阴影、间距与字体
- **数据私密**：所有数据本地存储于 SQLite，无云同步
- **易用友好**：托盘、热键、开机自启、主题跟随系统等桌面助手体验特性齐全

### 支持平台
- **正式支持平台**：仍在 Microsoft 支持周期内的 Windows 11 x64，以及 Windows 10 Enterprise／IoT Enterprise LTSC 2021 或更新版本 x64；LTSC 2019 虽仍处于 Microsoft 与 .NET 支持周期，但低于本项目 10.0.18362.0 API 基线，2.1.0 不得笼统宣称支持全部 LTSC
- **兼容基线**：Windows API 目标版本仍为 10.0.18362.0；已停止支持的普通 Windows 10 版本只提供尽力兼容，不作为正式发布承诺
- **目标框架**：.NET 10 LTS
- **UI 框架**：WPF + 自定义样式资源

---

## 质量目标

### 性能与交互
- 所有网络请求、数据库 IO、文件 IO 等耗时任务使用异步执行，不阻塞 UI 线程
- 支持取消正在进行的后台任务（例如天气刷新、数据导入导出），用户取消操作在 200ms 内响应
- 对高频 UI 更新进行节流/合批更新，避免频繁触发布局重排
- 硬件采集、数据库批处理等同步工作不得在 WPF Dispatcher 线程执行
- 列表类 UI（待办列表、快捷启动网格）采用虚拟化或分页策略，数据量增大时保持流畅

### 可靠性
- 捕获可恢复异常并以用户可理解的方式提示；未处理 UI 异常记录并只提示一次，随后安全终止进程
- 通过全局热键呼出 MainWindow 时，窗口在 500ms 内变为可交互；隐藏 MainWindow 在 300ms 内完成

---

## 架构设计

### 整体架构

```
┌─────────────────────────────────────┐
│          View Layer (XAML)          │
│  MainWindow | SettingsWindow | ...  │
└────────────────────┬────────────────┘
                     │
┌────────────────────▼────────────────┐
│      ViewModel Layer (MVVM)         │
│  绑定数据、处理用户交互             │
└────────────────────┬────────────────┘
                     │
┌────────────────────▼────────────────┐
│      Service Layer (业务逻辑)       │
│  ClockWeather/Todo/Shortcut        │
│  Tray/Hotkey/Window/Layout/Startup  │
└────────────────────┬────────────────┘
                     │
┌────────────────────▼────────────────┐
│     Data Access Layer (DAL)         │
│  DatabaseService, SettingsService   │
└────────────────────┬────────────────┘
                     │
┌────────────────────▼────────────────┐
│   Infrastructure (外部资源/API)     │
│  SQLite | 和风天气 API | Registry   │
│  Win32 API (P/Invoke) | NotifyIcon  │
└─────────────────────────────────────┘
```

### 设计模式

| 模式 | 应用场景 | 实现工具 |
|------|--------|--------|
| **MVVM** | 分离 UI 和业务逻辑 | CommunityToolkit.Mvvm |
| **Repository** | 数据访问抽象 | DatabaseService |
| **Dependency Injection** | 管理对象生命周期 | 内置 DI 容器 |
| **Observer** | 数据变化通知 | INotifyPropertyChanged |
| **Singleton** | Service 实例唯一性 | DI 容器配置 |
| **Factory** | 创建复杂对象 | ShortcutIconFactory |

---

### 仪表盘模块边界

- `MainWindowViewModel` 仅负责窗口状态、模块布局、设置协调、子模块组合，以及供 Settings 恢复流程调用的兼容委托；不直接承载模块 CRUD。
- 七个可见模块分别由 `TimeWeatherViewModel`、`HardwareMonitorViewModel`、`ShortcutsViewModel`、`TodosViewModel`、`QuickNotesViewModel`、`QuickTextViewModel` 和 `ModelRadarViewModel` 管理状态与命令；其中稳定模块 ID 为 `ModelRadar`。
- 七个 WPF 视图分别位于 `Controls/*ModuleView.xaml`；主窗口只组合控件并按 `DashboardModuleIds` 管理显示、顺序和高度。全新安装默认启用时间天气、硬件监视、待办事项和快速便签，默认关闭快捷方式、快捷文本和模型雷达；升级用户保留已保存的模块开关与顺序。缺失 `ModelRadar` 的旧布局只在当前模块列表末尾追加关闭项，不改变既有模块顺序。用户仍可在现有模块管理中启用、关闭和自由排序。
- 快捷方式鼠标排序、文件拖放和添加弹层状态归 `ShortcutsModuleView`／`ShortcutsViewModel`；主窗口 code-behind 不处理模块内部输入。
- 待办完成圆圈由 `TodosModuleView` 的显式鼠标处理器调用 `TodosViewModel.ToggleTodoCommand`，不使用脱离可视树后无法解析祖先绑定的 `MouseBinding`。
- 便签编辑器的主操作文案为「完成」；窗口关闭必须先完成保存清理，保存失败时保持编辑器打开，成功后再调度到 Dispatcher 下一轮执行真正关闭，不得在 `OnClosing` 调用栈内递归 `Close()`。
- `QuickNoteService` 更新和删除返回实际成功状态；编辑器与列表只有在数据库确实受影响后才关闭或刷新。
- 所有业务 Service 的读取失败必须记录并向上层传播，不得以空集合或 `null` 伪装成“当前没有数据”；创建、更新、删除和清空操作必须返回实际受影响结果或抛出异常，ViewModel 只有在持久化确实成功后才能关闭窗口、刷新列表或显示成功提示。
- 收缩态始终由专用的 `TimeWeatherModuleView` 迷你仪表盘承载，不受展开态模块启用状态和用户排序影响；调整模块顺序后仍须显示时间、天气、硬件摘要与下一待办的固定组合。
- 子模块自行持有事件、计时器和取消源，并在 `Dispose()` 中解除订阅；主壳统一触发子模块清理。

### 系统指标读取边界

- `SystemMetricsService` 只组合 CPU、GPU、内存和网络四类读取结果，不直接包含原生 API 或传感器选择实现。
- `SensorSelection` 是无状态选择策略；CPU、GPU、内存和网络读取器位于 `Services/SystemMetrics/`，各自拥有原生资源与释放责任。
- `SystemMetricsMonitor` 继续负责后台串行采样、禁用暂停、迟到结果抑制和向硬件监控 ViewModel 发布快照。
- UniDesk 主程序始终以普通用户权限运行；需要低层寄存器访问的 LibreHardwareMonitor 读取由可选的 `UniDesk.HardwareService` Windows 服务承担。
- 主程序与硬件服务只通过版本化的本机命名管道协议交换脱敏传感器快照。服务不得接收文件路径、进程路径、脚本、任意命令或硬件写入请求。
- 命名管道必须把超长帧、非法 UTF-8、未知命令和其它请求级协议错误隔离在当前连接内；记录或拒绝该请求后，同一接收循环必须继续服务。只有宿主取消或不可恢复的管道基础设施错误才能结束接收循环，普通本机客户端输入不得耗尽固定接收能力。
- 硬件服务不可用、协议不兼容或响应超时时，主程序必须继续使用 Windows Performance Counter、NVML、ADL、Windows GPU Engine、WMI 等普通权限来源，不得阻塞界面或导致应用退出。
- 硬件服务不得调用 LibreHardwareMonitor 的 AMD GPU `Update()`：该上游原生路径已在受影响设备上触发无法由进程内异常处理可靠隔离的 `AccessViolationException`。服务仍须更新 CPU、主板、NVIDIA GPU 和 Intel GPU，并在诊断硬件列表中明确标记已隔离的 AMD GPU；主程序继续优先使用 AMD ADL 读取 AMD GPU 使用率和温度，并以 Windows GPU Engine 作为使用率兼容来源。ADL 不可用时允许温度明确显示为不可用，但不得伪造 `0℃` 或让硬件服务崩溃。
- 安装器只允许在 Windows x64 上运行，不得因 x64 应用模拟而接受 Windows ARM64；在具备原生 ARM64 驱动和服务包前，不承诺 ARM64 支持。安装器必须通过 `MinVersion=10.0.18362` 在复制文件前拒绝低于 Windows 10 1903 API 兼容基线的系统。
- 桌面快捷方式和完整硬件监控组件均由安装器默认勾选；覆盖安装不得继承上次取消状态而导致两项静默变为未选。附加任务本身必须用一行清晰文案告知完整硬件监控会安装 PawnIO 驱动和以 `LocalSystem` 运行的只读系统服务，不再额外插入大段权限说明页；完整安全说明保留在 README 与安装许可目录。用户主动取消或可选组件安装失败后，天气、便签、快捷方式等非硬件功能仍须完整可用。
- 用户选择完整组件后，安装器调用随包发布的 `UniDesk.HardwareRepair` 管理员维护工具，统一完成 PawnIO 校验与安装、服务注册、服务启动和 IPC 健康检查；维护工具必须幂等并返回可诊断的分步退出码，安装器不得自行拼接 `sc.exe` 引号或把部分安装报告为成功。维护工具调用 Windows PowerShell 系统模块时必须把模块搜索路径隔离到对应 Windows PowerShell 自带目录，不能继承 PowerShell 7、开发工具或用户配置注入的 `PSModulePath`；相关验证必须覆盖污染环境下的真实子进程调用。
- 安装器自行请求管理员权限；标准用户正常双击后由 Windows 请求管理员凭据，不要求也不引导用户右键选择「以管理员身份运行」。安装完成页默认勾选启动 UniDesk，并必须使用 Inno Setup 的 `runasoriginaluser` 以安装前的原始普通用户令牌启动；主程序清单始终保持 `asInvoker`。如果用户显式以已提权进程启动安装器而导致原始令牌不可用，安装器不得通过修改主程序清单或永久提权来补偿。应用内修复只能从系统 `Common Program Files\UniDesk\HardwareRepair` 的固定受保护路径启动带 `requireAdministrator` 清单的维护工具，不得从用户可选的主程序目录、环境变量、配置或命令行解析可提权可执行文件，也不得复制或依赖完整安装包。
- 完整硬件组件属于可选增强项。维护工具必须区分“底层驱动／传感器不可用但普通权限兼容来源可继续工作”和真正的安装、安全或服务故障：前者返回稳定的兼容模式退出码，安装器不得弹出错误对话框，而应在完成页说明程序将继续使用 Windows／厂商来源且部分主板传感器可能缺失；后者仍须记录稳定退出码并显示可理解的非致命警告。两类情况均不得抛出 Runtime error、回滚或把基础应用报告为安装失败；用户之后可在设置中导出诊断并再次修复，且界面不得声称 PawnIO 或完整低层监控已成功安装。
- 维护工具只能执行固定的 `install-or-repair`、`remove-service`、`cleanup-startup` 和 `health-check` 操作，不接收任意路径、命令或脚本；它使用参数数组调用系统工具，并将 UniDesk 专属服务配置为 `Automatic`、`LocalSystem` 和限定恢复策略。迁移清理由同一个固定 `cleanup-startup` 命令读取受保护组件目录内、由提权安装器写入且受 ACL 保护的当前／旧主程序路径标记，不接受命令行旧路径。传给 `sc.exe create/config` 的 `binPath=` 值必须在服务注册值中保留包围可执行文件路径的双引号，即使安装目录包含空格也不得产生未加引号的 `LocalSystem` 服务路径。PawnIO 不存在时必须先校验安装包固定哈希和 Authenticode 签名再执行首次安装；PawnIO 服务已登记但启动后的健康检查仍明确返回驱动不可用时，维护工具只允许在同样校验安装包后执行一次有上限的修复安装并重新健康检查，不能无限重装或在安装包验证失败时触碰共享驱动，仍不可用则进入明确的兼容模式。
- 安装、覆盖安装和卸载在停止、重配或删除 `UniDeskHardwareService` 前，必须读取服务注册的 `ImagePath` 并规范化为可执行文件路径；新版只在它严格等于系统 `{commoncf}\UniDesk\HardwareService\UniDesk.HardwareService.exe` 时视为当前组件拥有。Inno Setup 的 `{commonpf}` 表示共享 `Program Files` 根而不是 `Common Files`，系统组件路径必须使用会展开为 `C:\Program Files\Common Files` 的 `{commoncf}`，并与 .NET `Environment.SpecialFolder.CommonProgramFiles` 保持一致。升级兼容只额外接受同一 AppId 已注册旧主程序目录下的历史 `HardwareService\UniDesk.HardwareService.exe`，且只能用于先禁用、停止并删除旧服务，不能继续从旧目录运行或重配。不存在时安装流程可以创建、卸载流程视为已清理；读取失败或同名服务指向其它路径时必须返回稳定的所有权冲突退出码，且安装器不得使用 `sc.exe` 兜底接管或删除。卸载已拥有服务时必须先禁用服务，再请求停止、以服务名过滤终止残留服务进程并轮询确认服务确已停止或不存在，最后才请求删除；只有确认进程停止且删除已接受／服务已不存在时才能报告成功。
- 覆盖安装必须在停止已有服务前完成安装目标校验和 ACL 收紧；停止请求返回成功只代表请求已接受，安装器还必须以服务名过滤终止残留服务进程并轮询确认服务确已停止，不能用固定休眠代替状态确认。停止后若文件复制、维护工具或安装流程失败，安装器退出前必须尽力重新启动此前由本次安装停止的已拥有服务，避免一次失败升级永久关闭旧版监控。
- 主程序目录 `{app}` 只承载普通权限应用、图标和许可文件，安装位置页必须允许用户选择本地固定磁盘上支持持久 ACL 的非宽泛安全目录，并默认继承同一 AppId 的上次位置以支持直接覆盖；目录页校验必须读取 `WizardDirValue` 的当前编辑框内容，复制前再校验最终 `{app}`，不得因页面值尚未提交而检查其它路径。必须在目录页和复制前同时拒绝 UNC／映射网络盘、可移动介质、无法识别的卷、不支持持久 ACL 的 FAT／exFAT 等文件系统、驱动器根、Windows、Program Files 根、ProgramData 根或用户配置根，并显示本地化的简短原因。新位置只能是尚不存在或为空的目录；非空目录只有严格等于同一 AppId 已登记位置时才能覆盖，不能仅凭存在同名 `UniDesk.exe` 推断目录归属。所选目录及其已经存在的祖先路径不得是重解析点，已存在目录内也不得包含重解析点。安装器在提升权限后复制或删除 `{app}` 内容期间，必须用不含 `FILE_SHARE_DELETE` 的目录句柄锁定从盘符根到 `{app}` 的现有路径链，并在取得锁后再次检查重解析点；安装时先把 `{app}` 及现有子项收紧为普通用户只读执行，复核无重解析点后才能复制，卸载时无法取得同样锁或发现重解析点必须停止。这样允许普通 D／E 盘 NTFS／ReFS 目录，但不能利用父目录替换或 junction 让管理员安装／卸载写删其它位置。所有可提权或以系统身份执行的载荷——硬件服务、修复工具、PawnIO 安装包和 Inno 卸载器——必须固定放在系统 `{commoncf}\UniDesk` 受保护组件目录，服务与卸载器不得从 `{app}` 加载 DLL、配置、脚本或其它代码。用户取消完整硬件监控时可以不注册服务，但固定修复工具仍随包保留以支持以后修复；安装位置页必须简短说明该离线修复载荷仍会占用约 220 MB 系统盘空间。
- 安装器对目录存在性、空目录、重解析点链和 ACL 子项的枚举必须 fail-closed：只有“成功枚举且结果安全”才能继续。无法枚举、无法读取属性、无法区分空目录与访问失败、或无法完成子项 ACL 重置时，必须返回稳定失败并在复制、递归 ACL 修改或删除前停止；不得把 `FindFirst`／属性读取失败当作“目录为空”“无重解析点”或“ACL 已加固”。驱动器根是已经由 `GetDriveType` 和 `GetVolumeInformation` 验证的路径链边界：路径存在性检查必须先识别真实存在的目录，祖先重解析检查必须在盘符根调用 `FindFirst` 前终止，不能把 Win32 对 `D:\` 这类根路径返回的 `ERROR_FILE_NOT_FOUND` 误判为安全目录不可访问；盘符根本身仍不得作为 `{app}`。
- 安装器必须在复制受保护组件、创建管理员卸载器或执行维护工具之前，对精确 `{commoncf}` 及其直属系统 Program Files 父目录执行 fail-closed ACL 预检：这两级目录不得是重解析点，所有者只能是 `SYSTEM`／`Administrators`／`TrustedInstaller`，且普通用户不得拥有删除子项、删除目录、改 ACL 或取得所有权的有效权限；检查到 Program Files 即停止，不得继续扫描卷根，也不得信任调用方可预置的通用环境变量。递归 ACL 收紧只能作用于精确 `{commoncf}\UniDesk` 组件目录及其固定子目录，绝不能作用于用户选择的 `{app}`。维护工具在注册服务前必须复核同一 Common Program Files／Program Files 边界以及服务载荷自身的 ACL。
- 同一 `AppId` 的旧版可位于任意合规主程序目录；新版不得执行旧目录中的 `uninsNNN.exe`。为把 Inno 卸载器迁移到固定受保护目录，安装器只能从固定 HKLM 同 AppId 的 `InstallLocation`／`UninstallString` 读取旧位置，规范化后要求卸载器严格位于该旧目录且文件名精确匹配 `unins` 加三位数字的 `.exe`。复制前只能锁定、检查并收紧旧目录，绝不能删除旧卸载器；所有可能中止安装的目录、ACL、服务所有权和停止条件必须先通过，且 Inno 已完成文件复制并在固定受保护目录创建新版卸载器后，才允许重新锁定并复核旧目录，逐个删除再次匹配的旧 `uninsNNN.exe` 及同编号 `.dat`／`.msg`。不得使用宽泛删除、不得执行旧卸载器、不得删除其它程序文件或用户数据；安装成功后的旧卸载器清理失败只能记录带稳定日志路径的迁移警告并保留可工作的新版卸载入口，不能把已经成功的新安装伪装为失败或再次破坏卸载注册。若严格 owned 的旧硬件服务仍位于已注册旧主程序目录或当前 `{app}` 下，安装器必须先禁用、停止并删除，再从固定组件目录注册新版服务；ImagePath 不匹配这些已知路径时视为外部同名服务，绝不接管。用户选择的新 `{app}` 与旧注册目录不同时，旧路径还必须写入受保护组件目录的固定迁移标记，由维护工具清理严格指向旧目录的启动项；旧程序目录不得整体自动删除，用户数据不受影响。当前主程序路径也必须由安装器写入受保护组件目录的固定标记，卸载清理只能信任该标记或同 AppId 的受保护卸载注册记录，不接受命令行通用路径。
- 卸载时维护工具必须执行受限的启动项清理：只枚举当前已加载的 `HKEY_USERS` 用户配置单元，仅删除 `UniDesk`／`LumiDesk`／`VsirDesk` Run 值中严格指向当前安装目录且文件名匹配的命令；同名计划任务也必须先读取 action 并按同一规则验证后才可删除。若标准用户安装时使用了另一管理员凭据，未加载的原用户配置单元无法由 Inno 卸载阶段可靠访问，工具必须在日志中明确清理范围而不得宣称已清理所有用户配置。
- 安装器必须默认启用安装日志；准备阶段或安装后自定义步骤失败时，面向用户的简短提示必须包含实际日志文件路径，不能只让普通用户猜测 Common Program Files 或临时目录。卸载初始化不得为验证而重新创建已不存在的 `{app}`；目录不存在时跳过主程序目录锁，存在时仍须完成安全校验和锁定。卸载完成后先释放目录锁，再仅删除确认为空且非重解析点的主程序目录，不得递归删除未知残留。
- 在复制或执行管理员维护载荷、注册或启动服务之前，安装器必须将 `{commoncf}\UniDesk` 及硬件服务、修复工具、PawnIO、卸载器子目录收紧为仅 `SYSTEM`／`Administrators` 可修改、普通用户只读执行，并移除弱继承写权限。目标组件目录本身或已有子项包含重解析点时必须拒绝递归，避免 junction／符号链接扩大 ACL 修改范围。用户选择的 `{app}` 只有在安全目录校验和整条路径句柄锁定完成后才能单独收紧，不能与受保护组件共用未经限定的递归目标。维护工具在注册服务前必须再次验证服务 EXE、承载业务代码的 DLL、配置与组件父目录均不可被普通用户写入；普通用户的 `Read`／`ReadAndExecute`／`Synchronize` 权限必须视为安全，只有实际写入、创建、追加、删除、修改属性或 ACL、取得所有权等基础权限位才视为可修改。权限判定不得把 `FullControl`／`Modify`／`Write` 这类包含读取位的复合枚举直接合并成危险位掩码，否则会把只读 ACL 误判为可写。校验失败时拒绝注册并返回稳定退出码。
- 任何通过计划任务或 Registry Run 值名称清理旧启动项的操作，必须先读取并验证启动命令指向当前安装目录中明确受支持的 UniDesk／LumiDesk／VsirDesk 可执行文件；不得只凭通用名称覆盖或删除任务与注册表值。
- 硬件服务初始化失败必须按有上限的退避策略重试；命名管道以固定数量并行接收循环处理连接，单个超时客户端不得独占全部服务能力。
- 硬件服务快照超过明确新鲜度上限时必须返回不可用或陈旧状态；主程序不得把服务端旧 `CapturedAtUtc` 重写为当前时间后继续显示为最新读数。
- PawnIO 视为共享系统依赖。卸载 UniDesk 时必须停止并删除 UniDesk 硬件服务；维护工具失败时仅允许使用固定的 `sc.exe stop/delete UniDeskHardwareService` 兜底并向用户报告仍未删除的状态。默认保留 PawnIO，避免破坏其它硬件监控软件。

### 持续集成

- `.github/workflows/ci.yml` 在 `main` 推送和 Pull Request 上使用 `windows-latest`、.NET `10.0.x` 执行 restore、传递依赖漏洞审计、Release build 和 Release test。普通 CI 不执行发布、制品上传、部署或密钥读取。
- CI 与发布构建使用仓库 `global.json` 固定受支持的 .NET 10 SDK feature band，并通过提交的 `packages.lock.json` 以 locked mode 还原；普通 CI 的官方 Action 也固定到已核验的完整 commit SHA。发布源清单记录实际 SDK 版本和依赖锁哈希，避免同一提交随时间解析出不同构建输入。
- Release 构建必须从无未提交改动的目标提交开始，通过统一脚本将主程序、硬件服务和修复工具发布到同一版本的全新制品根目录，再使用这些明确目录编译安装包；不得依赖历史 `publish` 目录、临时 Inno Setup 宏覆盖或人工复制来决定安装包内容。
- 可信签名使用 SignPath Foundation 公共信任签名作为首选方案。签名流程只能由受控的手动 GitHub Actions 工作流触发，连接令牌只保存在 GitHub Secrets，组织、项目和策略标识保存在 GitHub Variables；仓库不得保存证书私钥、令牌或身份材料。
- 读取 SignPath 令牌或处理待签名制品的工作流中，所有第三方 Action 必须固定到其官方仓库的完整 40 位 commit SHA，并在行尾保留对应版本标签注释；不得使用可移动的主版本标签直接执行。更新 SHA 时必须重新核对官方仓库 tag 指向并通过发布流水线回归测试。
- `workflow_dispatch` 等外部字符串输入不得直接插值到 `run:` 脚本正文；版本号必须先通过环境变量传入，并在任何 restore、build、publish 或签名步骤前按严格的三段数字语义版本格式校验。签名工作流必须有静态回归测试阻止脚本正文重新出现 `${{ inputs.* }}`。
- 签名前清单的 SHA-256 必须在提交外部签名请求前固化为 GitHub Actions 步骤输出，后续安装器构建与最终门禁同时以该不可由签名返回制品改写的值校验清单。外部载荷签名 Action 返回后，工作流必须再次用固定完整 commit SHA 的官方 checkout Action 清理工作区并恢复 `github.sha`；公开安装器构建脚本还必须独立确认仓库 `HEAD` 等于预期源提交且 worktree 无任何 tracked／untracked 改动，防止外部 Action 修改 Inno 脚本、安装资源或构建逻辑后进入第二次签名。
- 两次外部 SignPath Action 不得与其后续信任阶段复用同一 Runner：未签载荷构建、载荷签名、干净源码上的安装器构建、安装器签名、最终发布就绪验证分别使用独立 GitHub-hosted job，并只通过不可变 artifact id、前置 job output 和重新下载的制品传递状态。安装器构建 job 必须在提交第二次签名前固化未签名安装包的 Authenticode 规范化内容 SHA-256；最终独立验证 job 除检查有效签名、预期签名者和版本外，还必须确认签名安装包排除 Checksum、Security Directory 与终端证书表后的内容哈希与该前置值一致。
- 配置 SignPath 令牌前，默认分支必须启用禁止强推和删除的保护规则并要求 CI，通过仅允许 `main` 的专用 GitHub Environment 限制签名 Secret；仓库必须启用私密漏洞报告并提供 `SECURITY.md`。
- 仓库首页和发布说明必须公开链接 `CODE_SIGNING_POLICY.md` 与 `PRIVACY.md`，并保留 SignPath Foundation 要求的资助声明；代码签名政策必须列出作者／审查者／批准者、签名范围和人工批准边界，隐私政策必须如实列出天气、定位、更新检查和本地存储的数据流。
- 签名顺序固定为：先签主程序、硬件服务、修复工具及其承载一方托管代码的 DLL，再用已签名文件编译安装包，最后签安装包。不得只签 `.exe` 应用宿主而遗漏实际承载业务代码的 `.dll`。签名发布必须继续通过版本一致性、依赖漏洞、源提交一致性、发布清单哈希和全部一方 PE 文件的 Authenticode 门禁。
- `v2.1.0` 允许一次由项目所有者明确批准的未签名正式发布例外，以延续当前未签名稳定版的信任基线；该例外不构成签名通过，也不适用于后续版本。未签名正式包必须从最终公开 `main` 的精确干净提交重新构建，通过锁定还原、零警告 Release 构建、全量测试、依赖漏洞、版本一致性、完整载荷清单和 SHA-256 门禁；`Test-UnsignedReleaseReadiness.ps1` 必须验证安装包与全部 UniDesk 一方 PE 均为 `NotSigned`、源清单与当前提交一致、载荷无 PDB，并生成可公开核对的校验清单。门禁不得只信任调用方提供的清单状态：`isDirty` 必须存在且严格为布尔 `false`，门禁必须重新读取当前 `HEAD` 和包含未跟踪文件的工作区状态，并核对安装包版本资源中绑定的源清单 SHA-256；项目／锁文件枚举排除 `artifacts`、`bin`、`obj` 和 `publish` 时必须同时识别 Windows 反斜杠与规范化斜杠，忽略目录中的诊断工程不得改变正式门禁清单。只有当前源码、源清单、递归载荷哈希和安装包载荷指纹一致时才允许生成公开清单。README、发布说明和 GitHub Release 必须醒目标注 `Authenticode: NotSigned` 及 SmartScreen／企业策略风险，绝不得把未签名制品描述为已签名或通过 SignPath。未经用户最终确认仍不得创建 Git tag 或 GitHub Release。
- 签名前的发布源清单必须记录 `App`、`HardwareService` 和 `HardwareRepair` 三个安装载荷目录中每个目录的规范化相对路径，以及每个文件的规范化相对路径与 SHA-256，并明确唯一允许被 SignPath 改写的签名目标。三个载荷树中的任意文件或目录均不得是重解析点。签名工作流必须在签名前于独立路径保留该清单；签名返回的清单必须与签名前保留副本逐字节哈希一致，不能信任签名制品内部可被同步篡改的清单。签名返回后及编译安装包前，门禁必须验证递归文件与目录集合均与清单完全一致：新增、删除或重命名任一文件或目录均失败；只有清单中且同时属于固定签名目标集合的一方 PE 允许因 Authenticode 产生哈希变化，其余托管 DLL、原生库、配置、许可与资源文件必须逐文件保持原哈希。每个签名目标还必须比较排除 PE Checksum、Certificate Table 目录项与附加证书表后的规范化 SHA-256，证明签名过程没有改动代码或其它映像内容；并继续通过预期签名者、有效签名、版本和源提交绑定校验，不能用“允许签名变化”绕过内容来源验证。
- 发布门禁必须验证全部一方文件由预期且一致的 SignPath Foundation 身份签署，而不只检查 `Status=Valid`；PawnIO 继续单独验证固定哈希和上游签名者。
- 自包含安装包必须随附实际分发依赖所要求的许可证文本、版权声明和第三方 notices，至少覆盖 .NET Runtime、所有直接 NuGet 依赖、LibreHardwareMonitor、PawnIO 与图标资源；许可清单必须与发布载荷核对，不能仅维护两项手工摘要。

---

### 横切设计约束

#### 异步与取消
- 所有网络请求、数据库读写、文件读写、图标提取、数据导入导出均使用异步 API，并接受 `CancellationToken`
- ViewModel 持有当前长任务的取消源；重复触发同一任务时先取消旧任务，再启动新任务
- 提权硬件修复在辅助进程成功启动前可以取消；辅助进程一旦启动便进入不可安全中断阶段，主程序必须等待其退出并按真实退出码报告结果，不能因调用方随后取消就返回“已取消”而让用户误判系统修改已经停止
- 取消源由其对应任务在结束后释放；后继任务只负责取消旧任务，不提前释放旧任务仍可能访问的资源
- 可重入刷新使用 generation／身份校验，仅当前请求可以写回绑定状态或结束加载指示，迟到请求只做清理
- WeatherService 刷新、数据导入/导出在收到取消后仅做必要清理，目标是在 200ms 内停止后续 I/O 与 UI 更新

#### 模型雷达网络与缓存
- 模型雷达运行时只访问固定的 `https://modeldial.com/api/v1/radar/latest.json`，使用系统代理；不得抓取 HTML、使用 WebView 或依赖 `/index.json`、`/changes.json`、`/agent-profile.json`、`/data/reference-snapshots/latest.json` 等其它接口。
- HTTP 客户端超时为 12 秒，发送 `UniDesk/<version>` User-Agent，响应体上限为 1 MiB；请求接受并响应 `CancellationToken`，不自动重试，也不得并发发起相同刷新请求。
- 只接受 `modeldial.com` 的 HTTPS 请求和固定代码链接；外部重定向不得把请求带到其它域名。未知 JSON 字段忽略，未知主版本或关键结构不兼容必须返回 `SchemaError`，解析或请求失败不得使应用崩溃。
- 缓存优先展示，后台刷新不得阻塞模块首次显示；模块关闭或应用退出必须取消在途请求并停止刷新调度。

#### UI 线程与节流
- Service 层不得直接操作 WPF 控件；仅返回模型或通过事件/回调通知 ViewModel
- ViewModel 仅在最终需要更新绑定属性时切回 UI 线程，避免在 UI 线程执行 I/O 或复杂计算
- `SettingsService.InitializeAsync` 必须在后台线程完成 SQLite 初始化并一次性预载全部设置，且在构造任何 WPF ViewModel 前完成；同步 `GetSetting`／`GetValue` 只允许读取已初始化缓存。迁移或恢复使缓存失效后，异步流程必须先重新加载缓存，不能让后续 Dispatcher 调用回退到同步 SQLite 查询
- 拖拽预览、列表刷新、天气状态切换等高频 UI 更新采用节流/合批策略，默认节流窗口 50-100ms
- 待办列表使用支持虚拟化的列表控件（`ListBox` / `ListView` + `VirtualizingStackPanel`）；快捷启动区固定最多 8 项，无需额外虚拟化
- `SystemMetricsMonitor` 在后台串行调用同步硬件 reader；同一时间最多一个采样，隐藏硬件模块时暂停读取，完成后才调度绑定属性更新

#### 异常、日志与用户提示
- App 启动时注册 `DispatcherUnhandledException`、`AppDomain.CurrentDomain.UnhandledException`、`TaskScheduler.UnobservedTaskException`
- 到达 `DispatcherUnhandledException` 的异常视为不可恢复：使用原子一次性保护，提示后调用 `Shutdown(-1)`，不继续运行应用
- 异常统一写入 `%LOCALAPPDATA%\UniDesk\logs\`，日志按日期滚动；首实例启动时仅删除第一层中超过 7 天且严格命名为 `yyyy-MM-dd.log` 的文件
- 测试进程必须把 Logger 根目录显式隔离到独立临时目录，不得创建、迁移或写入真实 `%LOCALAPPDATA%\UniDesk`；生产默认日志目录保持不变
- 面向用户的错误提示统一通过 `INotificationService` 输出，避免 Service 直接弹窗
- 关键失败路径必须保留可恢复状态，例如天气失败回退缓存、导入失败保留原数据库、热键注册失败回退旧配置
- 数据库初始化失败必须记录后向上抛出，由启动流程明确提示并终止启动
- 设置页一次保存涉及的全部设置（含模块布局）必须在单一事务中提交；事务失败不得留下部分新值，也不得由后台延迟写入静默重试本次失败批次。主设置提交成功后的天气缓存清理、剪贴板历史裁剪和界面刷新属于派生维护，失败时单独记录并提示，不得把已经提交成功的设置误报为保存失败。
- 备份还原以导入事务提交为成功边界；提交后的设置重载、语言切换、图标补全和各模块刷新失败时，必须明确提示“数据已还原但界面刷新失败”，不得显示“还原失败”或诱导用户重复导入。
- 网络传输失败、HTTP 状态失败、城市未找到、定位不可用和定位反查失败必须保持可区分的结果类型；不得把连接异常伪装成城市无效或定位失败。

#### 数据库并发与事务
- 数据库连接按操作创建并使用连接池；批量恢复使用一个连接和一个显式事务
- 文件数据库初始化时启用 WAL，保留 Microsoft.Data.Sqlite 默认锁等待；不启用 `Cache=Shared`
- 数据库版本按语义版本比较，不使用字符串序比较
- 一次用户操作包含多个相互依赖的数据库写入时，必须使用同一事务给出单一成功语义；已提交的主记录不得因派生图标、排序或裁剪失败而向 UI 误报“新建失败”。派生文件必须在回滚时清理，或明确作为可重建的 best-effort 结果并记录诊断。
- 备份导出前必须 flush 待写设置，并从同一只读事务快照读取所有分区；任一源分区读取失败必须终止导出，且不得覆盖既有目标文件或显示成功。

#### 依赖注入与装配
- Window、Dialog、ViewModel、Service 全部接入 DI 容器，通过构造函数注入依赖
- 不使用 `ViewModelLocator` 作为主要装配方式，避免隐藏依赖关系；View 的 code-behind 仅做 UI 事件转发与窗口级消息 Hook
- Win32、注册表、文件系统、时间、HTTP 等外部依赖优先通过接口或独立 Helper/Adapter 封装，便于测试替身注入

#### 本地存储约定
- 应用数据根目录：`%LOCALAPPDATA%\UniDesk\`
- 数据库：`UniDesk.db`
- 天气缓存：`weather_cache.json`
- 模型雷达缓存：`cache\modeldial-radar.json`（完整路径为 `%LOCALAPPDATA%\UniDesk\cache\modeldial-radar.json`）
- 图标缓存：`icons\`
- 日志目录：`logs\`
- 模型雷达缓存是可重新下载的派生数据，不进入用户备份；备份仅保留模块开关和模块排序等 `ModuleSettings`，不写入榜单缓存。
- `WeatherApiKey` 与 `ClipboardHistory.Content` 使用 Windows DPAPI `CurrentUser` 范围保护，存储前缀为 `dpapi:v1:`；服务层对调用方保持明文语义
- `WeatherApiHost` 只接受 HTTPS、默认 443 端口且位于 `qweatherapi.com` 的开发者专属子域；禁止用户信息、路径、查询、片段、IP 地址和相似后缀域名，避免密钥被发送到非官方主机
- 自定义 `WeatherApiHost` 与 `WeatherApiKey` 必须成对配置并在持久化前完成连通性验证；验证失败时保留上一组有效配置，内置配额凭据不得与用户自定义 Host 混用
- 天气备份导出和导入均排除 `WeatherApiKey` 与 `WeatherApiHost`，旧备份携带的这两个字段也不得写回本机设置
- 数据库初始化后、首次读取设置前，`PrivacyMigrationService` 在一个事务中迁移既有明文；已保护值跳过，任一步失败则整体回滚
- 当前 Windows 用户无法解密的数据不回退为明文：天气密钥返回空，剪贴板记录从显示结果中省略，日志只记录键名或记录 ID

---

## 模块设计

### 1. 主窗口模块 (MainWindow)

#### 职责
- 承载所有功能卡片的主界面
- 管理窗口生命周期（吸附、折叠、置顶）
- 协调各子模块的交互

#### 主要功能
- **窗口管理**
  - 默认尺寸：360px 宽 × 600-720px 高
  - 默认位置：屏幕右侧，垂直居中
  - 自动吸附：拖动到屏幕左右边缘时自动对齐边缘
  - 置顶显示：默认置顶，允许在设置中关闭
  - 宽度调整：拖拽面板左侧边缘调整宽度（范围 320px - 520px），调整完成后持久化；收缩态保留当前宽度，确保时间、天气和下一项待办可读
  - 收缩功能：切换为约 178px 高的迷你仪表盘，点击展开按钮恢复最近一次保存的高度，不使用悬停自动展开

- **收缩状态设计**
  - 展开态：恢复最近一次保存的高度，显示全部启用模块
  - 收缩态：保持当前宽度并固定为约 178px 高；使用单层玻璃表面，不在外窗内重复绘制完整模块卡片边框
  - 标题栏只显示搜索、更多和展开；置顶、锁定、设置和最小化收入「更多」菜单，展开按钮保持可见强调态
  - 主信息区左右展示时间／日期和天气／城市，隐藏空气质量等次要详情
  - 主信息区中部使用无独立背景框的轻量硬件摘要，集中展示 CPU 使用率、内存使用率、CPU 温度和 GPU 温度；收缩态中间列固定为 56px，摘要使用 22px 标签列与自适应数值列，标签统一使用 8px 常规次文本，数值统一使用 9px 半粗主文本并在 3px 间距后左对齐，固定行距避免数值变化时跳动，并与两侧日期、天气保持同一视觉层级
  - 收缩态天气内容必须限制在右侧列内，与硬件摘要保留明确边界；城市行使用可压缩网格，城市名称和天气来源在空间不足时单行省略，不得依靠横向内容溢出或跨列覆盖
  - 底部状态条只展示下一项未完成待办及到期信息；无待办时显示无待办状态，不得在待办区域混入硬件指标
  - 仅点击展开按钮恢复，不使用鼠标经过自动展开，避免无意触发窗口尺寸变化
  - 键盘焦点使用与玻璃主题一致的强调边框，不显示系统默认虚线焦点框

- **桌面体验设置选择态**
  - 剪贴板历史最大保存量使用单选列表，`ClipboardHistoryMaxCount` 与当前选项双向绑定
  - 当前选项在设置页首次打开、恢复默认、取消修改和手动选择后都必须持续显示强调色边框、轻强调背景和半粗字体
  - 悬停和键盘焦点只提供临时反馈，不得覆盖或伪装持久选中态
  - 全新安装默认启用剪贴板历史，覆盖安装和升级保留用户既有选择；设置页必须直接说明历史仅保存在本机、可关闭，并指明可在「数据与备份」中清理，不在安装器增加额外权限提示

- **顶部区域**
  - 应用名称："UniDesk"（可选 Logo）
  - 设置按钮：打开 SettingsWindow
  - 最小化按钮：隐藏到托盘

#### 卡片排列顺序（从上到下）
- 展开态严格按照 `DashboardModuleIds` 中已启用模块的用户顺序排列；旧用户未保存 `ModelRadar` 时，追加的关闭项不会改变现有顺序。
- 新安装默认顺序仍为时间天气、硬件监视、快捷方式、待办事项、快速便签、快捷文本、模型雷达；首次展开只显示默认启用的时间天气、硬件监视、待办事项和快速便签，关闭项保留其模块管理顺序，启用后显示在用户选择的位置。

#### UI 样式
- **背景**：WPF 分层透明窗口与半透明中性主题画刷，透明度可调并真实透出桌面；不在分层窗口上叠加 DWM Mica/Acrylic 矩形底板。用户选择的色彩方案只提供强调色，不再整体染色窗口底面。
- **圆角**：窗口 16px、卡片 12px、控件 8px、小状态块 6px。
- **阴影**：透明主窗口的普通卡片不使用 `DropShadowEffect`；仅窗口壳层、独立弹窗等真正抬升层允许使用克制投影。
- **分隔符**：使用 1 DIP 语义边框；Hover、Pressed、KeyboardFocus 和 Disabled 必须分别定义，键盘焦点不得依赖系统默认虚线框。
- **文字渲染**：分层透明窗口继续使用稳定的 Grayscale 文本渲染；不得为了字体观感切换为可能产生残影的 ClearType。
- **显示适配**：设置窗口和主窗口必须在 `1366×768`、150% 缩放的可用工作区内保持标题栏、主要内容和底部操作区可达；窗口不得以固定最小高度把保存／取消按钮推到屏幕外。混合 DPI、多屏移动、显示器断开和系统文字缩放必须进入人工发布矩阵；分层透明窗口的 DPI awareness 变更必须经过真实多屏回归，不能只改清单。
- **多显示器边界**：窗口尺寸和位置必须按目标窗口或 owner 所在显示器的工作区计算，不得用主屏 `SystemParameters.WorkArea` 或 `VirtualScreen` 联合矩形代替单块显示器工作区。显示器选择必须先通过 `MonitorFromWindow`／`MonitorFromRect`／`MonitorFromPoint` 在统一的 Win32 物理像素坐标系完成，禁止把各屏绝对像素原点分别按自身 DPI 缩放后再跨屏比较；选定显示器后才将其 `rcWork` 和目标位置按该显示器 DPI 转换为 WPF DIP。主窗口保存位置时同时记录物理像素坐标，恢复时优先使用该坐标；旧版仅有 DIP 位置时采用兼容回退，并在下次保存后补齐物理坐标。显示器已断开时由 Win32 最近显示器规则回退并夹紧到真实工作区；非矩形排列中的空洞不得被视为可见区域。设置窗口始终在 owner 所在显示器内居中并保持底部操作区可达。

#### WidgetCard 编辑与布局持久化
- 每个 WidgetCard 右上角提供锁定按钮，默认锁定
- 解锁后进入可编辑状态：显示用于调整尺寸的拖拽手柄
- 可编辑状态下支持：
  - 拖拽调整高度（范围 120px - 600px）
  - 拖拽卡片顶部区域调整排序位置（拖拽过程提供占位与过渡效果，接近容器顶部/底部自动滚动）
  - 按下 Escape 或在无效区域释放鼠标时取消本次拖拽，不改变排序
- 调整尺寸或排序后，通过 LayoutService 持久化布局，应用重启后恢复

---

### 1.1 布局模块 (LayoutService)

#### 职责
- 管理 WidgetCard 的排序、尺寸与锁定状态
- 将布局状态持久化到 Settings 表（Key: "WidgetLayout"，Value: JSON）
- 应用启动时读取并恢复布局；解析失败时回退到默认布局

#### 布局数据
- WidgetLayout 字段：Order、Height、IsLocked（默认锁定）
- 默认顺序：ClockWeather → Shortcuts → Todos

---

### 2. 时钟天气模块 (ClockWeather)

#### 职责
- 实时显示当前时间、日期、星期
- 获取和缓存天气数据
- 支持城市自动定位和手动设置

#### 时钟数据结构
```csharp
public class ClockInfo
{
    public string Time { get; set; }        // HH:mm:ss
    public string Date { get; set; }        // yyyy年MM月dd日
    public string DayOfWeek { get; set; }   // 星期X
    public bool IsError { get; set; }
}
```

#### 天气数据结构
```csharp
public class WeatherInfo
{
    public string City { get; set; }
    public string Temperature { get; set; }      // 当前温度
    public string WeatherDesc { get; set; }      // 天气描述
    public string AirQuality { get; set; }       // 空气质量指数
    public string Humidity { get; set; }         // 湿度
    public string MaxTemp { get; set; }          // 最高温
    public string MinTemp { get; set; }          // 最低温
    public string IconCode { get; set; }         // 和风天气图标代码
    public DateTime FetchTime { get; set; }
    public bool IsExpired { get; set; }
}
```

#### 时钟实现细节
- 更新频率：1Hz（每秒）
- 刷新延迟：< 100ms
- 错误处理：显示上次有效数据 + 错误提示图标
- 异常恢复：自动重试

#### 天气缓存策略
- **缓存位置**：`%LOCALAPPDATA%\UniDesk\weather_cache.json`
- **缓存有效期**：30 分钟
- **格式**：JSON 文件

缓存文件结构：
```json
{
  "city": "北京",
  "temperature": "25°C",
  "weatherDesc": "晴",
  "airQuality": "优",
  "humidity": "60%",
  "maxTemp": "28°C",
  "minTemp": "18°C",
  "iconCode": "104",
  "fetchTime": "2026-05-17T10:30:00Z"
}
```

#### 定位策略
1. 全新安装创建用户数据库时显式写入 `AutoLocation=true`，首次启动即尝试自动定位；覆盖安装和升级必须保留既有值，不得重新开启用户已关闭的自动定位。`AutoLocation` 缺失或无法解析时仍按关闭处理，绝不能因设置损坏或旧数据缺项隐式请求位置权限
2. `AutoLocation=true` 时调用 Windows 定位服务获取坐标；系统权限被拒绝、定位服务关闭、定位超时或无坐标均视为明确的 `LocationUnavailable`，不得伪装为网络或 API 配置错误
3. 获得坐标后，使用当前有效的和风天气凭据通过 HTTPS 城市查询接口反查城市；不得在桌面客户端打包高德或其它第三方定位密钥
4. 自动定位不可用时回退到经过规范化的手动城市；旧值 `??`、`--`、纯标点和空白视为空，但不得通过数据库迁移删除其它用户设置
5. 手动城市与自动定位互斥：用户手动输入非空城市时立即关闭自动定位；用户开启自动定位时立即清空手动城市。加载或撤销已保存设置时不得触发互斥清理
6. 城市输入区必须直接显示格式提示：支持城市或区县名称及具体的国外城市名，例如“北京”“浦东”“Tokyo”；国家名称本身不作为城市查询值，不对省份等可被模糊搜索命中的文本额外做客户端阻断
7. 用户修改城市或定位模式后，保存时清除天气缓存并重新获取；缓存存在时保留最后有效天气并标记过期
8. 设置页必须明确告知 Windows 可能请求定位权限，以及坐标会发送给已配置的和风天气 HTTPS 主机用于城市反查
9. 设置页提供打开 Windows 位置设置的入口；应用只能引导用户调整系统权限，不得绕过或代替用户开启 Windows 定位。安装界面不增加重复的定位权限弹窗

**LocationProvider 实现方案**：
```csharp
public class LocationProvider
{
    // Windows 定位权限由用户显式启用；失败时回退到规范化后的手动城市
    public async Task<(double Latitude, double Longitude)?> GetLocationAsync()
    public async Task<string?> GetCityByCoordinatesAsync(double latitude, double longitude)
    public async Task<string?> ResolveCityAsync()
}
```

#### 和风天气 API 集成
- **API Provider**：https://dev.qweather.com/
- **必要参数**：
  - `X-QW-Api-Key`：API Key（Header 认证，支持个人 API Host）
  - `location`：城市代码或名称
  - `lang`：zh（中文）
- **建议接口拆分**：
  - 实时天气：当前温度、天气状态、图标代码
  - 3 日天气：当日最高温/最低温
  - 空气质量：AQI
- **聚合方式**：WeatherService 在后台并发请求多个端点并合并为单个 `WeatherInfo`，仅在全部完成或部分可降级完成后更新 UI
- **来源署名**：凡主面板展示和风天气数据，必须同时显示可见的 `QWeather` 来源链接并指向 `https://www.qweather.com`；展开态和收缩态均不得隐藏该署名

#### 天气图标策略
- 使用和风天气返回的天气代码（IconCode）映射本地 QWeather Icons 字体图标
- 本地图标缺失时允许降级使用通用占位图标
- UI 绑定统一使用 IconCode，通过 Converter 映射为字体图标

- **调用流程**
  ```
  检查缓存 → 未过期？返回缓存 → 过期/无缓存？
  → 网络请求 → 成功？缓存并返回 → 失败？返回上次缓存 + 过期提示
  ```

#### 天气错误处理
| 场景 | 处理 |
|------|------|
| 网络请求失败 | 返回最近缓存 + "数据可能已过期" |
| 无缓存且请求失败 | 显示"天气数据暂不可用" |
| API Key 无效 | 显示"API 配置错误，请检查设置" |
| 城市不存在 | 显示"城市不存在，请重新设置" |
| Windows 定位被拒绝或关闭 | 回退手动城市；无有效手动城市时显示"定位不可用，请输入城市" |
| 自动定位坐标反查失败 | 回退手动城市；不得显示为网络或 API 配置的统称错误 |

---

### 3. 待办事项模块 (Todo)

#### 职责
- 待办事项数据管理
- 快速新增待办
- 完成状态管理

#### 数据结构
```csharp
public class TodoItem
{
    public int Id { get; set; }
    public string Title { get; set; }
    public bool IsCompleted { get; set; }      // 完成状态
    public DateTime? DueDate { get; set; }     // 到期日期
    public TodoPriority Priority { get; set; } // 优先级
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; } // 完成时间
}

public enum TodoPriority { Low, Medium, High }
```

#### 查询范围与排序
显示范围：
- `DueDate == 今日日期（YYYY-MM-DD）` 或 `DueDate == null`
- SQL 查询示例：
  ```sql
  SELECT * FROM Todos 
  WHERE (DueDate = ? OR DueDate IS NULL) 
  ORDER BY CreatedAt ASC
  ```

#### MainWindow 待办列表
- 显示模式：竖向列表
- 每条待办显示：
  - 复选框：勾选状态切换
  - 标题：完成状态显示删除线
  - 完成时间提示（可选）
- 所有删除入口共用 `DeleteTodoCommand`，删除前显示包含待办标题的本地化确认框；取消确认不得调用数据库删除

#### 快速新增区
```
[文本输入框] [添加按钮]
```

- 输入框：TextBox，单行，最多 100 字，支持 Enter 键提交
- 添加按钮：输入框为空时禁用
- 快捷键：Enter 键快速添加

#### 上下文菜单
右键点击待办：
- 删除

#### 业务流程
```
快速添加 → 输入标题 + Enter/点击按钮 → 插入数据库(DueDate=今日) → 刷新列表
标记完成 → 勾选复选框 → 更新 IsCompleted=true, CompletedAt=Now → 显示删除线
标记未完成 → 取消勾选 → 更新 IsCompleted=false, CompletedAt=null → 移除删除线
删除 → 右键 → 点击删除 → 删除数据库记录 → 刷新列表
```

---

### 4. 快捷启动模块 (Shortcut)

#### 职责
- 快捷启动项管理
- 应用和文件夹启动
- 拖拽排序

#### 数据结构
```csharp
public enum ShortcutType { Application, Folder, File }

public class ShortcutItem
{
    public int Id { get; set; }
    public string Name { get; set; }           // 显示名称
    public string Path { get; set; }           // 文件/文件夹路径
    public ShortcutType Type { get; set; }
    public string IconPath { get; set; }       // 本地缓存图标路径
    public int SortOrder { get; set; }         // 排序序号
    public DateTime CreatedAt { get; set; }
}
```

#### 快捷启动区布局
- 最多显示 8 个项目
- 网格布局：每行 4 个图标
- 图标尺寸：48px × 48px
- 名称显示：图标下方，1 行，超出显示省略号
- 当快捷启动项数量达到 8 个时隐藏“添加”按钮，阻止继续添加

#### 启动逻辑
- **Type = Application**
  - 使用 `Process.Start()` 启动 .exe 文件
  - 支持 .lnk 快捷方式（直接调用）
  - 失败时显示错误提示

- **Type = Folder**
  - 使用 `explorer.exe` 打开文件夹
  - 路径不存在时显示错误提示

- **Type = File**
  - 使用系统默认关联程序打开文件
  - 路径不存在时显示错误提示

#### AddShortcutWindow 界面
选择添加方式：
- 选项1：添加应用程序 → 选择 .exe/.lnk → 自动提取名称和图标
- 选项2：添加文件夹 → 选择目录 → 自动提取名称，使用文件夹图标
- 选项3：添加文件 → 选择任意本地文件 → 自动提取名称和图标

名称编辑框：最多 50 字

操作按钮：确认添加 | 取消

#### 上下文菜单
右键点击快捷项：
- 删除

#### 拖拽排序
- 支持拖拽重排
- 拖拽完成后更新 SortOrder 到数据库并持久化
- 视觉反馈：拖拽时显示透明度 50%，放下时闪烁提示

#### 业务流程
```
添加应用 → 打开 AddShortcutWindow → 选择 .exe/.lnk
       → 自动提取名称/图标 → 修改名称(可选) → 保存
       → 插入数据库 → 刷新列表

启动应用 → 点击图标 → 验证路径存在 → Process.Start() → 成功/失败提示

删除 → 右键 → 删除 → 删除数据库记录 → 刷新列表（无需二次确认）

拖拽排序 → 拖动图标 → 实时更新 SortOrder → 完成后持久化
```

---

### 5. 设置模块 (Settings)

#### 职责
- 用户偏好设置管理
- 系统集成配置（托盘、自启动）
- 数据导入导出

#### SettingsWindow 配置项

| 配置项 | 类型 | 默认值 | 说明 |
|-------|------|-------|------|
| 主题 | ComboBox | 跟随系统 | 跟随系统/浅色/深色 |
| 城市 | TextBox | 空 | 手动输入城市；未启用自动定位时作为首选位置 |
| 自动定位 | Toggle | true（仅全新安装） | 调用 Windows 定位并将坐标发送给和风天气进行城市反查；升级保留既有值，缺失或无效值按 false 处理 |
| 开机自启 | Toggle | true（仅全新安装） | 随 Windows 启动；升级保留既有值 |
| 窗口置顶 | Toggle | true | MainWindow 始终置顶 |
| 面板透明度 | Slider | 70% | 范围：60%-100%；只预览主面板，设置窗口自身保持可读的中性遮罩 |
| 面板宽度 | Slider | 全新安装推荐 340 DIP | 范围：320-520 DIP；升级保留用户原值 |
| 面板高度 | Slider | 全新安装按当前工作区计算 | 常规下限 560 DIP；上限为当前显示器工作区高度减 16 DIP |
| 字体大小 | Slider | 标准（1.0） | 范围：0.90-1.18；字号与行高、最小控件高度同步缩放 |
| 自定义标题 | TextBox | UniDesk | 只改变主面板显示标题，不改变产品名和应用身份 |
| 适配当前屏幕 | Button | - | 将首选宽高重新计算为当前显示器的推荐值并立即预览 |
| 模块启用与顺序 | ModuleSettings | 时间天气、硬件监视、待办事项、快速便签启用 | 全新安装默认关闭快捷方式、快捷文本和模型雷达；升级保留既有开关与顺序，缺失的 `ModelRadar` 以关闭状态追加到列表末尾 |
| 全局热键 | HotkeyBox | Ctrl+Alt+Space | 呼出/隐藏 MainWindow（可配置并持久化） |
| 天气 API Key | TextBox | 空 | 和风天气 API Key |
| 天气 API Host | TextBox | 空 | 与 API Key 配套的 `*.qweatherapi.com` 专属主机 |
| 恢复默认设置 | Button | - | 恢复主题、透明度、字体和模块等默认值；面板宽高按当前显示器重新计算推荐值 |
| 导出数据 | Button | - | 导出版本化 JSON 备份 |
| 导入数据 | Button | - | 预检并导入 JSON 备份 |

#### 数据持久化
所有配置存储在 SQLite Settings 表：
```
Key: "Theme" → Value: "Dark" | "Light" | "System"
Key: "City" → Value: "北京"（可选；为空表示未设置）
Key: "AutoLocation" → Value: "true" | "false"
Key: "Startup" → Value: "true" | "false"
Key: "TopMost" → Value: "true" | "false"
Key: "WindowOpacity" → Value: "0.70"
Key: "PanelWidth" → Value: "340"（示例；保存的是用户首选 DIP，不是工作区夹紧后的实际宽度）
Key: "PanelHeight" → Value: "720"（示例；保存的是用户首选 DIP，不是工作区夹紧后的实际高度）
Key: "FontScale" → Value: "1.0"
Key: "DisplayTitle" → Value: "UniDesk"
Key: "WidgetLayout" → Value: "{...json...}"
Key: "ModuleSettings" → Value: "{...json...}"（包含模块 ID、启用状态和顺序；备份包含此设置，不包含模型雷达缓存）
Key: "Hotkey" → Value: "Ctrl+Alt+Space"
Key: "WeatherApiKey" → Value: "dpapi:v1:..."（Windows 当前用户范围）
Key: "WeatherApiHost" → Value: "abc.example.qweatherapi.com"（仅允许官方 HTTPS 专属主机）
```

#### 主题系统设计
```
启动时：读取 Settings["Theme"]
  ├─ "System" → 读取 Windows 注册表 AppsUseLightTheme
  ├─ "Light" → 应用浅色主题
  └─ "Dark" → 应用深色主题

运行时监听 Windows 主题变化事件：
  └─ 若 Settings["Theme"] == "System" 且检测到变化
     └─ 自动切换主题（1 秒内完成）

用户手动切换：
  └─ 立即应用新主题 + 保存到 Settings
```

#### 开机自启动实现
使用 Windows 注册表：
```
HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run
Key: "UniDesk"
Value: "C:\Path\To\UniDesk.exe"
```

#### 行为
- 全新安装首次启动时启用开机自启动；覆盖安装和升级保留既有设置
- 启用开机自启动：写入 Run 启动项
- 启用时若 `UniDesk` Run 值已经严格指向当前安装目录中的 `UniDesk.exe`，允许原位刷新；若现有命令可无歧义解析为完全限定的本地固定磁盘绝对路径、文件名严格为 `UniDesk.exe`，目标的现有祖先不含重解析点，且文件系统明确确认目标不存在，视为本产品遗留的失效启动值并允许替换。只要同名值已经存在，非字符串、空白字符串、命令畸形、`C:UniDesk.exe` 这类盘符相对路径、UNC、文件名不匹配、目标仍存在、祖先含重解析点或无法确定存在性时都必须拒绝覆盖；写入前还必须重新读取并确认值未变化，不能只凭通用值名称接管其它程序
- 禁用开机自启动：移除 Run 启动项
- SettingsWindow 打开时读取注册表状态并同步开关显示
- 注册表操作失败时提示错误并将开关恢复到操作前状态

#### 系统托盘集成
- **显示托盘图标**：应用启动时
- **双击托盘**：显示/隐藏 MainWindow
- **右键菜单**：
  ```
  显示/隐藏
  设置
  ---
  退出
  ```
- 选择“退出”时先执行清理（例如关闭数据库连接）后退出进程

#### 数据导入导出
- **导出**：
  1. 点击"导出数据"
  2. 始终排除 `WeatherApiKey`、`WeatherApiHost` 和内置天气凭据；默认排除剪贴板历史
  3. 用户可显式包含剪贴板历史；确认后再次提示该部分将以可读明文写入便携 JSON
  4. 打开文件保存对话框并写入 UTF-8、版本 5 JSON；`includedSections` 与 `containsSensitivePlaintext` 明确声明内容

- **导入**：
  1. 点击"导入数据"
  2. 打开文件打开对话框
  3. 用户选择 JSON 备份文件
  4. `PrepareImportAsync` 在任何写入前先拒绝超过 25 MiB 的文件，再读取、反序列化、校验版本与记录，并生成不可变导入计划
  5. 预览页显示各分区数量、敏感明文警告，以及所有快捷方式路径和启动参数；可执行文件、URI 或带参数项突出显示
  6. 仅在用户明确确认后调用 `ApplyImportAsync`，先 flush 既有设置写入，再在单连接、单事务内恢复全部分区
  7. 校验阶段限制设置项最多 1000 条、每个业务分区最多 10000 条；设置 key、名称、路径、参数、标题、正文和分类均有明确长度上限，任一超限必须在创建导入计划前拒绝且不得修改现有数据
  8. 校验阶段还必须验证业务记录的枚举值属于已定义域、剪贴板历史 `UseCount` 严格大于零、其它计数非负且不超过业务上限、文本片段分类不是空白字符串，并确认日期可解析且满足字段间约束；这些原始字段不得在应用计划时通过 clamp、默认枚举或默认日期静默改写。既有明确契约中的快捷方式连续排序和 `ModuleSettings` 目录补全仍属结构规范化，必须在预览与恢复中保持确定性
  9. 旧备份中的 `WeatherApiKey` 与 `WeatherApiHost` 一律忽略；剪贴板正文写入前使用 DPAPI 保护；任一步失败回滚全部修改，成功后刷新缓存和界面

#### SettingsWindow 交互流程
- 打开时：从 SettingsService 加载当前配置并填充控件
- 全新数据库的初始界面语言优先采用安装器显式传入的中／英／日／西语言；没有安装器提示时按当前 Windows UI 语言映射，无法映射才回退 `zh-CN`。覆盖安装不得覆盖用户已保存语言。
- 所有用户可见的验证和启动错误必须通过本地化资源映射；网络客户端或系统 API 的内部中文错误文本不得直接泄漏到非中文界面。
- 保存：持久化所有修改后的配置并关闭窗口
- 取消：关闭窗口且不保存修改
- 保存失败：提示错误并保持窗口打开
- 天气凭据发生变化时：先使用候选 Host／Key 验证；仅验证成功后持久化并关闭窗口，失败时保留原有效值和编辑状态
- 全新数据库不预填固定 `PanelWidth`／`PanelHeight`；主窗口首次获得目标显示器工作区后计算推荐值、持久化，并立即应用。既有数据库中的宽高值必须原样保留。
- 主窗口区分用户首选尺寸与当前实际尺寸：跨屏或工作区不足时只夹紧实际尺寸，不覆盖已保存首选值；返回较大工作区后恢复首选值。
- 用户手动调整宽高后保持该偏好，不得在每次启动时重新按比例放大或缩小。
- 适配当前屏幕：显式重新计算并保存当前显示器推荐宽高；恢复默认设置也使用同一推荐算法。

---

### 6. 窗口管理模块

#### 需求

| 功能 | 实现方案 |
|------|--------|
| 单实例运行 | 使用 Mutex 检测，重复启动时激活已运行实例 |
| 吸附对齐 | 窗口 Move 事件中检测距离边缘 < 20px 时自动吸附 |
| 折叠/展开 | AnimatedPanel 控件或手动 Canvas 动画，350ms 过渡 |
| 置顶显示 | 设置 Window.Topmost = true（用户可在设置关闭） |
| 最小化到托盘 | 监听 Window.Closing，设置 e.Cancel = true，隐藏窗口 |
| 全局热键 | Win32 RegisterHotKey/UnregisterHotKey，触发显示/隐藏 MainWindow |
| 托盘菜单 | NotifyIcon 右键菜单包含 显示/隐藏、设置、退出 |

#### 吸附算法
```csharp
private void Window_LocationChanged(object sender, EventArgs e)
{
    const int SnapDistance = 20;
    var workArea = SystemParameters.WorkArea;
    
    // 右侧吸附
    if (Left + Width + SnapDistance >= workArea.Right)
        Left = workArea.Right - Width;
    
    // 左侧吸附
    if (Left - SnapDistance <= workArea.Left)
        Left = workArea.Left;
}
```

---

### 7. 模型雷达模块 (ModelRadar)

#### 定位与职责
- `ModelRadar` 是与时钟天气、硬件监视、快捷方式、待办事项、快速便签和快捷文本平级的只读决策参考模块。它只展示 ModelDial 官方公开评测数据，不执行模型调用、不进行本地评测，也不修改任何模型工具配置。
- 模块由 `ModelRadarService`、`ModelRadarViewModel` 和 `ModelRadarModuleView` 组成：Service 负责固定端点的 HTTP、JSON 校验、决策选择、四维排序和文件缓存；ViewModel 负责启用／禁用、刷新、取消、状态和迟到结果抑制；View 只负责 WPF 展示与固定链接事件转发。

#### 数据源与兼容性
- 运行时唯一数据源为 `https://modeldial.com/api/v1/radar/latest.json`。不得抓取 ModelDial HTML、使用 WebView，或把 `/index.json`、`/changes.json`、`/agent-profile.json`、`/data/reference-snapshots/latest.json` 作为 MVP 运行时依赖。
- 必须验证 `schemaVersion` 属于支持的 `1.x`，发布时间可解析，`overallBatch`／排名数组结构有效，排名为正整数，模型 ID、完整模型名称和推理强度非空；可用得分必须在 0-100。未知字段忽略，未知主版本、关键字段缺失、结构无效或发布时间不可解析均进入 `SchemaError`。耗时、参考费用和能力分数是可选值，缺失显示 `--`，不得转换成零。
- 在线响应成功后记录批次和发布时间；界面必须区分最新在线结果、离线缓存和过期数据。缓存损坏只忽略并记录，不自动删除；成功响应先写同目录临时文件，再以原子替换更新正式缓存。

#### 排名与决策规则
- `overallRankings` 的综合榜严格保持发布方顺序，首项作为「综合最高」卡，展示完整模型名称、推理强度、综合得分及可用的后端／前端／知识分数。
- 后端、前端和知识榜均从综合批次配置派生：分别按 `backendScore`、`frontendScore`、`knowledgeScore` 降序；缺失值排在末尾；同分按原综合排名稳定排序。界面明确标注「按后端得分」「按前端得分」「按知识得分」，不得把派生顺序称为接口提供的独立官方名次。每类最多展示 Top 5，每行显示位置、完整配置（模型名称与推理强度不可拆开）、对应得分和官方 `decisionTags`。
- 「性价比推荐」在综合可用时只从 `overallRankings`、在 Pending 后端回退时只从 `rankings`，均按发布方顺序选择首个 `decisionTags` 包含精确 `value` 的配置；不得使用 `score / cost`、价格、厂商或其它公式重算，也不得用 `lowest_cost` 冒充。没有官方 `value` 标签时显示「本批次暂无性价比推荐」；参考费用必须标注为评测参考费用，不是用户实际账单。
- 当 `overallBatch` 尚未发布或 `overallRankings` 为空，状态为 `Pending`，显示「综合结果暂未完成」，禁用综合、前端、知识标签，自动切换到 `rankings` 的后端 Top 5；不得把缺失能力按零分计算综合得分。此时仍只依据官方数组中的 `value` 标签决定推荐，不自行推导推荐。

#### 界面与归属
- 顶部使用通用雷达风格图标，不使用 ModelDial Logo 或任何模型厂商 Logo；标题字号和样式与相邻模块一致，标题区只保留「模型雷达」和手动刷新按钮，不重复显示最新状态、批次或发布时间。两张纵向紧凑决策卡的主文案固定为「综合最高」和「性价比推荐」；模型名称与加粗的推理强度在同一配置行相邻展示。
- 排名行显示当前位置、完整模型名称、推理强度、当前分类得分和非空的官方 `decisionTags`；当前配置没有官方标签时不渲染占位行，也不显示误导性的 `--`。ToolTip 提供四项分数、耗时、评测参考费用和路由；这些指标的缺失值统一显示 `--`。
- 底部显示 `ModelDial Radar`、`CC BY 4.0`、数据发布时间、固定的完整榜单链接 `https://modeldial.com/radar`，并显示「公共评测参考，实际表现可能因账号、路由和端点而异。」许可说明链接固定为 `https://modeldial.com/data-license`。不得打开 JSON 返回的任意 URL。

#### 缓存、刷新与生命周期
- 缓存文件为 `%LOCALAPPDATA%\UniDesk\cache\modeldial-radar.json`。启用模块时先读缓存，有缓存立即展示；缓存超过 6 小时后后台刷新，启用期间最多每 6 小时检查一次；用户可手动刷新。手动刷新期间禁用刷新按钮，并保证同一时间最多一个请求。
- UI 状态仅允许 `Loading`、`Fresh`、`Stale`、`Unavailable`、`Pending` 和 `SchemaError`。请求失败但有缓存时继续展示并明确标记「离线缓存」或「数据可能已过期」；无缓存时显示友好错误和重试按钮；SchemaError 不得回退为未经验证的数据。
- 模块未启用时必须零网络请求、零刷新 Timer；启用后由 ViewModel 持有刷新取消源。禁用模块或应用退出时立即停止刷新并取消在途请求，`Dispose()` 解除事件并释放取消源。每次刷新递增 generation，只有当前 generation 可以写回绑定状态，迟到结果只清理资源，不得覆盖新结果。

---

## 数据模型

### 核心模型

#### TodoItem
```csharp
public class TodoItem
{
    public int Id { get; set; }
    public string Title { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? DueDate { get; set; }
    public TodoPriority Priority { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public enum TodoPriority { Low, Medium, High }
```

#### ShortcutItem
```csharp
public enum ShortcutType { Application, Folder, File }

public class ShortcutItem
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Path { get; set; }
    public ShortcutType Type { get; set; }
    public string IconPath { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

#### AppSettings
```csharp
public class AppSetting
{
    public string Key { get; set; }
    public string Value { get; set; }
}
```

#### WidgetLayout
```csharp
public class WidgetLayout
{
    public string WidgetKey { get; set; } // ClockWeather/Shortcuts/Todos
    public int Order { get; set; }
    public double Height { get; set; }
    public bool IsLocked { get; set; }
}
```

#### WeatherInfo
```csharp
public class WeatherInfo
{
    public string City { get; set; }
    public string Temperature { get; set; }
    public string WeatherDesc { get; set; }
    public string AirQuality { get; set; }
    public string Humidity { get; set; }
    public string MaxTemp { get; set; }
    public string MinTemp { get; set; }
    public string IconCode { get; set; }
    public DateTime FetchTime { get; set; }
    public bool IsExpired { get; set; }
}
```

---

## 数据库设计

### 数据库位置
`%LOCALAPPDATA%\UniDesk\UniDesk.db`

### 表结构

#### Todos 表
```sql
CREATE TABLE Todos (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Title TEXT NOT NULL,
    IsCompleted INTEGER NOT NULL DEFAULT 0,
    DueDate TEXT,
    CreatedAt TEXT NOT NULL,
    CompletedAt TEXT
);

CREATE INDEX idx_todos_due_date ON Todos(DueDate);
CREATE INDEX idx_todos_created_at ON Todos(CreatedAt);
```

#### Shortcuts 表
```sql
CREATE TABLE Shortcuts (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL,
    Path TEXT NOT NULL,
    Type TEXT NOT NULL DEFAULT 'Application',
    IconPath TEXT,
    SortOrder INTEGER NOT NULL DEFAULT 0,
    CreatedAt TEXT NOT NULL
);

CREATE INDEX idx_shortcuts_sort_order ON Shortcuts(SortOrder);
```

- Type 取值：`Application`、`Folder`、`File`

#### Settings 表
```sql
CREATE TABLE Settings (
    Key TEXT PRIMARY KEY,
    Value TEXT
);
```

### 数据类型说明
- **整数**：INTEGER
- **文本**：TEXT（所有日期存储为 ISO 8601 格式字符串：yyyy-MM-ddTHH:mm:ss）
- **颜色**：TEXT（十六进制格式：#RRGGBB）
- **布尔值**：INTEGER（0 = false, 1 = true）

### 迁移策略
- 使用版本号 (Settings["DbVersion"]) 跟踪数据库版本
- 每次应用启动检查版本，执行必要的 ALTER TABLE 操作
- 保持向后兼容性
- `LumiDesk` 文件迁移只在目标 `UniDesk.db` 尚不存在时执行；目标数据库已存在即视为用户已完成迁移或已使用新版，不得因遗留旧目录中的锁定或损坏文件阻止现有 UniDesk 启动。首次迁移失败时必须写诊断并停止启动，不得生成新空数据库掩盖旧数据。

---

## UI/UX 设计

### Calm Glass 设计系统

该设计系统以“中性玻璃底面、单一强调色、清晰字体角色、低噪声交互”为准则。参考站点的字体分工、宽松行距和细边框只作为设计输入，不复制其绿色品牌、网页英雄区、终端装饰或卡片上浮效果。

#### 字体角色

| Token | 字体与回退 | 基准字号／行高 | 用途 |
|------|------------|---------------|------|
| `DisplayFontFamily` | 内嵌 Space Grotesk；中文回退 Microsoft YaHei UI | 窗口标题 18/26、模块标题 14/20，Semibold | 品牌、窗口标题、模块标题 |
| `BodyFontFamily` | Segoe UI Variable Text → Segoe UI → Microsoft YaHei UI | 正文 13/20，Regular | 菜单、设置、待办、说明文字 |
| `DataFontFamily` | 内嵌 JetBrains Mono；中文回退 Microsoft YaHei UI | 12-13/18，Medium | 时间、温度、百分比、网络值、快捷键 |
| `CaptionFontFamily` | `BodyFontFamily` | 11-12/17-18，Regular | 时间戳、状态、辅助说明 |

- 仅打包必要的静态 TTF 字重和对应 SIL OFL 1.1 许可证，不引入字体 NuGet 包，也不打包完整中文字体。
- `FontScale` 只缩放文字 Token；图标字体不随文字比例失真。所有固定高度控件必须在 1.18 倍字号下仍无裁剪。

#### 语义颜色

浅色中性基线为 `#EFF1F5`，主文字 `#4C4F69`，辅助文字 `#6C6F85`；深色中性基线为 `#1E1E2E`，主文字 `#CDD6F4`，辅助文字 `#A6ADC8`。实际窗口和卡片通过 Alpha 保留玻璃效果。

所有色彩方案必须完整提供下列语义资源，而不是只修改窗口背景：

```text
WindowSurfaceBrush
SecondarySurfaceBrush
CardSurfaceBrush
CardHoverBrush
ControlSurfaceBrush
PrimaryTextBrush
SecondaryTextBrush
MutedTextBrush
AccentBrush
AccentSoftBrush
FocusRingBrush
DividerBrush
SuccessBrush
WarningBrush
DangerBrush
```

- 用户已有八套色彩方案及保存值保持兼容；方案色只映射到 Accent 及其派生状态。
- 日历、完成态、逾期、优先级、删除区和弹窗不得保留与主题无关的直接颜色。
- 设置窗口表单内容层必须保持足够遮罩，不能因主面板透明度降低而让后方内容穿透到影响阅读。

#### 几何与间距

| 元素 | 规格 |
|------|------|
| 窗口圆角 | 16 DIP |
| 模块卡片圆角 | 12 DIP |
| 输入框、按钮、导航项圆角 | 8 DIP |
| 小状态块圆角 | 6 DIP；胶囊仅用于标签和状态 |
| 主面板外边距／模块间距 | 12／10 DIP |
| 模块／设置卡片内边距 | 14／20 DIP |
| 导航项／按钮／输入控件基准高度 | 44／36／34 DIP |

#### 交互状态

- Hover：轻微提高背景对比度并显示强调边框。
- Pressed：降低内容层透明度，不做整体缩放。
- KeyboardFocus：使用 2 DIP `FocusRingBrush`；任何 `FocusVisualStyle={x:Null}` 都必须有等价替代。
- Disabled：文字、图标和边框同时降级，仍可辨识控件边界。
- 只允许 120-160ms 的颜色或透明度过渡；透明窗口上的模块不得使用位移、缩放、卡片上浮或逐卡片重阴影。

### 自适应布局

尺寸计算使用目标显示器工作区的 WPF DIP，不直接使用物理分辨率：

```text
RecommendedWidth  = min(340, WorkAreaWidth - 32)
RecommendedHeight = min(roundTo20(clamp(WorkAreaHeight * 0.70, 560, 840)),
                        WorkAreaHeight - 16)
ActualWidth        = clamp(PreferredWidth, min(320, WorkAreaWidth - 32),
                           min(520, WorkAreaWidth - 32))
ActualHeight       = clamp(PreferredHeight, min(560, WorkAreaHeight - 16),
                           WorkAreaHeight - 16)
```

- 模块行继续使用 `Auto`，统一由主面板滚动承接；不引入模块独立尺寸滑块或嵌套滚动。
- 宽度上限维持 520 DIP；在模块没有响应式双列布局前不得仅为“自由度”扩大上限。
- 跨显示器时重新计算实际边界并夹紧，但不得静默覆盖首选宽高。

### 动画设计

| 动画 | 时长 | 效果 |
|------|------|------|
| 窗口出现 | 160ms | 淡入；不使用会改变窗口边界的位移 |
| 折叠/展开 | 现有时序 | 保留宽度过渡与内容淡入/淡出，必须通过透明渲染回归 |
| Hover／Pressed | 120-160ms | 仅颜色或透明度变化 |
| 加载中 | 循环 | 保留旋转加载圆，并遵循现有取消与可见性逻辑 |

---

## 技术栈

### 核心框架
- **.NET Framework**: .NET 10 LTS
- **UI Framework**: WPF
- **设计语言**: Fluent Design 2.0

### 关键 NuGet 依赖

| 包名 | 版本 | 用途 |
|------|------|------|
| CommunityToolkit.Mvvm | Latest | MVVM 框架 |
| Microsoft.Data.Sqlite | Latest | SQLite 数据库 |
| Hardcodet.NotifyIcon.Wpf | Latest | 系统托盘 |
| System.Drawing.Common | Latest | 图标和系统资源处理 |
| System.Net.Http | Latest | HTTP 请求 |

### 开发工具
- **IDE**: Visual Studio 2022 / Rider
- **VCS**: Git
- **测试框架**: xUnit
- **代码分析**: StyleCop, FxCop

### 构建目标
- **目标框架**: net10.0-windows10.0.18362.0
- **输出类型**: Exe (可执行文件)
- **发布平台**: Windows x64；ARM64 与 x86 不在当前安装包支持范围内

---

## 项目结构

```
UniDesk/
├── src/
│   └── UniDesk/
│       ├── Views/                      # XAML 视图
│       │   ├── MainWindow.xaml
│       │   ├── SettingsWindow.xaml
│       │   ├── TodoEditWindow.xaml
│       │   └── Windows/
│       │       ├── ToastWindow.xaml
│       │       └── CompactConfirmWindow.xaml
│       │
│       ├── ViewModels/                 # MVVM ViewModels（构造函数注入 Service）
│       │   ├── MainWindowViewModel.cs
│       │   ├── SettingsViewModel.cs
│       │   ├── TodoEditViewModel.cs
│       │   ├── WidgetCardViewModel.cs
│       │   └── ColorSchemeOptionViewModel.cs
│       │
│       ├── Models/                     # 数据模型
│       │   ├── TodoItem.cs
│       │   ├── TodoPriority.cs
│       │   ├── TodoDatePreset.cs
│       │   ├── ShortcutItem.cs
│       │   ├── WeatherInfo.cs
│       │   ├── ClockInfo.cs
│       │   ├── CalendarDayItem.cs
│       │   └── WidgetLayout.cs
│       │
│       ├── Services/                   # 业务逻辑服务
│       │   ├── TodoService.cs
│       │   ├── TodoBackupService.cs
│       │   ├── ShortcutService.cs
│       │   ├── WeatherService.cs
│       │   ├── QWeatherApiClient.cs
│       │   ├── ClockService.cs
│       │   ├── SettingsService.cs
│       │   ├── StartupService.cs
│       │   ├── TrayService.cs
│       │   ├── HotkeyService.cs
│       │   ├── WindowService.cs
│       │   ├── LayoutService.cs
│       │   ├── DatabaseService.cs
│       │   ├── NoteService.cs              # 便签服务（保留数据库兼容，UI 已移除）
│       │   └── NotificationService.cs
│       │
│       ├── Helpers/                    # 工具类
│       │   ├── LocationProvider.cs
│       │   ├── ConfigSecretProtector.cs
│       │   ├── WeatherApiDefaults.cs
│       │   ├── WeatherIconResolver.cs
│       │   ├── CalendarDayBuilder.cs
│       │   ├── TodoSortHelper.cs
│       │   ├── ShortcutLaunchHelper.cs
│       │   ├── ShortcutLimitHelper.cs
│       │   ├── AppIconHelper.cs
│       │   ├── ModuleIconHelper.cs
│       │   ├── AppColorSchemeCatalog.cs
│       │   ├── Debouncer.cs
│       │   └── ...更多工具类
│       │
│       ├── Controls/
│       │   └── TodoSwipeRow.xaml
│       │
│       ├── Resources/                  # 资源文件
│       │   ├── Themes/
│       │   │   ├── Light.xaml
│       │   │   ├── Dark.xaml
│       │   │   └── Shared.xaml
│       │   └── WeatherIcons/
│       │
│       ├── App.xaml
│       │   App.xaml.cs
│       └── UniDesk.csproj
│
├── tests/
│   └── UniDesk.Tests/
│       ├── TodoServiceTests.cs
│       ├── TodoSortHelperTests.cs
│       ├── ShortcutServiceTests.cs
│       ├── WeatherServiceTests.cs
│       ├── WeatherIconResolverTests.cs
│       ├── DatabaseServiceTests.cs
│       ├── SettingsServiceTests.cs
│       ├── StartupServiceTests.cs
│       └── UniDesk.Tests.csproj
│
├── docs/
│   ├── DESIGN.md                       # 本文档
│   └── DEVELOPMENT_PLAN.md
│
└── README.md
```

---

## 关键特性实现

### 1. 贴边吸附

**需求**：窗口拖动至屏幕边缘 20px 范围内时自动对齐边缘

**实现方案**：
- 监听 `Window.LocationChanged` 事件
- 计算当前位置与屏幕工作区边界的距离
- 若距离 < 20px，自动调整窗口位置

**关键代码位置**：`Helpers/WindowHelper.cs`

### 2. 折叠/展开动画

**需求**：
- 展开状态：360px 宽
- 收缩状态：40px 宽
- 过渡时间：350ms
- 悬停或点击后自动展开

**实现方案**：
- 使用 WPF `DoubleAnimation` 驱动宽度变化
- 内容使用 `Opacity` 动画淡入/淡出
- 收缩态下点击展开，或悬停 500ms 后自动展开

**关键代码位置**：ViewModel 与 Service（View 的 code-behind 仅做 UI 事件转发）

### 2.1 面板宽度拖拽与持久化

**需求**：
- 拖拽面板左侧边缘调整宽度（320px - 520px）
- 折叠状态宽度固定 40px
- 调整完成后持久化，展开时恢复最近一次保存宽度

**实现方案**：
- 通过 WindowService 监听鼠标拖拽并计算宽度（限制最小/最大值）
- 调整结束时写入 Settings["PanelWidth"]
- 展开时读取 PanelWidth 并应用
- PanelWidth 无效时回退到默认宽度 360px

### 3. 主题切换

**需求**：
- 启动时读取系统主题或用户设置
- 支持手动切换
- 运行时实时监听系统主题变化

**实现方案**：
- 在 App.xaml 中定义资源字典：`Light.xaml`、`Dark.xaml`
- 在 App.xaml.cs 中实现 `ApplyTheme()` 方法
- 监听 `WM_SETTINGCHANGE` Windows 消息监听系统主题变化
- Settings 表中存储用户主题偏好

**关键代码位置**：
- `App.xaml.cs`
- `Resources/Themes/`
- `Services/SettingsService.cs`

### 4. 实时时钟更新

**需求**：
- 每秒更新一次时间
- 更新延迟 < 100ms
- 错误时显示上次有效数据 + 错误图标

**实现方案**：
- `ClockService` 使用 `DispatcherTimer` 设置 1Hz 频率
- 每次 Tick 时计算当前时间并通过 `INotifyPropertyChanged` 通知 UI
- ViewModel 绑定 `ClockInfo` 属性

**关键代码位置**：`Services/ClockService.cs`

### 5. 天气数据缓存与更新

**需求**：
- 30 分钟缓存
- 缓存过期自动更新
- 网络失败降级到缓存
- 支持城市切换

**实现方案**：
- 缓存文件：`%LOCALAPPDATA%\UniDesk\weather_cache.json`
- 启动时检查缓存有效性
- 定时更新任务（DispatcherTimer，30 分钟）
- 网络请求失败时捕获异常并返回缓存
- 每次刷新拥有独立取消源；新请求可以取消旧请求，但旧请求完成时只能释放自身取消源，不得清空或释放后来请求的取消源

**关键代码位置**：`Services/WeatherService.cs`

### 6. 数据库迁移

**需求**：
- 支持多个版本的数据库 schema
- 自动执行迁移
- 保证数据不丢失

**实现方案**：
- Settings 表中存储 `DbVersion`
- `DatabaseService.InitializeAsync()` 在启动时执行版本检查
- 使用语义版本比较，根据版本差异执行对应的 ALTER TABLE 操作
- schema 创建、迁移、默认值和版本更新在事务内完成
- 所有数据库写操作使用参数化查询，防止 SQL 注入
- 数据库文件损坏或无法访问时记录错误日志，并向上层抛出包含错误描述的异常

**关键代码位置**：`Services/DatabaseService.cs`

### 7. 单实例运行

**需求**：
- 同一时间只运行一个应用实例
- 重复启动时激活已运行实例

**实现方案**：
- 使用 `Mutex` 检测重复启动
- 首实例监听仅限当前用户的命名管道；重复实例发送 `Activate` 后退出
- 首实例通过 WPF Dispatcher 调用 `IWindowService.ActivateWindow()` 恢复并激活自有窗口，不按窗口标题查找全局窗口

**关键代码位置**：`Helpers/SingleInstanceHelper.cs`、`App.xaml.cs` 中的 `OnStartup()`

### 8. 全局热键呼出/隐藏

**需求**：
- 提供全局热键用于呼出/隐藏 MainWindow，且热键可在 SettingsWindow 配置并持久化
- 呼出后 500ms 内可交互，隐藏在 300ms 内完成

**实现方案**：
- HotkeyService 封装 Win32 RegisterHotKey/UnregisterHotKey，并将触发事件转发到 MainWindow 显示/隐藏逻辑
- SettingsService 持久化热键配置（Settings["Hotkey"]）
- 显示/隐藏使用非阻塞动画与异步调度，避免 UI 卡顿

### 8. 快捷方式图标提取

**需求**：
- 从 .exe 文件提取图标
- 从 .lnk 快捷方式提取关联图标
- 文件夹使用系统图标

**实现方案**：
- 使用 `Icon.ExtractAssociatedIcon()` API
- .lnk 文件通过 Shell.Application COM 接口获取目标文件图标
- 提取后缓存到本地：`%LOCALAPPDATA%\UniDesk\icons\`
- 派生 PNG 必须先写入同目录唯一临时文件，完整解码校验后再原子替换目标；同一目标路径的生成必须串行化，已有缓存只有通过 PNG 完整性校验后才可复用
- 数据库写入失败后的清理只能删除本次调用仍然拥有且内容哈希未变化的派生文件，不能删除并发调用已经替换或采用的图标

**关键代码位置**：`Helpers/IconExtractor.cs`

---

## 开发计划

### Phase 1：项目初始化与基础架构（第 1-2 周）

- [ ] 项目结构创建
- [ ] NuGet 依赖配置
- [ ] MVVM 基础框架搭建
- [ ] 数据库初始化
- [ ] DI 容器配置

### Phase 2：核心模块开发（第 3-5 周）

- [ ] MainWindow 基础布局
- [ ] 时钟模块
- [ ] 天气模块（包括 API 集成）
- [ ] 待办模块（CRUD + 优先级 + 到期日期）
- [ ] 快捷启动模块（CRUD）

### Phase 3：系统集成与优化（第 6-7 周）

- [ ] 主题系统实现
- [ ] 系统托盘集成
- [ ] 开机自启动
- [ ] 设置页面
- [ ] 窗口管理（吸附、折叠、置顶）

### Phase 4：测试与打磨（第 8 周）

- [ ] 单元测试编写
- [ ] UI 测试
- [ ] 性能优化
- [ ] Bug 修复
- [ ] 文档补充

### Phase 5：发布与后续（第 9+ 周）

- [ ] 构建发布版本
- [ ] 使用受控证书签名主程序、硬件服务和安装器，并运行发布就绪门禁
- [ ] 用户文档
- [ ] 反馈收集
- [ ] 迭代更新

---

## 附录：API 设计参考

### WeatherService API

```csharp
public interface IWeatherService
{
    Task<WeatherInfo?> GetWeatherAsync(string city, CancellationToken cancellationToken = default, bool notifyUser = true);
    Task<WeatherInfo?> GetCachedWeatherAsync();
    Task<WeatherInfo?> RefreshWeatherAsync(CancellationToken cancellationToken = default, bool notifyUser = true);
    void CancelRefresh();
    Task SetCityAsync(string city);
    Task<QWeatherValidationResult> ValidateApiKeyAsync(string apiKey, string? apiHost = null, CancellationToken cancellationToken = default);
    string GetEffectiveApiKey();
}
```

### TodoService API

```csharp
public interface ITodoService
{
    Task<List<TodoItem>> GetAllTodosAsync();
    Task<TodoItem?> GetTodoAsync(int id);
    Task<int> CreateTodoAsync(TodoItem todo);
    Task UpdateTodoAsync(TodoItem todo);
    Task DeleteTodoAsync(int id);
    Task ToggleCompleteAsync(int id);
    Task MarkCompletedAsync(int id);
    Task MarkUncompletedAsync(int id);
    Task<List<TodoItem>> GetTodayTodosAsync();
}
```

### ShortcutService API

```csharp
public interface IShortcutService
{
    Task<int> CreateShortcutAsync(ShortcutItem shortcut);
    Task DeleteShortcutAsync(int id);
    Task UpdateSortOrderAsync(int id, int newOrder);
    Task<IEnumerable<ShortcutItem>> GetAllShortcutsAsync();
    Task LaunchShortcutAsync(int id);
}
```

---

**文档版本**: 1.3  
**最后审阅**: 2026年5月20日  
**下一个审阅周期**: 代码与文档对齐后更新
