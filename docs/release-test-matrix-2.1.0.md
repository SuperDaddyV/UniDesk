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
| I-02 | Windows 11 x64 标准账户，正常双击并输入另一管理员账户凭据 | 安装完成后默认自动启动 UniDesk；主程序属于安装前的标准用户并保持普通权限；当前标准用户的开机自启已启用 |
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
| I-14 | 清除旧用户数据后全新安装并首次启动 | 设置中的开机自启和自动定位均默认勾选；开机启动项实际存在；定位不可用时明确提示 Windows 权限问题，不误报网络或 API 错误 |
| U-01 | 从旧版覆盖安装 2.1.0，且安装前已主动关闭开机自启或自动定位 | 原设置、待办、便签、快捷方式、剪贴板历史和天气配置保留；两个已关闭选项不会被重新开启 |
| U-02 | 已安装 2.1.0 后再次覆盖安装 | 两个安装任务重新默认勾选；硬件组件幂等修复；无重复服务和驱动错误 |
| R-01 | 普通权限主程序中点击「修复硬件监控组件」并批准 UAC | 本地维护工具启动，修复完成后状态刷新；不要求寻找旧安装包 |
| R-02 | 修复时取消 UAC | 显示已取消，不崩溃、不改变主程序权限 |
| X-01 | 卸载 UniDesk，选择保留 PawnIO | `UniDeskHardwareService` 被删除，PawnIO 保留，用户 `%LOCALAPPDATA%\UniDesk` 数据保留 |
| X-02 | 卸载 UniDesk，并明确选择移除 PawnIO | UniDesk 服务先删除；PawnIO 移除失败时给出明确警告，不误删用户数据 |
| A-01 | Windows ARM64 或 x86 上启动安装包 | 安装器在复制文件前明确拒绝不支持的架构 |
| A-02 | 低于 Windows `10.0.18362` 的 x64 系统上启动安装包 | 安装器在复制文件前明确拒绝不支持的系统版本 |
| UI-01 | 打开「桌面体验」，分别选择 20／50／100／200 后切换设置页面并重新打开 | 当前最大保存量始终保持强调色边框、轻强调背景和半粗字体，且只有一个选项被选中 |
| UI-02 | 收缩主面板并使用搜索、更多、展开 | 面板显示单层迷你仪表盘；顶部仅有搜索、更多、展开；更多菜单可操作置顶、锁定、设置和最小化 |
| UI-03 | 收缩态分别测试有待办和无待办 | 中部以无背景框摘要紧凑显示 CPU 使用率、内存使用率、CPU 温度和 GPU 温度；四行标签字号／字重一致、数值在统一 3px 间距后左对齐；天气内容限制在右侧列内，城市或来源过长时省略；底部只显示下一项待办或无待办状态，文本不重叠、不溢出 |
| UI-04 | 先开启自动定位，再在城市框中输入有效城市；随后重新开启自动定位 | 输入城市时自动定位立即取消勾选；重新开启自动定位时城市立即清空；保存后按当前定位模式刷新天气 |
| UI-05 | 查看城市输入说明，分别填写“北京”“浦东”“Tokyo”和仅国家名 | 设置页直接提示城市／区县和国外具体城市名格式；前三种可按接口结果查询，仅国家名不被描述为有效城市格式 |
| UI-06 | 在展开态和收缩态查看天气区域并点击来源署名 | 两种状态均显示可见的和风天气／QWeather 来源；点击后使用默认浏览器打开 `https://www.qweather.com`，主程序不提权、不崩溃 |
| UI-07 | 在常规设置点击「打开 Windows 位置设置」 | 使用 `ms-settings:privacy-location` 打开 Windows 位置设置；打开失败时显示本地化提示，主程序不崩溃 |
| UI-08 | 全新安装后打开「桌面体验」中的剪贴板设置 | 剪贴板历史默认勾选，页面直接说明历史仅保存在本机、可关闭，并指明可在「数据与备份」中清理 |
| D-01 | 导入超过 25 MiB、任一分区超过 10000 条或字段超长的备份 | 在生成预览和写入数据库前明确拒绝；原设置和业务数据保持不变 |

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
- [x] 自动回归验证全新数据库默认写入 `Startup=true` 和 `AutoLocation=true`，再次初始化不会覆盖既有选择。
- [x] 自动回归验证安装器最低版本、签名工作流完整 SHA、剪贴板披露、备份上限和收缩态排版约束。
- [ ] 上表适用场景全部记录实际结果；未执行的场景不得标记通过。

## 2026-07-28 自动审核记录

- 全量测试：`359/359` 通过，`0` 失败，`0` 跳过。
- Release 构建：`0` 警告，`0` 错误。
- 线程回归测试连续运行 `10/10` 通过。
- 服务注册参数测试覆盖 `sc.exe create` 和 `config`，两者的 `binPath=` 均要求完整双引号路径。
- PowerShell 脚本语法、GitHub Actions YAML、版本一致性、NuGet 传递依赖漏洞和 `git diff --check` 均通过。
- 普通 CI 与签名工作流均使用 Node.js 24 运行时的 `actions/checkout@v6` 和 `actions/setup-dotnet@v5`，避免已弃用的 Node.js 20 Action 警告。
- `scripts/Build-Release.ps1` 已从当前源码完成一次独立端到端发布和 Inno Setup 编译；验证制品位于忽略的 `artifacts` 目录，因工作区未提交且制品未签名，不得公开发布。
- 用户已在当前 Windows 11 设备确认剪贴板搜索、CPU／GPU／内存／网络读取、覆盖安装数据保留和最终 UI 交互；只读系统检查进一步确认 `UniDesk.exe` 为 Medium Integrity，`UniDeskHardwareService` 以 `LocalSystem`／Automatic 运行且 `PathName` 已由完整双引号包围，因此当前未签名安装的 I-09 和 I-13 通过。
- 待完成：SignPath Foundation 审批与仓库变量配置、干净提交上的签名工作流、签名制品重新安装后的 I-09／I-13 复核，以及尚未覆盖的标准账户／Windows 10／取消 UAC／安全软件拦截／卸载场景。

## 2026-08-01 默认体验回归记录

- 全量测试：`362/362` 通过，`0` 失败，`0` 跳过。
- Release 构建：`0` 警告，`0` 错误。
- 新增真实临时 SQLite 数据库测试，确认全新数据库默认写入 `Startup=true` 和 `AutoLocation=true`，再次初始化保留用户改为 `false` 的既有选择。
- 保留 `AutoLocation` 缺失时不请求 Windows 位置权限的回归测试，避免旧数据缺项或损坏时隐式开启定位。
- 设置页提供 `ms-settings:privacy-location` 入口；安装器未增加定位权限页面或额外确认弹窗，现有硬件驱动／系统服务披露保持不变。

## 2026-08-02 发布前终审修订记录

- 全量测试：`365/365` 通过，`0` 失败，`0` 跳过；Release 构建 `0` 警告、`0` 错误。
- 版本一致性、NuGet 直接与传递依赖漏洞审计、`git diff --check` 均通过。
- 安装器 `MinVersion=10.0.18362` 已由 Inno Setup 6.7.3 完整编译验证；四个签名工作流 Action SHA 与各自官方仓库版本标签当前指向逐项一致。
- 未签名测试安装包：`artifacts\release\2.1.0-compact-layout-test-20260802-153336\installer\UniDesk_Setup_2.1.0.exe`，SHA-256 `F29A10DDE9343F7AD1BA068F4EF211C1C47A2826C3F599B154A56C4287BA057D`；源清单 `isDirty=true`，不得公开发布。
- 收缩态紧凑间距、天气边界和剪贴板说明仍须由用户在该测试包上完成视觉／交互复核；低版本 Windows 拒绝场景仍须在适用测试环境执行。
