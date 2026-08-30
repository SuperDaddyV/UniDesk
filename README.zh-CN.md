# UniDesk

UniDesk 是一个轻量、个性化、清爽好用的 Windows 桌面侧边栏工具，把时间天气、硬件监视、模型雷达、快捷方式、待办事项、快速便签和快捷文本整合进一个顺手的桌面工作台。

<p align="center">
  <a href="README.md">简体中文</a> ·
  <a href="README.en-US.md">English</a> ·
  <a href="README.ja-JP.md">日本語</a> ·
  <a href="README.es-ES.md">Español</a>
</p>

![UniDesk 产品展示图](images/unidesk-hero.png)

## ✨ 主要特性

### 时间天气

- 显示当前时间、日期和农历信息。
- 显示天气、温度、空气质量、湿度和城市信息。
- 内置桌面日历，方便快速查看公历和农历日期。

### 硬件监视

- 实时查看 CPU、内存、GPU 使用率。
- 显示 CPU / GPU 温度。
- 显示整机网络上传 / 下载速度。
- GPU 温度会尽量从可用驱动和硬件监视来源读取；无法读取时会安全显示为 `--`。

### 快捷方式

- 支持添加常用应用、文件和文件夹。
- 支持从桌面或资源管理器拖拽添加。
- 支持快捷方式自由排序。
- 支持自定义主面板快捷方式显示数量。

### 待办事项

- 支持新增、编辑、完成和删除待办。
- 支持任务时间和优先级显示。
- 数据本地保存，适合日常事项记录。

### 快速便签

- 支持多条便签。
- 支持自动保存、置顶、复制和删除。
- 适合临时记录灵感、草稿、会议记录和备忘内容。

### 快捷文本

- 支持剪贴板历史。
- 支持常用短语。
- 支持一键复制。
- 支持敏感内容过滤，尽量避免保存验证码、密码、Token、Cookie 等敏感文本。

### 模型雷达

- 先用「综合最高」与「性价比推荐」给出决策结论，再提供综合、后端、前端、知识四类 Top 5 参考。
- 数据来自 [ModelDial Radar](https://modeldial.com/zh-CN/radar) 公开评测（[CC BY 4.0](https://modeldial.com/data-license)）；「性价比推荐」只采用发布方的官方 `value` 标签，UniDesk 不重新计算评分或费用。
- 模块默认关闭，关闭时不发起网络请求。UniDesk 运行且模块启用期间，优先读取本地缓存，每 6 小时最多自动检查一次，并只通过固定接口 `https://modeldial.com/api/v1/radar/latest.json` 刷新；也可随时手动刷新。
- 这是只读决策参考，不调用模型、不执行本地评测，也不修改模型工具配置。公开评测不代表合作或背书，实际表现可能因账号、路由和端点而异。

### 模块管理

- 支持模块显示 / 隐藏。
- 支持模块自由排序。
- 可以按自己的使用习惯组合桌面面板。

### 个性化设置

- 支持主题配色、窗口透明度、面板宽度、面板高度和字体大小调节。
- 支持自定义顶部显示名称。
- 支持置顶、锁定、收起、开机自启和快捷方式数量设置。
- 支持恢复默认布局和恢复默认设置。

### 数据备份与还原

- 支持本地数据备份。
- 支持待办事项、快速便签、剪贴板历史和常用短语还原。
- 方便重装系统或迁移电脑后恢复常用数据。

## 🖼️ 界面预览

### 收缩仪表盘

收缩后仍保留时间、天气、硬件状态和下一项待办，适合长时间常驻桌面。

![UniDesk 收缩仪表盘](images/unidesk-compact-dashboard.png)

### 核心功能

![UniDesk 核心功能概览](images/unidesk-features.png)

### 模型雷达

两张决策卡先回答当前综合最高配置和 ModelDial 官方性价比推荐，再查看不同任务维度的 Top 5。

![UniDesk 模型雷达展示](images/unidesk-model-radar.png)

展示图使用 2026 年 8 月 30 日公开批次；应用内结果会随 ModelDial Radar 数据更新。

### 个性化设置

![UniDesk 个性化设置展示](images/unidesk-customization.png)

## 🚀 适合谁使用

UniDesk 适合希望桌面保持清爽，但又想快速查看信息、打开常用工具、记录待办和便签的 Windows 用户。

适合场景：

- 日常办公
- 个人效率管理
- 桌面快捷启动
- 系统状态查看
- 轻量待办与便签记录
- 常用文本快速复制

## 📦 安装与使用

从 [v2.1.0-rc.2 最新功能预览版](https://github.com/SuperDaddyV/UniDesk/releases/tag/v2.1.0-rc.2) 下载最新功能预览安装包。该未签名预览版包含「模型雷达」和最新模块默认状态，首次运行可能触发 Windows SmartScreen。当前稳定版仍请使用 [v2.0.0 稳定版（Latest）](https://github.com/SuperDaddyV/UniDesk/releases/latest)。RC2 安装包文件名为：

```powershell
UniDesk_Setup_2.1.0.exe
```

建议安装或升级前先退出正在运行的 UniDesk。

请直接双击安装包并在 Windows 提示时确认 UAC；标准用户需要输入管理员凭据，无需右键选择「以管理员身份运行」。主程序可以安装到 D／E 等任意本地磁盘的安全目录，覆盖安装会默认沿用上次位置；硬件服务、修复工具和卸载组件仍固定安装到 Windows 的 `Common Program Files\UniDesk` 受保护目录。安装器不接受网络路径、磁盘根目录、重解析路径或无法确认属于 UniDesk 的非空目录。

安装器默认勾选桌面快捷方式和「完整硬件监控组件」，并明确说明它会安装共享的 PawnIO 驱动和以 `LocalSystem` 运行的只读 Windows 服务；`UniDesk.exe` 本身始终保持普通用户权限。安装完成页默认勾选启动 UniDesk，并以安装前的普通用户身份自动启动。取消该组件或组件安装失败不会影响天气、便签、快捷方式等基础功能，但 CPU 温度等底层指标可能不可用，可稍后在设置中导出诊断并重试修复。

系统要求：

- 仍在 Microsoft 支持周期内的 Windows 11 x64
- Windows 10 Enterprise／IoT Enterprise LTSC 2021 或更新版本 x64

项目保留 Windows 10 1903 API 兼容基线；LTSC 2019 低于该基线，已停止支持的普通 Windows 10 版本也不属于正式支持范围。

## 🛠️ 本地构建

环境要求：

- .NET 10 SDK
- 受支持的 Windows 11 或 Windows 10 LTSC 开发环境
- Visual Studio 2022、JetBrains Rider，或其他支持 .NET / WPF 的开发环境
- Inno Setup 6，仅制作安装包时需要

构建并运行：

```powershell
git clone https://github.com/SuperDaddyV/UniDesk.git
cd UniDesk

dotnet restore UniDesk.sln
dotnet build UniDesk.sln -c Release
dotnet run --project UniDesk\UniDesk.csproj
```

从干净工作区构建本地未签名候选包：

```powershell
.\scripts\Build-Release.ps1 -Version 2.1.0
```

脚本会将主程序、硬件服务和修复工具发布到全新的版本目录，再从这些确定输入编译安装包。公开发布制品必须通过 GitHub Actions 的 `Build and sign release candidate` 手动工作流交由 SignPath 签名，并通过 `Test-ReleaseReadiness.ps1`；该工作流不会自动创建 GitHub Release。

## 🧰 技术栈

| 技术 | 用途 |
| --- | --- |
| .NET 10 LTS | 应用运行框架 |
| WPF | Windows 桌面界面 |
| SQLite | 本地数据存储 |
| CommunityToolkit.Mvvm | 界面与数据绑定辅助 |
| LibreHardwareMonitorLib | 硬件信息读取 |
| Hardcodet.NotifyIcon.Wpf | 系统托盘 |
| Inno Setup | Windows 安装包 |

## 🔐 数据与隐私

UniDesk 优先使用本地存储，用户数据保存在本机。当前主要数据包括设置、快捷方式、待办事项、快速便签、快捷文本和图标缓存等。

剪贴板历史功能带有敏感内容过滤，用于尽量避免保存验证码、密码、Token、Cookie 等敏感文本。该过滤用于降低误存风险，但不应被视为绝对安全保证；如果处理高敏感内容，建议关闭剪贴板历史或及时清理记录。

## Code signing policy

公开发布包遵循[代码签名政策](CODE_SIGNING_POLICY.md)和[隐私政策](PRIVACY.md)。签名候选包必须来自公开提交、通过人工批准，并完成源码版本、Authenticode 和 SHA-256 校验。

Free code signing provided by SignPath.io, certificate by SignPath Foundation.

## 🆕 更新亮点

当前版本已补充以下能力：

- 新增模型雷达：展示综合最高、官方性价比推荐及综合、后端、前端、知识四类 Top 5。
- 新增模块管理：支持模块显示 / 隐藏和排序。
- 新增快捷方式拖拽添加与自由排序。
- 新增快速便签：支持多条便签、自动保存、置顶和复制。
- 新增快捷文本：支持剪贴板历史、常用短语和敏感内容过滤。
- 优化硬件监视布局，完整显示 CPU、内存、GPU、温度和 RX / TX 网速。
- 优化 GPU 温度读取，尽量兼容更多硬件与驱动环境。
- 优化个性化设置和主面板滚动体验。

## 📌 后续计划

- 更多主题预设。
- 更完善的硬件详情展示。
- 更灵活的模块扩展能力。
- 更好的安装与更新体验。

## 🙏 致谢

UniDesk 基于 [Happyeveryweek/LumiDesk](https://github.com/Happyeveryweek/LumiDesk) 开发。感谢原作者提供的创意、基础代码和桌面小工具体验。

## 📄 License

本项目遵循仓库中的 [MIT License](LICENSE)。自包含安装包实际分发的第三方依赖、许可文本和来源见 [Third-party notices](THIRD-PARTY-NOTICES.md)。
