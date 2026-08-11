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
| I-05 | PawnIO 已存在、正在运行且硬件服务健康后覆盖安装 | 不重复运行 PawnIO 安装器；UniDesk 服务被修复并启动；安装不报退出码 13 |
| I-06 | PawnIO 已注册但停止、损坏或首次启动后硬件服务仍不可用 | 安装器先尝试启动；仍不可用时只执行一次签名与哈希验证后的 PawnIO 修复，再重启并复查 UniDesk 服务；最终仍不可用时进入兼容模式，不循环安装、不阻止基础应用 |
| I-07 | 取消完整硬件组件后安装 | 基础应用安装成功；天气、搜索、剪贴板、便签、快捷方式正常；底层温度允许显示 `--`；任务说明明确系统受保护目录仍会保留约 220 MB 离线修复文件 |
| I-08 | 使用安全软件或策略阻止硬件修复工具／服务 | 显示带退出码和日志路径的非致命警告，不出现 Inno Setup Runtime error；基础应用仍可启动 |
| I-09 | 安装后从快捷方式启动并检查进程 | `UniDesk.exe` 不带管理员权限；修复组件时只有 `UniDesk.HardwareRepair.exe` 请求 UAC |
| I-10 | 停止或删除 `UniDeskHardwareService` 后运行主程序 | 主程序不崩溃；普通权限指标继续回退读取；设置中显示可诊断状态并可修复 |
| I-11 | 制造服务协议不匹配或健康检查失败 | 主程序不退出、不阻塞；诊断明确显示协议或服务故障 |
| I-12 | 在无兼容温度传感器的 x64 电脑运行 | CPU 温度显示 `--` 并可导出诊断，其他可用指标继续显示，不宣称安装失败 |
| I-13 | 安装、覆盖安装或修复完整硬件组件后检查 `UniDeskHardwareService` 的 `PathName`／`ImagePath` | 完整可执行文件路径由双引号包围，例如 `"C:\Program Files\Common Files\UniDesk\HardwareService\UniDesk.HardwareService.exe"`；服务以 `LocalSystem` 正常启动，路径不随主程序安装盘变化 |
| I-14 | 清除旧用户数据后全新安装并首次启动 | 设置中的开机自启和自动定位均默认勾选；开机启动项实际存在；定位不可用时明确提示 Windows 权限问题，不误报网络或 API 错误 |
| I-15 | 分别在系统 `{commoncf}` 或其直属 Program Files 父目录赋予普通用户可删除子项／改 ACL 的有效权限后启动安装；另保持这两级安全但把主程序选择到普通 D／E 盘目录 | 前两种场景均在复制系统组件和创建卸载器前明确拒绝，且不修改父目录 ACL；后一场景允许继续，主程序安装到所选盘，系统组件仍位于 `{commoncf}\UniDesk`；盘符根目录的宽松但不向下生效权限不单独导致误拒绝 |
| I-16 | 全新安装时选择 `D:\Apps\UniDesk` 等尚不存在或为空的本地目录，完成后再次覆盖安装 | 首次安装保留所选位置；覆盖安装默认沿用该位置并允许直接继续，用户数据保持不变 |
| I-17 | 分别选择 UNC／映射网络盘、可移动盘、FAT／exFAT 盘、磁盘根目录、经过 junction／符号链接的路径、含同名假 `UniDesk.exe` 但未登记的非空目录 | 安装器在复制文件前停留在目录选择页并给出对应提示；主程序只允许安装到支持持久 ACL 的本地固定 NTFS／ReFS 盘，不覆盖未知目录、不递归修改主程序目录 ACL |
| U-01 | 从旧版覆盖安装 2.1.0，且安装前已主动关闭开机自启或自动定位 | 原设置、待办、便签、快捷方式、剪贴板历史和天气配置保留；两个已关闭选项不会被重新开启 |
| U-02 | 已安装 2.1.0 后再次覆盖安装 | 两个安装任务重新默认勾选；硬件组件幂等修复；无重复服务和驱动错误 |
| U-03 | 同 AppId 旧版安装在 D／E 盘自定义目录，存在严格 owned 的旧目录硬件服务；不修改安装位置，直接覆盖 | 新安装器先锁定并收紧旧目录、禁用并确认停止旧服务，再安装新版并从 `{commoncf}\UniDesk` 注册新版服务；只有新卸载器已经创建后，才逐个删除注册路径内严格匹配的旧 `uninsNNN.exe／.dat／.msg`；不执行旧卸载器、不删除用户数据、不产生两个服务 |
| U-04 | 同 AppId 旧版位于自定义目录，但本次选择新的本地目录 | 新安装器完成同样的系统组件迁移；新卸载器创建后才清理严格匹配的旧卸载文件，并只清理严格指向旧目录且文件名匹配的 Run／计划任务；不执行旧卸载器、不整体删除旧目录，完成后提示确认新版后手动删除旧程序文件夹 |
| U-05 | 篡改同 AppId 的 `UninstallString` 使其指向旧目录外或使用非 `uninsNNN.exe` 名称 | 安装在复制新版文件和停止服务前中止；不执行、不删除外部路径，不用通配符扩大删除范围 |
| U-06 | 让已确认属于旧目录的任一旧卸载文件在安装完成后的清理阶段无法删除 | 新版及其卸载器保持可用；安装器给出带 `{log}` 路径的非致命警告，不把已完成安装回滚成不可卸载状态，也不执行旧卸载器 |
| R-01 | 普通权限主程序中点击「修复硬件监控组件」并批准 UAC | 只从系统 `Common Program Files\UniDesk\HardwareRepair` 启动本地维护工具，修复完成后状态刷新；不从可选主程序目录加载提权程序，也不要求寻找旧安装包 |
| R-02 | 修复时取消 UAC | 显示已取消，不崩溃、不改变主程序权限 |
| X-01 | 卸载 UniDesk，选择保留 PawnIO | `UniDeskHardwareService` 被删除，PawnIO 保留，用户 `%LOCALAPPDATA%\UniDesk` 数据保留 |
| X-02 | 卸载 UniDesk，并明确选择移除 PawnIO | UniDesk 服务先删除；PawnIO 移除失败时给出明确警告，不误删用户数据 |
| X-03 | 模拟 `sc stop` 已接受但服务进程持续运行后卸载或覆盖安装 | 安装器／维护工具按服务名过滤终止进程并轮询真实停止状态；未确认停止时不得报告删除成功或覆盖文件，必须返回稳定失败码并显示警告 |
| X-04 | 在 `{app}` 已缺失、为空或仍含用户自行放入文件三种状态下运行卸载 | 卸载器不为缺失目录重新建空文件夹；释放目录锁后只移除空且非重解析的 `{app}`，存在其它文件时保留目录且不扩大删除范围 |
| A-01 | Windows ARM64 或 x86 上启动安装包 | 安装器在复制文件前明确拒绝不支持的架构 |
| A-02 | 低于 Windows `10.0.18362` 的 x64 系统上启动安装包，包括 Windows 10 Enterprise／IoT Enterprise LTSC 2019 | 安装器在复制文件前明确拒绝不支持的系统版本；支持范围明确为 Windows 10 Enterprise／IoT Enterprise LTSC 2021 及更新受支持 LTSC |
| UI-01 | 打开「桌面体验」，分别选择 20／50／100／200 后切换设置页面并重新打开 | 当前最大保存量始终保持强调色边框、轻强调背景和半粗字体，且只有一个选项被选中 |
| UI-02 | 收缩主面板并使用搜索、更多、展开 | 面板显示单层迷你仪表盘；顶部仅有搜索、更多、展开；更多菜单可操作置顶、锁定、设置和最小化 |
| UI-03 | 收缩态分别测试有待办和无待办 | 中部以无背景框摘要紧凑显示 CPU 使用率、内存使用率、CPU 温度和 GPU 温度；四行标签字号／字重一致、数值在统一 3px 间距后左对齐；天气内容限制在右侧列内，城市或来源过长时省略；底部只显示下一项待办或无待办状态，文本不重叠、不溢出 |
| UI-04 | 先开启自动定位，再在城市框中输入有效城市；随后重新开启自动定位 | 输入城市时自动定位立即取消勾选；重新开启自动定位时城市立即清空；保存后按当前定位模式刷新天气 |
| UI-05 | 查看城市输入说明，分别填写“北京”“浦东”“Tokyo”和仅国家名 | 设置页直接提示城市／区县和国外具体城市名格式；前三种可按接口结果查询，仅国家名不被描述为有效城市格式 |
| UI-06 | 在展开态和收缩态查看天气区域并点击来源署名 | 两种状态均显示可见的和风天气／QWeather 来源；点击后使用默认浏览器打开 `https://www.qweather.com`，主程序不提权、不崩溃 |
| UI-07 | 在常规设置点击「打开 Windows 位置设置」 | 使用 `ms-settings:privacy-location` 打开 Windows 位置设置；打开失败时显示本地化提示，主程序不崩溃 |
| UI-08 | 全新安装后打开「桌面体验」中的剪贴板设置 | 剪贴板历史默认勾选，页面直接说明历史仅保存在本机、可关闭，并指明可在「数据与备份」中清理 |
| UI-09 | 在模块管理中禁用「时间天气」并把任意其它模块移到首位，再收缩主面板 | 收缩态仍固定显示时间、天气、硬件摘要和下一待办；展开态继续遵循用户的启用状态与排序 |
| UI-10 | 在 `1366×768`、Windows 缩放 `150%` 环境打开展开主面板和设置窗口，并逐页键盘操作 | 两个透明窗口均完全位于可用工作区；设置底部取消／保存按钮始终可见可达，内容可滚动，焦点不被裁切 |
| UI-11 | 在 `100%` 与 `150%／200%` 的混合 DPI 双屏之间移动主窗口和设置窗口，再断开窗口所在的副屏 | 窗口按 `PerMonitorV2` 重新布局，文字和图标保持清晰，不出现尺寸跳变、裁切或透明区域错位；断屏后窗口回到可见工作区且操作可达 |
| L-01 | 清除本地用户数据，分别选择中文／English／日本語／Español 安装并保持完成页自动启动 | 四次首次启动均直接使用安装器所选语言，不闪回中文；后续手动选择的语言在覆盖安装后保持不变 |
| L-02 | 不通过安装器参数启动全新用户数据，分别模拟中／英／日／西 Windows UI 语言 | 首次语言映射为 `zh-CN`／`en-US`／`ja-JP`／`es-ES`；无法映射的系统语言回退 `zh-CN` |
| L-03 | 在英文、日文和西班牙文界面填写错误的 QWeather Key／Host 并保存 | 只显示当前界面的本地化验证提示，不直接出现网络层内部中文错误 |
| LIC-01 | 安装候选包后检查 `{app}\licenses` 并与 NuGet 锁定版本／自包含 publish 清单核对 | 包含 UniDesk MIT、.NET／WindowsDesktop Runtime、全部直接 NuGet 依赖、LibreHardwareMonitor 传递依赖、QWeather Icons、PawnIO GPL 与上游例外文本；notices 中版本和来源准确 |
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
- [x] 自动回归验证安装器最低版本、签名工作流完整 SHA、剪贴板披露、备份上限、收缩态排版、可选本地主程序目录、固定受保护系统组件、重解析路径拒绝、旧服务受信迁移和服务实质停止约束。
- [x] 自动回归验证收缩态不受模块排序／禁用影响、首次语言解析、`PerMonitorV2` 清单、低逻辑分辨率窗口上限、非中文天气验证提示及完整许可载荷。
- [x] 自动回归验证五 job 签名 Runner 隔离、artifact-id 传递、完整文件／目录清单、reparse point 拒绝、非签名 companion 哈希、PE／安装器 Authenticode 规范化内容哈希和干净源码提交绑定。
- [ ] 上表适用场景全部记录实际结果；未执行的场景不得标记通过。

## 2026-08-11 安装兼容修订记录

- 主程序安装位置恢复为可选择的本地固定 NTFS／ReFS 安全目录；UNC、映射网络盘、可移动盘和不支持持久 ACL 的文件系统会在复制前被拒绝。提权维护工具、硬件服务、PawnIO 安装包和卸载器固定到 `{commoncf}\UniDesk`。自动回归禁止误用会展开为 Program Files 根的 `{commonpf}`。
- 覆盖旧版时，安装器不会运行旧目录中的卸载器；只在新版卸载器已经创建，且同 AppId 注册路径、目录锁和 ACL 校验均通过后，逐个删除精确匹配的旧 `uninsNNN.exe／.dat／.msg`。旧卸载文件清理失败只给出带安装日志路径的警告，不破坏新版卸载能力。
- Windows PowerShell 5.1 硬件包签名校验会隔离继承的 PowerShell 7 模块路径；已注册但不可用的 PawnIO 只进行一次验证后的修复与复查，避免假失败和循环安装。
- 安装／硬件／启动项回归已纳入全量测试；全量 Release 测试 `503/503` 通过；Release 构建 `0` 警告、`0` 错误；Inno Setup 6.7.3 实际编译通过。
- 人工覆盖测试确认 `3d3f1bc` 候选会因 Windows PowerShell 5.1 继承 PowerShell 7 模块路径而在 `Get-Acl` 自动加载阶段误拒绝正常 `Common Program Files`；该候选判定失败并废弃。预检现直接使用 .NET ACL API，自动回归锁定安装脚本不得再调用 `Get-Acl`。
- 人工覆盖测试确认 `4c1fe3d` 候选误用了 Inno `{commonpf}`；该常量实际展开为 `C:\Program Files`，导致父级 ACL 检查错误扫描 `C:\` 并返回 `22`，且与主程序使用的 .NET `CommonProgramFiles` 路径不一致。安装器现统一使用展开为 `C:\Program Files\Common Files` 的 `{commoncf}`，自动回归禁止再次使用错误常量；错误消息通过 `FmtMessage` 注入真实 `{log}` 路径，不再显示字面量。
- 人工覆盖测试确认 `43c8f6e` 候选的 `{commoncf}\UniDesk` 实际 ACL 已正确收紧为普通用户只读执行，但维护工具的危险权限位掩码包含 `FullControl`／`Modify`／`Write` 复合枚举，读取位也会发生交集，从而把 `BUILTIN\Users: ReadAndExecute` 误判为可写并返回 `26`。权限回归现分别锁定只读权限必须通过，写入、创建、追加、删除、改 ACL 和取得所有权必须拒绝；该候选已废弃。
- 人工覆盖测试确认 `97913bc` 候选安装完成且无组件警告：D 盘主程序以非提升权限运行，Common Files 中的服务载荷 ACL 受保护，`UniDeskHardwareService` 以 `LocalSystem／Automatic／Running` 运行并使用完整引号路径，硬件维护最终返回 `0 (Success)`，原有数据库和开机启动路径保留。
- 版本一致性、NuGet 直接与传递依赖漏洞、PowerShell AST 和 `git diff --check` 均通过。该未签名候选只用于人工验收；正式安装包必须在最终 `main` 提交上由签名流水线重新生成。

## 2026-08-09 对抗式终审记录

- 锁定 RID 还原通过；Release 构建 `0` 警告、`0` 错误；全量测试 `481/481` 通过、`0` 跳过。
- 版本一致性、NuGet 直接与传递依赖漏洞、PowerShell AST、`git diff --check` 均通过；最终只读交叉审查未发现剩余 P0／P1。
- 新未签名验证包：`artifacts\release\2.1.0-2a7356fe1b2e-20260809-122412\installer\UniDesk_Setup_2.1.0.exe`，SHA-256 `8FF238E0189DD4111244BE39CE88B482B978C496E42E6A28F774468387B298A3`，`isDirty=true`、`NotSigned`，不得公开发布。
- 当前电脑的旧主程序和旧 `LocalSystem` 服务位于弱 ACL 的 `D:\Program Files\UniDesk`。新版不再从该目录运行服务或卸载器：覆盖安装应沿用 D 盘主程序位置，同时把系统组件迁移到 `{commoncf}\UniDesk`。不得运行旧目录中的提权卸载器；签名候选仍需按 U-03 实装确认迁移结果。
- `dotnet format --verify-no-changes` 暴露仓库既有空格格式债务（包括本轮未修改文件），它不是当前 CI／项目发布门禁；本轮没有为了绿灯全仓格式化无关代码。

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
