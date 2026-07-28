# UniDesk 2.1.0 最终发布人工测试矩阵

## 测试制品

- 安装包：`UniDesk_Setup_2.1.0.exe`
- 支持架构：Windows x64
- 不支持：Windows ARM64、Windows x86
- 测试版允许未签名；公开发布制品必须通过 `scripts/Test-ReleaseReadiness.ps1` 的全部 Authenticode 和哈希检查

每轮记录 Windows 版本、账户类型、安装包 SHA-256、是否已有 PawnIO、实际结果和 `ProgramData\UniDesk\logs\hardware-repair.log`。

## 必须通过

| 编号 | 环境与操作 | 预期结果 |
| --- | --- | --- |
| I-01 | Windows 11 x64 管理员账户，正常双击安装包 | 自动弹出一次 UAC；桌面快捷方式和完整硬件监控组件默认勾选 |
| I-02 | Windows 11 x64 标准账户，正常双击并输入另一管理员账户凭据 | 安装完成后默认自动启动 UniDesk；主程序属于安装前的标准用户并保持普通权限 |
| I-03 | 安装时取消 UAC | 安装安全取消，不产生可运行的部分安装 |
| I-04 | 保持完整硬件组件勾选，电脑没有 PawnIO | PawnIO、`UniDeskHardwareService` 安装并启动；CPU、GPU、内存和网络数据显示；支持的机器显示 CPU 温度 |
| I-05 | PawnIO 已存在且正在运行后覆盖安装 | 不重复运行 PawnIO 安装器；UniDesk 服务被修复并启动；安装不报退出码 13 |
| I-06 | PawnIO 已存在但已停止后覆盖安装 | PawnIO 被启动；UniDesk 服务健康检查通过 |
| I-07 | 取消完整硬件组件后安装 | 基础应用安装成功；天气、搜索、剪贴板、便签、快捷方式正常；底层温度允许显示 `--` |
| I-08 | 使用安全软件或策略阻止硬件修复工具／服务 | 显示带退出码和日志路径的非致命警告，不出现 Inno Setup Runtime error；基础应用仍可启动 |
| I-09 | 安装后从快捷方式启动并检查进程 | `UniDesk.exe` 不带管理员权限；修复组件时只有 `UniDesk.HardwareRepair.exe` 请求 UAC |
| I-10 | 停止或删除 `UniDeskHardwareService` 后运行主程序 | 主程序不崩溃；普通权限指标继续回退读取；设置中显示可诊断状态并可修复 |
| I-11 | 制造服务协议不匹配或健康检查失败 | 主程序不退出、不阻塞；诊断明确显示协议或服务故障 |
| I-12 | 在无兼容温度传感器的 x64 电脑运行 | CPU 温度显示 `--` 并可导出诊断，其他可用指标继续显示，不宣称安装失败 |
| I-13 | 安装、覆盖安装或修复完整硬件组件后检查 `UniDeskHardwareService` 的 `PathName`／`ImagePath` | 完整可执行文件路径由双引号包围，例如 `"C:\Program Files\UniDesk\HardwareService\UniDesk.HardwareService.exe"`；服务以 `LocalSystem` 正常启动 |
| U-01 | 从旧版覆盖安装 2.1.0 | 原设置、待办、便签、快捷方式、剪贴板历史和天气配置保留 |
| U-02 | 已安装 2.1.0 后再次覆盖安装 | 两个安装任务重新默认勾选；硬件组件幂等修复；无重复服务和驱动错误 |
| R-01 | 普通权限主程序中点击「修复硬件监控组件」并批准 UAC | 本地维护工具启动，修复完成后状态刷新；不要求寻找旧安装包 |
| R-02 | 修复时取消 UAC | 显示已取消，不崩溃、不改变主程序权限 |
| X-01 | 卸载 UniDesk，选择保留 PawnIO | `UniDeskHardwareService` 被删除，PawnIO 保留，用户 `%LOCALAPPDATA%\UniDesk` 数据保留 |
| X-02 | 卸载 UniDesk，并明确选择移除 PawnIO | UniDesk 服务先删除；PawnIO 移除失败时给出明确警告，不误删用户数据 |
| A-01 | Windows ARM64 或 x86 上启动安装包 | 安装器在复制文件前明确拒绝不支持的架构 |
| UI-01 | 打开「桌面体验」，分别选择 20／50／100／200 后切换设置页面并重新打开 | 当前最大保存量始终保持强调色边框、轻强调背景和半粗字体，且只有一个选项被选中 |
| UI-02 | 收缩主面板并使用搜索、更多、展开 | 面板显示单层迷你仪表盘；顶部仅有搜索、更多、展开；更多菜单可操作置顶、锁定、设置和最小化 |
| UI-03 | 收缩态分别测试有待办和无待办 | 中部以无背景框摘要紧凑显示 CPU 使用率、内存使用率、CPU 温度和 GPU 温度；标签与数值间距自然；底部只显示下一项待办或无待办状态；文本不溢出 |
| UI-04 | 先开启自动定位，再在城市框中输入有效城市；随后重新开启自动定位 | 输入城市时自动定位立即取消勾选；重新开启自动定位时城市立即清空；保存后按当前定位模式刷新天气 |
| UI-05 | 查看城市输入说明，分别填写“北京”“浦东”“Tokyo”和仅国家名 | 设置页直接提示城市／区县和国外具体城市名格式；前三种可按接口结果查询，仅国家名不被描述为有效城市格式 |
| UI-06 | 在展开态和收缩态查看天气区域并点击来源署名 | 两种状态均显示可见的和风天气／QWeather 来源；点击后使用默认浏览器打开 `https://www.qweather.com`，主程序不提权、不崩溃 |

## 发布前门禁

- [x] `dotnet test UniDesk.sln -c Release --no-restore` 全部通过。
- [x] Release 构建零警告、零错误。
- [x] 主程序清单为 `asInvoker`，修复工具清单为 `requireAdministrator`。
- [ ] 安装包、所有 UniDesk 自有 EXE、承载一方托管代码的 DLL 和 PawnIO 的 Authenticode 状态均为 `Valid`。
- [x] `scripts/Test-VersionConsistency.ps1 -ExpectedVersion 2.1.0` 通过。
- [x] `scripts/Test-PackageVulnerabilities.ps1` 通过。
- [ ] `scripts/Test-ReleaseReadiness.ps1` 通过并记录全部 SHA-256。
- [ ] `release-source.json` 显示干净工作区、目标版本和准备发布的精确 Git 提交；`release-manifest.json` 与签名工作流制品一致。
- [ ] SignPath Foundation 项目、GitHub App、签名策略、两个 Artifact Configuration、Secret 和 Variables 已完成配置；仓库与日志中不存在令牌或私钥。
- [x] 五份 README 和发布说明均公开链接代码签名政策与隐私政策，并包含 SignPath Foundation 资助声明。
- [x] 自动回归验证展开态与收缩态共用的天气视图始终保留 QWeather 来源链接，四种界面语言均提供署名文本。
- [x] 自动回归验证 `AutoLocation` 缺失时按关闭处理，不请求 Windows 位置权限。
- [ ] 上表适用场景全部记录实际结果；未执行的场景不得标记通过。

## 2026-07-28 自动审核记录

- 全量测试：`358/358` 通过，`0` 失败，`0` 跳过。
- Release 构建：`0` 警告，`0` 错误。
- 线程回归测试连续运行 `10/10` 通过。
- 服务注册参数测试覆盖 `sc.exe create` 和 `config`，两者的 `binPath=` 均要求完整双引号路径。
- PowerShell 脚本语法、GitHub Actions YAML、版本一致性、NuGet 传递依赖漏洞和 `git diff --check` 均通过。
- `scripts/Build-Release.ps1` 已从当前源码完成一次独立端到端发布和 Inno Setup 编译；验证制品位于忽略的 `artifacts` 目录，因工作区未提交且制品未签名，不得公开发布。
- 用户已在当前 Windows 11 设备确认剪贴板搜索、CPU／GPU／内存／网络读取、覆盖安装数据保留和最终 UI 交互；只读系统检查进一步确认 `UniDesk.exe` 为 Medium Integrity，`UniDeskHardwareService` 以 `LocalSystem`／Automatic 运行且 `PathName` 已由完整双引号包围，因此当前未签名安装的 I-09 和 I-13 通过。
- 待完成：SignPath Foundation 审批与仓库变量配置、干净提交上的签名工作流、签名制品重新安装后的 I-09／I-13 复核，以及尚未覆盖的标准账户／Windows 10／取消 UAC／安全软件拦截／卸载场景。
