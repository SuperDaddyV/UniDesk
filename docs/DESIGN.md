# UniDesk 设计文档

**版本**: 2.0.0
**最后更新**: 2026年7月11日
**项目**: UniDesk - Windows 11 桌面侧边助手应用

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
UniDesk 是运行于 Windows 11 的桌面侧边助手应用，以悬浮右侧面板形式呈现，集成时钟天气、硬件监视、快捷方式、待办事项、快速便签和快捷文本等核心功能，帮助用户在不中断主要工作流的情况下快速访问信息和启动应用。

### 核心目标
- **顺滑稳定**：耗时任务不阻塞 UI 线程，交互与动画顺滑，未处理 UI 异常会提示并安全终止，避免在未知状态下继续运行
- **美观现代**：遵循 Fluent Design（Windows 11 风格），统一圆角、阴影、间距与字体
- **数据私密**：所有数据本地存储于 SQLite，无云同步
- **易用友好**：托盘、热键、开机自启、主题跟随系统等桌面助手体验特性齐全

### 支持平台
- **目标平台**：Windows 11（及 Windows 10 v1903+）
- **目标框架**：.NET 9
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
- 六个可见模块分别由 `TimeWeatherViewModel`、`HardwareMonitorViewModel`、`ShortcutsViewModel`、`TodosViewModel`、`QuickNotesViewModel`、`QuickTextViewModel` 管理状态与命令。
- 六个 WPF 视图分别位于 `Controls/*ModuleView.xaml`；主窗口只组合控件并按 `DashboardModuleIds` 管理显示、顺序和高度。
- 快捷方式鼠标排序、文件拖放和添加弹层状态归 `ShortcutsModuleView`／`ShortcutsViewModel`；主窗口 code-behind 不处理模块内部输入。
- 待办完成圆圈由 `TodosModuleView` 的显式鼠标处理器调用 `TodosViewModel.ToggleTodoCommand`，不使用脱离可视树后无法解析祖先绑定的 `MouseBinding`。
- 便签编辑器的主操作文案为「完成」；窗口关闭必须先完成保存清理，保存失败时保持编辑器打开，成功后再调度到 Dispatcher 下一轮执行真正关闭，不得在 `OnClosing` 调用栈内递归 `Close()`。
- `QuickNoteService` 更新和删除返回实际成功状态；编辑器与列表只有在数据库确实受影响后才关闭或刷新。
- 子模块自行持有事件、计时器和取消源，并在 `Dispose()` 中解除订阅；主壳统一触发子模块清理。

### 系统指标读取边界

- `SystemMetricsService` 只组合 CPU、GPU、内存和网络四类读取结果，不直接包含原生 API 或传感器选择实现。
- `SensorSelection` 是无状态选择策略；CPU、GPU、内存和网络读取器位于 `Services/SystemMetrics/`，各自拥有原生资源与释放责任。
- `SystemMetricsMonitor` 继续负责后台串行采样、禁用暂停、迟到结果抑制和向硬件监控 ViewModel 发布快照。

### 持续集成

- `.github/workflows/ci.yml` 在 `main` 推送和 Pull Request 上使用 `windows-latest`、.NET `9.0.x` 执行 restore、Release build 和 Release test。
- CI 不执行发布、制品上传、部署或密钥读取。

---

### 横切设计约束

#### 异步与取消
- 所有网络请求、数据库读写、文件读写、图标提取、数据导入导出均使用异步 API，并接受 `CancellationToken`
- ViewModel 持有当前长任务的取消源；重复触发同一任务时先取消旧任务，再启动新任务
- 取消源由其对应任务在结束后释放；后继任务只负责取消旧任务，不提前释放旧任务仍可能访问的资源
- 可重入刷新使用 generation／身份校验，仅当前请求可以写回绑定状态或结束加载指示，迟到请求只做清理
- WeatherService 刷新、数据导入/导出在收到取消后仅做必要清理，目标是在 200ms 内停止后续 I/O 与 UI 更新

#### UI 线程与节流
- Service 层不得直接操作 WPF 控件；仅返回模型或通过事件/回调通知 ViewModel
- ViewModel 仅在最终需要更新绑定属性时切回 UI 线程，避免在 UI 线程执行 I/O 或复杂计算
- 拖拽预览、列表刷新、天气状态切换等高频 UI 更新采用节流/合批策略，默认节流窗口 50-100ms
- 待办列表使用支持虚拟化的列表控件（`ListBox` / `ListView` + `VirtualizingStackPanel`）；快捷启动区固定最多 8 项，无需额外虚拟化
- `SystemMetricsMonitor` 在后台串行调用同步硬件 reader；同一时间最多一个采样，隐藏硬件模块时暂停读取，完成后才调度绑定属性更新

#### 异常、日志与用户提示
- App 启动时注册 `DispatcherUnhandledException`、`AppDomain.CurrentDomain.UnhandledException`、`TaskScheduler.UnobservedTaskException`
- 到达 `DispatcherUnhandledException` 的异常视为不可恢复：使用原子一次性保护，提示后调用 `Shutdown(-1)`，不继续运行应用
- 异常统一写入 `%LOCALAPPDATA%\UniDesk\logs\`，日志按日期滚动；首实例启动时仅删除第一层中超过 7 天且严格命名为 `yyyy-MM-dd.log` 的文件
- 面向用户的错误提示统一通过 `INotificationService` 输出，避免 Service 直接弹窗
- 关键失败路径必须保留可恢复状态，例如天气失败回退缓存、导入失败保留原数据库、热键注册失败回退旧配置
- 数据库初始化失败必须记录后向上抛出，由启动流程明确提示并终止启动
- 设置手动保存失败必须向调用者传播；后台写入失败保留待写值供后续重试

#### 数据库并发与事务
- 数据库连接按操作创建并使用连接池；批量恢复使用一个连接和一个显式事务
- 文件数据库初始化时启用 WAL，保留 Microsoft.Data.Sqlite 默认锁等待；不启用 `Cache=Shared`
- 数据库版本按语义版本比较，不使用字符串序比较

#### 依赖注入与装配
- Window、Dialog、ViewModel、Service 全部接入 DI 容器，通过构造函数注入依赖
- 不使用 `ViewModelLocator` 作为主要装配方式，避免隐藏依赖关系；View 的 code-behind 仅做 UI 事件转发与窗口级消息 Hook
- Win32、注册表、文件系统、时间、HTTP 等外部依赖优先通过接口或独立 Helper/Adapter 封装，便于测试替身注入

#### 本地存储约定
- 应用数据根目录：`%LOCALAPPDATA%\UniDesk\`
- 数据库：`UniDesk.db`
- 天气缓存：`weather_cache.json`
- 图标缓存：`icons\`
- 日志目录：`logs\`
- `WeatherApiKey` 与 `ClipboardHistory.Content` 使用 Windows DPAPI `CurrentUser` 范围保护，存储前缀为 `dpapi:v1:`；服务层对调用方保持明文语义
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
  - 宽度调整：拖拽面板左侧边缘调整宽度（范围 320px - 520px），折叠状态宽度固定 40px，调整完成后持久化
  - 折叠功能：收起为 40px 宽窄条，点击或悬停 500ms 展开到最近一次保存的宽度

- **折叠状态设计**
  - 展开态：恢复最近一次保存的宽度（默认 360px），显示所有卡片
  - 收缩态：40px 宽，仅显示图标
  - 动画过渡：350ms easing
  - 收缩态交互：右侧边缘显示 16px 宽的展开箭头区域（图标 8px）
    - 点击箭头：立即展开至最近一次保存宽度
    - 悬停 500ms：自动展开至最近一次保存宽度
    - 视觉反馈：悬停时箭头高亮显示

- **顶部区域**
  - 应用名称："UniDesk"（可选 Logo）
  - 设置按钮：打开 SettingsWindow
  - 最小化按钮：隐藏到托盘

#### 卡片排列顺序（从上到下）
1. 时钟天气区域（时间、日期、天气信息）
2. 快捷启动区
3. 待办区

#### UI 样式
- **背景**：毛玻璃效果（Mica 或 Acrylic，透明度 85% 可调）
- **圆角**：8px 圆角半径
- **阴影**：柔和投影（Elevation 4）
- **分隔符**：浅色分割线

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
1. 启用自动定位时，优先通过 HTTPS 高德 IP 定位接口获取城市
2. 高德不可用、未配置 Key 或请求超时时，回退到 Settings 表中保存的城市
3. 不再调用明文 HTTP IP 定位服务；没有获批的坐标提供方时，`GetLocationAsync()` 返回空
4. 用户修改：立即清除缓存并重新获取

**LocationProvider 实现方案**：
```csharp
public class LocationProvider
{
    // HTTPS 高德 IP 定位，失败时由主流程读取已保存城市
    protected virtual async Task<string?> GetCityByAmapIpAsync()
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
| 城市 | TextBox | 自动定位 | 手动输入或自动定位 |
| 自动定位 | Toggle | true | 启用自动定位 |
| 开机自启 | Toggle | false | 随 Windows 启动 |
| 窗口置顶 | Toggle | true | MainWindow 始终置顶 |
| 面板透明度 | Slider | 85% | 范围：30%-100% |
| 面板宽度 | Slider | 360px | 范围：320px-520px |
| 全局热键 | HotkeyBox | Ctrl+Alt+Space | 呼出/隐藏 MainWindow（可配置并持久化） |
| 天气 API Key | TextBox | 空 | 和风天气 API Key |
| 恢复默认布局 | Button | - | 重置 WidgetLayout 与 PanelWidth 并立即应用 |
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
Key: "WindowOpacity" → Value: "0.85"
Key: "PanelWidth" → Value: "360"
Key: "WidgetLayout" → Value: "{...json...}"
Key: "Hotkey" → Value: "Ctrl+Alt+Space"
Key: "WeatherApiKey" → Value: "dpapi:v1:..."（Windows 当前用户范围）
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
- 启用开机自启动：写入 Run 启动项
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
  2. 默认排除 `WeatherApiKey` 和剪贴板历史
  3. 用户可显式包含剪贴板历史；确认后再次提示该部分将以可读明文写入便携 JSON
  4. 打开文件保存对话框并写入 UTF-8、版本 5 JSON；`includedSections` 与 `containsSensitivePlaintext` 明确声明内容

- **导入**：
  1. 点击"导入数据"
  2. 打开文件打开对话框
  3. 用户选择 JSON 备份文件
  4. `PrepareImportAsync` 在任何写入前读取、反序列化、校验版本与记录，并生成不可变导入计划
  5. 预览页显示各分区数量、敏感明文警告，以及所有快捷方式路径和启动参数；可执行文件、URI 或带参数项突出显示
  6. 仅在用户明确确认后调用 `ApplyImportAsync`，先 flush 既有设置写入，再在单连接、单事务内恢复全部分区
  7. 旧备份中的天气密钥与剪贴板正文在直接插入前使用 DPAPI 保护；任一步失败回滚全部修改，成功后刷新缓存和界面

#### SettingsWindow 交互流程
- 打开时：从 SettingsService 加载当前配置并填充控件
- 保存：持久化所有修改后的配置并关闭窗口
- 取消：关闭窗口且不保存修改
- 保存失败：提示错误并保持窗口打开
- 恢复默认布局：重置 WidgetLayout 与 PanelWidth 为默认值并立即应用到 MainWindow

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

---

## UI/UX 设计

### 设计系统

#### 颜色方案

**浅色主题**
```
主背景: #FFFFFF (或 #F7F7F7 毛玻璃)
文本: #1A1A1A
次文本: #5A5A5A
分隔线: #E0E0E0
强调: #0078D4 (Win11 蓝)
成功: #107C10 (Win11 绿)
警告: #FFB900 (Win11 黄)
错误: #E74C3C (Win11 红)
```

**深色主题**
```
主背景: #1E1E1E (或 #272727 毛玻璃)
文本: #FFFFFF
次文本: #A0A0A0
分隔线: #3A3A3A
强调: #40E0D0
成功: #6BCF7F
警告: #FFD56F
错误: #F7630C
```

#### 排版
- **标题**：16px, 600 weight (Semibold)
- **卡片标题**：14px, 600 weight
- **正文**：12px, 400 weight (Regular)
- **小文本**：11px, 400 weight
- **字体**：Segoe UI (系统默认)

#### 间距
- **卡片内边距**：12px
- **卡片间距**：8px
- **列表项间距**：4px
- **按钮内边距**：8px 12px

#### 圆角
- **窗口**：8px
- **卡片**：4px
- **按钮/输入框**：4px
- **小组件**：2px

#### 阴影
- **窗口**：Elevation 16 (半径 16px, 模糊 20px, Y 偏移 8px)
- **卡片**：Elevation 4 (半径 8px, 模糊 8px, Y 偏移 2px)
- **悬停效果**：Elevation 8

### 响应式布局

| 屏幕宽度 | 面板宽度 | 备注 |
|---------|--------|------|
| 1920px+ | 360px | 标准 |
| 1440px | 320px | 平衡 |
| < 1440px | 依然 360px | 可能覆盖任务栏 |

### 动画设计

| 动画 | 时长 | 效果 |
|------|------|------|
| 窗口出现 | 200ms | 从下方滑入 + 淡入 |
| 折叠/展开 | 350ms | 宽度过渡 + 内容淡入/淡出 |
| 悬停按钮 | 150ms | 背景色过渡 + 微微放大 |
| 列表项删除 | 200ms | 滑出 + 淡出 |
| 加载中 | 循环 | 旋转加载圆 |

---

## 技术栈

### 核心框架
- **.NET Framework**: .NET 9
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
- **目标框架**: net9.0-windows10.0.18362.0
- **输出类型**: Exe (可执行文件)
- **平台**: x86 / x64 / AnyCPU

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
- [ ] 签名与认证
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
