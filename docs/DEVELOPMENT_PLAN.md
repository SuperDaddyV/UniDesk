# UniDesk 开发计划与任务清单

> **历史归档**：本文是 2026 年 5 月的早期实施清单，其中 `.NET 9`、旧模块范围和待办状态只描述当时基线，不是当前产品要求。当前规范以根目录 `AGENTS.md`、`global.json` 和 `docs/DESIGN.md` 为准；当前目标 SDK 为 `.NET 10.0.302`。

**项目**：UniDesk - Windows 11 桌面侧边助手  
**更新日期**：2026年5月20日
**文档版本**：1.5

---

## 开发阶段总览

| 阶段 | 名称 | 目标 |
|------|------|------|
| Phase 1 | 项目初始化与基础架构 | 搭建开发环境、建立项目结构、配置依赖 |
| Phase 2 | 核心模块开发 | 实现主窗口与核心功能模块（时钟天气/待办/快捷启动/布局） |
| Phase 3 | 系统集成与优化 | 托盘/热键/自启动/设置页集成与交互打磨，完善异常处理与日志 |
| Phase 4 | 测试与打磨 | 单元测试覆盖核心服务，按需求验收标准做自测与性能验证 |

---

## 可直接开干的编码实施顺序

以下顺序用于实际编码执行，目标是先打通基础骨架，再优先验证高风险系统能力，最后铺完整业务模块与验收。实际开发时优先按此顺序推进，Phase 任务清单作为详细检查表使用。

### Step 0：建立最小可运行骨架 ✅

- [x] 创建解决方案、主项目、测试项目与目录结构
- [x] 配置 .NET 9、WPF、Windows 最低版本、NuGet 依赖
- [x] 创建 `App.xaml`、`MainWindow.xaml`、基础资源字典
- [x] 首次运行目标：应用可启动、显示空主窗口、无崩溃

**完成标志**
- [x] `dotnet build` 通过
- [x] 应用启动后能显示空窗口

### Step 1：先打基础设施，不做业务功能 ✅

- [x] 接入 DI 容器，注册 Window、ViewModel、Service
- [x] 创建基础接口与空实现：Settings/Database/Notification/Window/Tray/Hotkey
- [x] 建立本地目录约定：数据库、日志、图标、缓存
- [x] 接入全局异常处理与日志写入
- [x] 建立统一用户提示通道，避免 Service 直接弹窗

**完成标志**
- [x] 所有基础服务可被容器解析
- [x] 制造一个测试异常时能记录日志并给出用户提示

### Step 2：先做数据底座 ✅

- [x] 实现 `DatabaseService`，完成数据库创建、表初始化、版本检查
- [x] 实现 `SettingsService`，打通读写配置、默认值初始化
- [x] 先完成最核心设置项：Theme、TopMost、PanelWidth、WidgetLayout、Hotkey、AutoLocation、City、WeatherApiKey
- [x] 先写 `DatabaseService` 与 `SettingsService` 的基础测试

**完成标志**
- [x] 首次启动能自动创建 `%LOCALAPPDATA%\UniDesk\UniDesk.db`
- [x] 重启后能读回默认设置与已修改设置

### Step 3：先验证主窗口壳子与 Windows 11 外观 ✅

- [x] 搭好 MainWindow 基础布局：标题区、卡片容器、滚动区域
- [x] 实现无边框、圆角、置顶开关、默认位置与默认尺寸
- [x] 验证 Mica/Acrylic 或降级背景方案在当前 WPF 实现中可用
- [x] 只做静态卡片占位，不接业务数据

**完成标志**
- [x] 主窗口外观达到预期
- [x] 窗口显示、隐藏、拖动无明显异常

### Step 4：优先做系统能力技术预验证 ✅

- [x] 验证托盘图标能显示、双击可切换窗口显示/隐藏、右键菜单可用
- [x] 验证全局热键能注册、冲突时能失败并给出提示
- [x] 验证单实例激活流程可行
- [x] 验证 WinRT `Geolocator` 权限行为，确认失败时能可靠降级到 IP 定位
- [x] 验证和风天气最小 API 调用、字段映射与错误码处理

**完成标志**
- [x] 以上能力至少各有一个最小可运行 Demo 或已接入主工程验证通过
- [x] 若某项不稳定，立即确定降级方案并记录到实现备注

**实现备注**
- 托盘图标：使用 `Hardcodet.Wpf.TaskbarNotification.TaskbarIcon` 实现，支持双击切换窗口、右键菜单
- 全局热键：使用 Win32 API `RegisterHotKey` 实现，支持热键解析、冲突检测与错误提示
- 单实例：使用 `Mutex` 实现，重复启动时自动激活已有实例窗口
- 定位：优先使用 WinRT `Geolocator`，失败时降级到 IP 定位（ipapi.co）
- 天气：完整实现和风天气 API 调用，包含城市查询、实时天气、空气质量、错误码处理、缓存机制

### Step 5：先做 Settings 和 Window 行为闭环 ✅

- [x] 完成 `SettingsWindow` 基础 UI 与 `SettingsViewModel`
- [x] 打通主题、置顶、透明度、面板宽度、自动定位、城市、API Key、热键配置
- [x] 实现 `WindowService`：置顶、前台激活、显示/隐藏、吸附、宽度调整
- [x] 实现恢复默认布局、保存、取消、失败回退逻辑

**完成标志**
- [x] 设置页可打开、可保存、可取消
- [x] 修改置顶、透明度、宽度后可立即作用到主窗口

### Step 6：实现 LayoutService 与主窗口交互骨架 ✅

- [x] 实现 `LayoutService` 的默认布局、序列化、反序列化、损坏回退
- [x] 在主窗口接入 WidgetCard 顺序与高度配置
- [x] 先完成锁定/解锁状态切换，再做调整高度与拖拽排序
- [x] 实现拖拽取消、自动滚动、布局持久化

**完成标志**
- [x] 重启后能恢复卡片顺序、高度、锁定状态与面板宽度
- [x] 布局 JSON 损坏时能回退默认布局

### Step 7：先做最简单稳定模块，快速形成正反馈 ✅

- [x] 实现 `ClockService` 与时钟区域
- [x] 时钟与天气在同一卡片区域显示
- [x] 确认每秒刷新、错误回退、UI 刷新延迟满足要求

**完成标志**
- [x] 主窗口顶部时钟正常工作

### Step 8：再做本地数据模块，顺序是 Todo → Shortcut ✅

- [x] 先做 `TodoService`、快速新增、完成切换、右键删除、优先级设置、到期日期
- [x] 再做 `ShortcutService`、添加弹窗、图标提取、启动逻辑、拖拽排序
- [x] 每完成一个服务就补对应单元测试，再进入下一个模块

**完成标志**
- [x] 待办、快捷启动都能独立完成 CRUD 或核心操作
- [x] 每个模块完成后都已接入主窗口

### Step 9：最后做天气模块 ✅

- [x] 实现 `LocationProvider`
- [x] 实现 `WeatherService`：缓存、接口聚合、取消刷新、失败降级
- [x] 接入城市切换、自动定位开关、API Key 校验
- [x] 完成天气卡片 UI、图标策略与过期提示
- [x] 支持个人 API Host + `X-QW-Api-Key` 认证方式

**为什么放后面**
- [x] 天气模块依赖外部 API、缓存、定位、设置项和错误处理，是全项目耦合最高的模块之一
- [x] 放在本地模块后实现，更容易复用已完成的 Settings、Notification、日志、取消机制

**完成标志**
- [x] 有缓存、无缓存、网络失败、API Key 错误、城市切换几条路径都能跑通

### Step 10：回头补托盘、热键、自启动的正式集成 ✅

- [x] 将前面的技术预验证能力正式接入主工程结构
- [x] 完成 `TrayService`、`HotkeyService`、`StartupService` 正式实现
- [x] 接好 MainWindow 关闭即最小化到托盘、托盘退出前清理、自启动注册表同步

**完成标志**
- [x] 托盘、热键、自启动与设置页形成完整闭环

### Step 11：统一做动画、节流、性能与异常打磨 ✅

- [x] 优化折叠/展开动画、拖拽预览、列表刷新策略
- [x] 检查所有 I/O 是否异步，所有长任务是否支持取消
- [x] 验证热键呼出/隐藏时延、时钟刷新、主题切换、拖拽流畅度
- [x] 排查未处理异常、资源释放、数据库句柄占用问题

**完成标志**
- [x] 性能与稳定性指标基本达标
- [x] 关键异常路径均能记录日志并友好提示

### Step 12：最后集中测试与验收

- [ ] 补齐 `TodoService`、`ShortcutService`、`LayoutService`、`DatabaseService` 单元测试
- [ ] 按需求验收自测项逐条走查
- [ ] 修复验收阶段发现的问题
- [ ] 输出可发布构建

**完成标志**
- [ ] 验收自测项全部过一遍
- [ ] 形成可交付版本

### 编码执行原则

- [ ] 每次只推进一个主步骤，避免同时横跳多个系统能力
- [ ] 每完成一个 Step 就先验证再继续，不要堆到最后一起调
- [ ] 外部依赖能力先做最小验证，再做完整封装
- [ ] 本地模块优先于外部模块，先把应用主体做稳
- [ ] 先有基础设施，再做交互打磨，不要一开始就堆动画和视觉细节

### 阶段交付规则

- [ ] 每完成一个可测阶段，必须先通过构建验证，再产出一版可运行文件（开发运行目录或发布目录）供主人测试
- [ ] 阶段交付不得只停留在“本地已验证”，必须提供可由主人在真实 Windows 环境直接运行的版本
- [ ] 涉及托盘、全局热键、开机自启、单实例、主题跟随、定位权限等系统能力的阶段，优先提供可运行版本做实机验证，不延后到最终发布
- [ ] 进入下一阶段前，先完成当前阶段的构建验证、主人测试反馈收集与必要修正，避免问题堆积到后期
- [ ] 最终发布版之前，至少应有多轮阶段性可运行版本交付，确保不会在最后一次发布构建时集中暴露前期问题

---

## Phase 1：项目初始化与基础架构

### 1.1 项目结构创建

- [ ] 创建解决方案和项目文件
  - [ ] UniDesk.csproj（主程序）
  - [ ] UniDesk.Tests.csproj（测试项目）
  
- [ ] 按规范创建目录结构
  - [ ] Views/
  - [ ] ViewModels/
  - [ ] Models/
  - [ ] Services/
  - [ ] Data/
  - [ ] Resources/
  - [ ] Helpers/

- [ ] 创建默认文件占位符
  - [ ] App.xaml / App.xaml.cs
  - [ ] MainWindow.xaml / MainWindow.xaml.cs

### 1.2 NuGet 依赖配置

- [ ] 安装核心框架依赖
  - [ ] CommunityToolkit.Mvvm (MVVM 框架)
  - [ ] Microsoft.Data.Sqlite (SQLite 数据库)
  - [ ] Hardcodet.NotifyIcon.Wpf (系统托盘)
  - [ ] System.Drawing.Common (图标和系统资源处理)

- [ ] 配置内置库使用
  - [ ] System.Net.Http (HTTP 请求)
  - [ ] System.Text.Json (JSON 序列化/反序列化)

- [ ] 配置项目文件
  - [ ] 目标框架设置为 .NET 9
  - [ ] Windows 10 版本最低支持 1903 (10.0.18362.0)
  - [ ] 配置 OutputType 为 WinExe

### 1.3 依赖注入与服务容器

- [ ] 在 App.xaml.cs 中配置 DI 容器
  ```csharp
  IServiceCollection services = new ServiceCollection();
  // 注册所有 Services (单例)
  // 注册所有 ViewModels
  _serviceProvider = services.BuildServiceProvider();
  ```

- [ ] 创建 Service 接口定义
  - [ ] IWeatherService
  - [ ] IClockService
  - [ ] ITodoService
  - [ ] IShortcutService
  - [ ] IDatabaseService
  - [ ] ISettingsService
  - [ ] IStartupService
  - [ ] ITrayService
  - [ ] IHotkeyService
  - [ ] IWindowService
  - [ ] ILayoutService

- [ ] 创建 Service 实现框架（暂为空实现）
  - [ ] WeatherService.cs
  - [ ] ClockService.cs
  - [ ] TodoService.cs
  - [ ] ShortcutService.cs
  - [ ] DatabaseService.cs
  - [ ] SettingsService.cs
  - [ ] StartupService.cs
  - [ ] TrayService.cs
  - [ ] HotkeyService.cs
  - [ ] WindowService.cs
  - [ ] LayoutService.cs

### 1.4 数据库初始化

- [ ] 创建本地数据目录
  - [ ] `%LOCALAPPDATA%\UniDesk\`
  - [ ] `icons\`
  - [ ] `logs\`
  - [ ] 临时导入导出目录

- [ ] 实现 DatabaseService
  - [ ] 数据库文件位置：`%LOCALAPPDATA%\UniDesk\UniDesk.db`
  - [ ] 自动创建目录结构
  - [ ] 执行数据库迁移（版本检查）

- [ ] 创建 Todos 表
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

- [ ] 创建 Shortcuts 表
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

- [ ] 创建 Settings 表
  ```sql
  CREATE TABLE Settings (
    Key TEXT PRIMARY KEY,
    Value TEXT
  );
  ```

- [ ] 初始化默认设置
  - [ ] Theme = "System"
  - [ ] WindowOpacity = "0.85"
  - [ ] TopMost = "true"
  - [ ] Startup = "true"（仅全新安装；升级保留既有值）
  - [ ] AutoLocation = "true"
  - [ ] City = "" (empty, 使用自动定位)
  - [ ] PanelWidth = "360"
  - [ ] WidgetLayout = "" (empty, 使用默认布局)
  - [ ] Hotkey = "Ctrl+Alt+Space"

### 1.5 MVVM 基础框架

- [ ] 创建基础 ViewModel 类
  - [ ] ObservableObject (from CommunityToolkit.Mvvm)
  - [ ] RelayCommand 使用范例

- [ ] 实现主 ViewModel
  - [ ] MainWindowViewModel.cs
  - [ ] SettingsViewModel.cs
  - [ ] TodoEditViewModel.cs
  - [ ] WidgetCardViewModel.cs

- [ ] 配置 DataContext 绑定
  - [ ] 通过 DI 创建各 Window / Dialog，并在构造函数中注入对应 ViewModel
  - [ ] 各 Window 的 DataContext 在构造函数中绑定
  - [ ] 禁止将业务逻辑放入 code-behind，仅保留 UI 事件转发与窗口消息 Hook

### 1.6 资源与主题框架

- [ ] 创建主题资源文件结构
  - [ ] Resources/Themes/Light.xaml (浅色主题)
  - [ ] Resources/Themes/Dark.xaml (深色主题)
  - [ ] Resources/Themes/Shared.xaml (共享资源)

- [ ] 定义核心颜色资源
  - [ ] 主背景色、文本色、强调色
  - [ ] 浅色/深色主题分别定义

- [ ] 创建字符串资源文件
  - [ ] Resources/Strings.resx (UI 文本)

### 1.7 工具类与辅助方法

- [ ] 创建 Helpers 目录下的工具类
  - [ ] IconExtractor.cs (图标提取)
  - [ ] FilePathValidator.cs (文件路径验证)
  - [ ] LocationProvider.cs (地理位置)
  - [ ] DateTimeExtensions.cs (日期时间扩展)
  - [ ] NotificationService.cs (消息提示服务)
  - [ ] SingleInstanceHelper.cs (单实例运行)

### 1.8 图标与资源

- [ ] 创建应用图标资源
  - [ ] 应用主图标 (16px, 32px, 48px, 256px)
  - [ ] 托盘图标 (16px)
  - [ ] 功能模块图标（时钟、天气、快捷启动等）
  - [ ] 控制按钮图标（设置、最小化、展开箭头等）
  - [ ] 状态提示图标（错误、成功、警告）

- [ ] 创建 Resources/Icons/ 目录
  - [ ] 整理所有图标文件
  - [ ] 统一命名规范

### 1.9 消息与通知系统

- [ ] 实现 NotificationService
  - [ ] ShowInfoMessage() - 信息提示
  - [ ] ShowWarningMessage() - 警告提示
  - [ ] ShowErrorMessage() - 错误提示
  - [ ] ShowSuccessMessage() - 成功提示
  - [ ] ShowConfirmDialog() - 确认对话框
  - [ ] 实现方式：Snackbar/Toast（浮动通知）或 MessageBox

- [ ] 在 ViewModel 中集成通知
  - [ ] 各 ViewModel 注入 NotificationService
  - [ ] 业务逻辑完成后调用相应通知方法

### 1.10 应用基线与验证

- [ ] 建立应用生命周期与日志基础
  - [ ] App.xaml.cs 注册 DispatcherUnhandledException
  - [ ] 注册 AppDomain.CurrentDomain.UnhandledException
  - [ ] 注册 TaskScheduler.UnobservedTaskException
  - [ ] 创建日志目录并验证写入权限
  - [ ] 约定用户提示统一走 NotificationService

- [ ] 建立异步与取消基础设施
  - [ ] 为 Weather/Import/Export 约定统一的 CancellationToken 传递方式
  - [ ] ViewModel 中约定“新任务启动前取消旧任务”的模式
  - [ ] 为 UI 高频更新约定 50-100ms 节流策略（拖拽预览、列表刷新、状态提示）

- [ ] 编译项目无错误
- [ ] 解决所有 NuGet 包冲突
- [ ] 运行时无崩溃（空 UI 状态）
- [ ] 验证 DI 容器能正确注入所有服务

---

## Phase 2：核心模块开发

### 2.1 主窗口模块 (MainWindow)

- [ ] 设计主窗口 XAML 布局
  - [ ] 无边框窗口配置 (WindowStyle="None")
  - [ ] 毛玻璃背景效果 (Mica/Acrylic)
  - [ ] 圆角设置 (CornerRadius="8")

- [ ] 实现主窗口卡片容器
  - [ ] ScrollViewer 容纳可滚动内容
  - [ ] 卡片容器支持拖拽占位、自动滚动与非阻塞动画
  - [ ] 待办内部列表使用支持虚拟化的列表控件，不使用纯 ItemsControl 硬堆列表

- [ ] 实现面板宽度拖拽与持久化
  - [ ] 拖拽面板左侧边缘调整宽度（320px - 520px）
  - [ ] 折叠状态宽度固定为 40px
  - [ ] 拖拽结束后持久化到 Settings["PanelWidth"]
  - [ ] 展开时恢复最近一次保存宽度
  - [ ] PanelWidth 无效时回退默认宽度 360px

- [ ] 实现折叠/展开功能
  - [ ] 存储展开/收缩状态
  - [ ] DoubleAnimation 驱动宽度变化
  - [ ] 收缩态右侧显示展开箭头 (16px 区域)
  - [ ] 点击展开箭头触发展开
  - [ ] 悬停 500ms 后自动展开
  - [ ] 内容淡入/淡出动画

- [ ] 实现窗口吸附功能
  - [ ] 监听 LocationChanged 事件
  - [ ] 检测距屏幕边缘 < 20px 时自动吸附
  - [ ] 同时支持左右边缘吸附

- [ ] 实现默认位置与尺寸
  - [ ] 默认尺寸：360px × 650px
  - [ ] 屏幕右侧，垂直居中位置计算

- [ ] 实现窗口置顶控制
  - [ ] Topmost 属性绑定到 SettingsViewModel
  - [ ] 用户可在设置中切换

- [ ] 实现标题栏
  - [ ] 显示应用名称 "UniDesk"
  - [ ] 设置按钮 (打开 SettingsWindow)
  - [ ] 最小化按钮 (隐藏到托盘)
  - [ ] 标题栏 Drag 移动窗口支持

- [ ] 实现窗口的 Close 按钮拦截
  - [ ] 监听 Window.Closing 事件
  - [ ] 设置 e.Cancel = true
  - [ ] 调用隐藏到托盘逻辑

- [ ] 实现 WidgetCard 锁定/编辑态
  - [ ] 默认锁定：禁止调整尺寸与排序
  - [ ] 解锁后显示拖拽手柄（调高）与拖拽区域（排序）
  - [ ] 高度范围：120px - 600px

- [ ] 实现 WidgetCard 拖拽排序与取消
  - [ ] 拖拽过程提供占位/预览/过渡效果
  - [ ] 拖拽接近容器顶部/底部自动滚动
  - [ ] Escape 取消拖拽且不改变排序
  - [ ] 完成后持久化布局（ILayoutService → Settings["WidgetLayout"]）

### 2.2 时钟天气模块 (ClockWeather)

- [ ] 设计时钟天气 UI 卡片
  - [ ] 左侧：时钟区域
    - [ ] 显示时间 (HH:mm:ss)
    - [ ] 显示日期 (yyyy年MM月dd日)
    - [ ] 显示星期 (星期X)
    - [ ] 错误提示图标（可选）
  - [ ] 右侧：天气区域
    - [ ] 城市名称
    - [ ] 天气图标（QWeather Icons 字体）
    - [ ] 当前温度 (大号字体)
    - [ ] 天气描述
    - [ ] 空气质量指数 / 湿度
    - [ ] 最高/最低温度

- [ ] 实现 ClockService
  - [ ] 使用 DispatcherTimer 1Hz 更新
  - [ ] 实现 INotifyPropertyChanged 通知 UI
  - [ ] 格式化日期字符串（中文）
  - [ ] 异常处理：保留上次有效数据

- [ ] 实现 LocationProvider
  - [ ] 优先使用和风天气 IP 定位 API
  - [ ] 降级到 ipapi.co 坐标定位 + 逆地理编码
  - [ ] 定位失败时提示用户在 SettingsWindow 中手动指定城市

- [ ] 实现天气缓存机制
  - [ ] 缓存位置：`%LOCALAPPDATA%\UniDesk\weather_cache.json`
  - [ ] 缓存有效期：30 分钟
  - [ ] JSON 序列化/反序列化

- [ ] 实现 WeatherService + QWeatherApiClient
  - [ ] 获取 API Key 从 SettingsService 或本地 secrets.json
  - [ ] 支持个人 API Host + X-QW-Api-Key Header 认证
  - [ ] 拆分并聚合实时天气 / 3日天气 / 空气质量接口
  - [ ] 实现缓存检查逻辑
  - [ ] 网络请求失败时返回缓存 + 过期提示
  - [ ] 支持取消刷新（CancellationToken），用户取消操作在 200ms 内响应
  - [ ] 城市修改后立即清空缓存并重新拉取天气

- [ ] 实现定时更新
  - [ ] 应用启动时立即获取一次
  - [ ] 定时任务每 30 分钟刷新一次 (DispatcherTimer)

- [ ] 实现天气图标策略
  - [ ] 根据天气代码（IconCode）通过 WeatherIconResolver 映射为 QWeather Icons 字体图标
  - [ ] 避免在 UI 刷新期间重复下载图片

- [ ] 错误处理
  - [ ] 网络错误 → 返回缓存 + "数据可能已过期"
  - [ ] 无缓存且失败 → 显示"天气数据暂不可用"
  - [ ] API Key 无效 → 显示"API 配置错误"

### 2.3 待办事项模块 (Todo)

- [ ] 设计待办列表 UI
  - [ ] ListBox/ListView + VirtualizingStackPanel 显示待办事项
  - [ ] 每项显示：复选框、标题(已完成显示删除线)、优先级颜色标记、到期日期
  - [ ] 支持滑动删除（TodoSwipeRow 控件）

- [ ] 设计快速新增区
  - [ ] TextBox 输入框 (max 100字)
  - [ ] Button 添加按钮 (输入框为空时禁用)
  - [ ] 支持 Enter 键提交

- [ ] 设计 TodoEditWindow
  - [ ] 标题编辑
  - [ ] 优先级选择 (Low/Medium/High)
  - [ ] 到期日期预设 (今天/明天/后天/本周/自定义)
  - [ ] 保存/取消按钮

- [ ] 实现 TodoService (CRUD)
  - [ ] CreateTodoAsync(TodoItem) → DueDate 设置为今日
  - [ ] UpdateTodoAsync(TodoItem) → 更新待办
  - [ ] MarkCompletedAsync(int id) → IsCompleted=true, CompletedAt=Now
  - [ ] MarkUncompletedAsync(int id) → IsCompleted=false, CompletedAt=null
  - [ ] ToggleCompleteAsync(int id) → 切换完成状态
  - [ ] DeleteTodoAsync(int id) → 删除记录
  - [ ] GetAllTodosAsync() → 查询所有待办
  - [ ] GetTodayTodosAsync() → 查询今日待办

- [ ] 实现待办事项交互
  - [ ] 勾选复选框 → MarkCompletedAsync → 显示删除线
  - [ ] 取消勾选 → MarkUncompletedAsync → 移除删除线
  - [ ] 快速新增 → 创建新 TodoItem
  - [ ] 右键/滑动删除 → 删除待办
  - [ ] 点击待办 → 打开 TodoEditWindow 编辑

- [ ] 实现 TodoBackupService
  - [ ] 待办数据定期备份与恢复

- [ ] 创建 TodoEditViewModel
  - [ ] Title, Priority, DueDate 属性
  - [ ] SaveCommand, CancelCommand

### 2.4 快捷启动模块 (Shortcut)

- [ ] 设计快捷启动 UI 卡片
  - [ ] 网格布局：4 列，2 行（最多 8 个）
  - [ ] 每项显示：48px 图标 + 名称 (截断省略号)
  - [ ] 达到 8 项时隐藏添加按钮，阻止继续添加

- [ ] 实现 ShortcutService (CRUD)
  - [ ] CreateShortcutAsync(ShortcutItem) → 插入数据库
  - [ ] DeleteShortcutAsync(int id) → 删除记录
  - [ ] UpdateSortOrderAsync(int id, int order) → 更新排序
  - [ ] GetAllShortcutsAsync() → 按 SortOrder 升序查询
  - [ ] LaunchShortcutAsync(int id) → 启动/打开

- [ ] 实现启动逻辑
  - [ ] Type = Application → Process.Start(path)
  - [ ] Type = Folder → explorer.exe path
  - [ ] Type = File → 使用系统默认关联程序打开文件
  - [ ] 异常处理 → 显示错误提示

- [ ] 设计 AddShortcutWindow UI
  - [ ] 选项1：添加应用程序 (OpenFileDialog, .exe/.lnk)
  - [ ] 选项2：添加文件夹 (FolderBrowserDialog)
  - [ ] 选项3：添加文件 (OpenFileDialog, 任意文件)
  - [ ] 名称编辑框 (max 50字)
  - [ ] 确认/取消按钮

- [ ] 实现图标提取
  - [ ] 创建 IconExtractor 工具类
  - [ ] .exe 文件 → Icon.ExtractAssociatedIcon()
  - [ ] .lnk 快捷方式 → 通过 COM 获取目标图标
  - [ ] 文件夹 → 使用系统文件夹图标
  - [ ] 图标保存到：`%LOCALAPPDATA%\UniDesk\icons\`

- [ ] 实现拖拽排序
  - [ ] 支持拖拽重排
  - [ ] 拖拽时透明度 50%
  - [ ] 完成后更新 SortOrder 到数据库

- [ ] 创建 ShortcutViewModel
  - [ ] Shortcuts ObservableCollection
  - [ ] 添加/删除/排序的 Command

- [ ] 实现"已满 8 项隐藏添加按钮"逻辑

### 2.5 集成各模块到 MainWindow

- [ ] 在 MainWindow 中组合所有卡片
  - [ ] 从上到下排列：时钟天气、快捷启动、待办
  - [ ] 各卡片绑定对应 ViewModel
  - [ ] 设置合理的间距和边距

- [ ] 测试各模块独立功能
  - [ ] 时钟每秒更新
  - [ ] 天气 30 分钟刷新
  - [ ] 待办 CRUD 功能
  - [ ] 快捷启动是否能启动应用

---

## Phase 3：系统集成与优化

### 3.1 主题系统

- [ ] 实现 ThemeManager 类
  - [ ] 监听 Windows 注册表主题变化 (WM_SETTINGCHANGE 消息)
  - [ ] 读取 HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize 中的 AppsUseLightTheme 值
  - [ ] 实现 OnThemeChanged 事件
  - [ ] 使用 RegisterWindowMessage() 和 WndProc 监听 WM_SETTINGCHANGE

- [ ] 在 App.xaml.cs 中集成主题管理
  - [ ] 应用启动时读取 Settings["Theme"]
  - [ ] 根据用户偏好应用主题
  - [ ] 若为 "System"，启用系统主题监听
  - [ ] 在 MainWindow 中的 SourceInitialized 事件中 Hook WndProc

- [ ] 实现主题切换方法
  ```csharp
  private void ApplyTheme(ThemeMode mode)
  {
      var dict = mode switch {
          ThemeMode.Light => new ResourceDictionary() { Source = ... },
          ThemeMode.Dark => new ResourceDictionary() { Source = ... },
          _ => ...
      };
      Application.Current.Resources.MergedDictionaries.Add(dict);
  }
  ```

- [ ] 测试主题实时切换
  - [ ] 修改 Windows 设置主题，应用 1 秒内响应
  - [ ] 验证深色主题下所有元素可见性
  - [ ] 验证浅色主题下所有元素可见性
  - [ ] 验证主题切换时 UI 无闪烁

### 3.2 系统托盘集成 (TrayService)

- [ ] 实现 TrayService
  - [ ] 使用 Hardcodet.NotifyIcon.Wpf
  - [ ] 创建托盘图标
  - [ ] 绑定事件处理

- [ ] 实现双击托盘图标功能
  - [ ] MainWindow 已显示 → 隐藏到托盘
  - [ ] MainWindow 已隐藏 → 显示并置于前台

- [ ] 实现托盘右键菜单
  - [ ] 显示/隐藏
  - [ ] 设置
  - [ ] ---
  - [ ] 退出

- [ ] 实现窗口关闭逻辑
  - [ ] 用户点击关闭按钮 → 隐藏到托盘（不退出）
  - [ ] 托盘菜单"退出" → 关闭数据库连接 + 退出进程

- [ ] 实现单实例运行
  - [ ] 在 App.OnStartup() 中创建 Mutex（名称基于应用GUID）
  - [ ] 若 Mutex 创建失败（已存在），说明已有实例运行
  - [ ] 通过 FindWindow + SetForeground 激活已有实例
  - [ ] 或使用 Windows 消息（WM_COPYDATA）通知已有实例
  - [ ] 新实例随后退出
  - [ ] 应用正常退出时释放 Mutex

### 3.3 开机自启动

- [ ] 实现 StartupService
  - [ ] EnableStartup() → 写入 Windows 注册表
    ```
    HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run
    Key: "UniDesk"
    Value: "C:\Path\To\UniDesk.exe"
    ```
  - [ ] DisableStartup() → 删除注册表项
  - [ ] IsStartupEnabled() → 读取注册表状态

- [ ] 异常处理
  - [ ] 注册表操作失败 → 显示错误提示 + 恢复开关状态

### 3.4 设置页面 (SettingsWindow)

- [ ] 设计 SettingsWindow UI 布局
  - [ ] 分组设置项（主题、位置、窗口、数据、API）
  - [ ] 保存/取消按钮

- [ ] 实现配置项 UI 控件
  - [ ] 主题选择 ComboBox (跟随系统/浅色/深色)
  - [ ] 城市设置 TextBox + 自动定位 Toggle
  - [ ] 开机自启 Toggle
  - [ ] 窗口置顶 Toggle
  - [ ] 面板透明度 Slider (30%-100%, 默认 85%)
  - [ ] 面板宽度 Slider (320px-520px, 默认 360px)
  - [ ] 全局热键配置控件（呼出/隐藏 MainWindow）
  - [ ] API Key TextBox
  - [ ] 恢复默认布局 Button（重置 WidgetLayout 与 PanelWidth，并立即应用）
  - [ ] 导出数据 Button
  - [ ] 导入数据 Button

- [ ] 实现 SettingsViewModel
  - [ ] 各配置项属性绑定
  - [ ] SaveCommand → 持久化配置
  - [ ] CancelCommand → 关闭窗口

- [ ] 实现配置加载逻辑
  - [ ] 打开 SettingsWindow 时从 SettingsService 加载所有配置

- [ ] 实现配置保存逻辑
  - [ ] 验证 API Key 非空（如果需要）
  - [ ] 将所有配置写入 Settings 表
  - [ ] 立即应用某些配置（如主题、透明度）
  - [ ] 保存失败时提示错误并保持窗口打开
  - [ ] 取消时关闭窗口且不保存修改

### 3.5 数据导入导出

- [ ] 实现导出功能
  - [ ] SaveFileDialog 让用户选择目标路径
  - [ ] 导出前释放数据库文件句柄并暂停新写入
  - [ ] 复制 UniDesk.db 文件到目标位置
  - [ ] 支持取消导出任务
  - [ ] 显示成功提示

- [ ] 实现导入功能
  - [ ] OpenFileDialog 让用户选择 db 文件
  - [ ] 先复制到临时路径再验证文件格式（尝试打开 → 检查表结构）
  - [ ] 验证失败 → 显示"导入失败：文件格式无效或已损坏"，并保留原有数据库不变
  - [ ] 验证成功 → 备份当前数据库 → 原子替换新文件
  - [ ] 支持取消导入任务
  - [ ] 提示用户重启应用

### 3.6 面板透明度控制

- [ ] 将 MainWindow 的 Opacity 绑定到 SettingsViewModel
- [ ] Slider 调整 Settings["WindowOpacity"]
- [ ] 实时预览透明度变化

### 3.7 全局热键与窗口行为 (HotkeyService / WindowService)

- [ ] 实现 HotkeyService
  - [ ] 注册与响应全局热键（可在 SettingsWindow 配置并持久化）
  - [ ] 热键触发：显示/隐藏 MainWindow
  - [ ] 取消/替换热键时及时注销旧热键
  - [ ] 新热键注册失败时恢复旧热键与旧配置
  - [ ] 处理无效组合或被系统/其他应用占用的冲突提示

- [ ] 实现 WindowService
  - [ ] 窗口置顶、吸附、前台激活等窗口行为封装
  - [ ] 热键呼出后 500ms 内可交互、隐藏 300ms 内完成（避免阻塞 UI）
  - [ ] 拖拽排序/调整高度/调整面板宽度过程保持界面可交互、不卡顿

### 3.8 错误处理与日志

- [ ] 创建日志系统
  - [ ] 记录到 `%LOCALAPPDATA%\UniDesk\logs\`
  - [ ] 按日期生成日志文件
  - [ ] 记录所有异常和关键操作

- [ ] 全局异常处理
  - [ ] App.xaml.cs 中添加 DispatcherUnhandledException 处理
  - [ ] 记录异常日志 + 显示用户友好提示

---

## Phase 4：测试与打磨

### 4.1 单元测试

- [ ] 测试 TodoService
  - [ ] 创建待办 → DueDate 是否设为今日
  - [ ] 更新待办 → 字段是否正确更新
  - [ ] 标记完成 → CompletedAt 是否正确记录
  - [ ] 标记未完成 → CompletedAt 是否置为 null
  - [ ] 删除待办 → 记录是否正确移除
  - [ ] 查询今日 → 是否正确过滤 (DueDate=today OR null)

- [ ] 测试 ShortcutService
  - [ ] 创建快捷项 → 能否正确插入
  - [ ] 删除快捷项 → 是否正确移除
  - [ ] 更新排序 → SortOrder 是否持久化
  - [ ] 查询所有 → 是否按 SortOrder 升序

- [ ] 测试 LayoutService
  - [ ] 布局序列化/反序列化是否正确
  - [ ] 数据损坏/解析失败时是否回退默认布局
  - [ ] PanelWidth 无效时是否回退默认宽度 360px

- [ ] 测试 DatabaseService
  - [ ] 初始化数据库 → 所有表是否创建成功
  - [ ] 参数化查询 → SQL 注入防护是否有效

- [ ] 测试 WeatherIconResolver
  - [ ] 天气代码到图标映射是否正确

### 4.2 需求验收自测

- [ ] 主题系统
  - [ ] 启动时跟随系统主题；当用户手动指定主题时优先使用用户设置
  - [ ] Windows 主题运行时切换时，若为“跟随系统”，应用 1 秒内自动切换
  - [ ] 重启后恢复上次主题偏好

- [ ] 主窗口与布局
  - [ ] 面板宽度拖拽范围 320px-520px；折叠宽度 40px；展开恢复最近一次保存宽度
  - [ ] WidgetCard 默认锁定；解锁后可调高（120px-600px）与拖拽排序；Escape 可取消拖拽且不改变排序
  - [ ] 调整后布局能持久化并在下次启动恢复（WidgetLayout、PanelWidth）

- [ ] 天气模块
  - [ ] 首次启动未设置城市时自动定位；关闭自动定位后可按手动城市获取天气
  - [ ] 缓存未过期时直接返回缓存；过期时重新请求
  - [ ] 网络失败时回退最近缓存并提示"数据可能已过期"；无缓存时显示"天气数据暂不可用"
  - [ ] 修改城市后立即清除缓存并重新获取天气

- [ ] 托盘与窗口管理
  - [ ] 双击托盘图标显示/隐藏 MainWindow
  - [ ] 右键托盘菜单包含 显示/隐藏、设置、退出；退出前完成清理后退出
  - [ ] 关闭 MainWindow 最小化到托盘而非退出
  - [ ] 单实例：重复启动激活已运行实例

- [ ] 待办/快捷启动
  - [ ] 待办支持空标题禁用添加、Enter 快速添加、完成/取消完成、优先级设置、到期日期、滑动删除、编辑弹窗
  - [ ] 快捷启动支持 Application/Folder/File 三种类型、最多 8 项、拖拽排序持久化

- [ ] 热键
  - [ ] 全局热键可配置并持久化
  - [ ] 热键冲突或注册失败时显示错误并恢复旧配置
  - [ ] 呼出后 500ms 内可交互；隐藏 300ms 内完成

- [ ] 设置页与数据
  - [ ] 打开设置页加载当前配置；保存成功后关闭；取消不保存；保存失败提示并保持窗口打开
  - [ ] 导出数据库：复制 UniDesk.db 到用户选择路径
  - [ ] 导入数据库：无效/损坏时提示“导入失败：文件格式无效或已损坏”，且保留原数据库不变；成功后覆盖并提示重启
  - [ ] 恢复默认布局：重置 WidgetLayout 与 PanelWidth 并立即应用
  - [ ] 开机自启动开关打开/关闭时同步注册表状态；失败时恢复开关

### 4.3 性能与稳定性验证

- [ ] 所有网络/数据库/文件等耗时任务异步执行不阻塞 UI
- [ ] 后台任务取消响应 < 200ms（天气刷新、数据导入/导出）
- [ ] 时钟每秒更新且刷新延迟 < 100ms
- [ ] 系统主题切换时 1 秒内自动切换（当主题为跟随系统）
- [ ] 异常不导致进程崩溃：记录日志并给出用户可理解提示（覆盖关键异常路径）

---

## 任务依赖关系

```
Phase 1 基础架构
    ↓
Phase 2 核心模块 (各模块可并行)
    ├─ 2.1 主窗口 (块)
    ├─ 2.2-2.6 各功能模块 (可并行)
    └─ 2.7 集成到主窗口
    ↓
Phase 3 系统集成 (依赖 Phase 2 完成)
    ├─ 3.1-3.6 各系统功能 (可大部分并行)
    └─ 3.7-3.8 优化与日志
    ↓
Phase 4 测试与打磨 (依赖 Phase 3 完成)
    ├─ 4.1 单元测试
    ├─ 4.2 验收自测
    └─ 4.3 性能与稳定性验证
```

---

## 检查点与验收标准

### Phase 1 验收标准
- ✅ 项目能正常编译
- ✅ 所有 NuGet 依赖已安装
- ✅ DI 容器配置完成
- ✅ 数据库能正确创建（首次启动）
- ✅ App 能启动到主窗口（空 UI）

### Phase 2 验收标准
- ✅ 所有功能模块能独立工作
- ✅ 时钟每秒更新
- ✅ 时钟能正确更新，天气能正确获取并缓存
- ✅ 待办/快捷启动 CRUD 功能完整
- ✅ 各模块 UI 正确显示和交互

### Phase 3 验收标准
- ✅ 主题能正确切换
- ✅ 托盘图标显示和交互正常
- ✅ 设置页面配置能正确保存
- ✅ 开机自启动功能可用
- ✅ 窗口吸附、折叠等功能工作正常

### Phase 4 验收标准
- ✅ TodoService/ShortcutService/LayoutService 核心逻辑测试通过
- ✅ 关键异常路径可记录日志并提示用户，且不导致进程直接崩溃
- ✅ 关键时延满足需求：取消 < 200ms；热键呼出 < 500ms、隐藏 < 300ms；主题跟随 < 1 秒；时钟刷新 < 100ms

---

**文档版本**: 1.5  
**最后更新**: 2026年5月20日  
**下一次审阅**: 代码与文档对齐后更新
