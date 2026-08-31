# UniDesk v2.1.0 最终发布审核

审核日期：2026-08-31（2026-08-11 安装兼容修订；2026-08-30 模型雷达候选与公开下载文档修订；2026-08-31 最终修复与本地工程验收）
审核范围：`C:\Users\Administrator\Documents\UniDesk` 当前工作区
审核结论：公开发布为 **NO-GO**。项目所有者已明确接受 `v2.1.0` 以 `Authenticode: NotSigned` 形式正式发布，并接受未执行人工矩阵的残余风险；但精确候选 `1a2428d` 在本机覆盖安装时错误拒绝已登记的 D 盘安装目录，必须修复、重新提交、重跑 CI、重建候选并完成针对性安装验收。既有 RC 和失败候选不得覆盖、改名或发布。

## 2026-08-31 精确候选覆盖安装阻断

- 从干净 `main` 提交 `1a2428d837628055a8fdc5ddcfa512965726b90a` 构建的未签名候选 `UniDesk_Setup_2.1.0.exe` 在“选择目标位置”页拒绝 `D:\Program Files\UniDesk`，因此该候选安装验收为 **FAIL**，尚未创建 `v2.1.0` tag 或 GitHub Release。
- 实机只读核验确认 HKLM 64 位同 AppId 的 `InstallLocation` 为 `D:\Program Files\UniDesk\`；目标位于本地固定 NTFS 卷，目录及 `D:\Program Files` 祖先均非重解析点，目标树内重解析点数量为 `0`，受保护组件标记也记录同一路径。
- 最小 Inno Setup 6.7.3 探针复现出根因：`FindFirst('D:\', FindRec)` 返回 `False／ERROR_FILE_NOT_FOUND`。新增的祖先 fail-closed 逻辑把这个盘符根 API 语义误判为“祖先不可访问”，导致所有位于非系统盘既有目录的覆盖安装在到达卷根时被拒绝。修订必须在已验证固定卷的盘符根停止祖先遍历，同时保留根以下枚举失败、访问失败和重解析点的 fail-closed 行为；目录页必须改用 `WizardDirValue` 校验当前可见路径。
- “上一步”未显示是因为默认禁用欢迎页后，目标位置页就是首个向导页；它不是本次覆盖失败的原因。当前修订不为制造一个无实际返回目标的按钮而增加欢迎页，先修复会阻断安装的路径校验。
- 修复路径校验后的预验证安装包已在本机从 `D:\Program Files\UniDesk` 成功进入下一页，随后因误点继续完成安装；安装本身成功，但首次启动提示“无法写入开机自启设置”。实机只读证据确认 HKCU Run 键可写，遗留值为 `UniDesk = "D:\Program Files (x86)\UniDesk\UniDesk.exe"`，而该目标文件和目录均已不存在。当前 `StartupService` 只允许覆盖严格位于当前安装目录的值，因而把可确认失效的本产品旧值误判为潜在同名冲突。正式候选还必须补充“仅替换可无歧义解析且目标明确不存在的精确 `UniDesk.exe` 值”的窄修复；目标存在、权限不明或命令不合法时继续 fail-closed。
- 独立安全复核在补充完全限定路径、值类型、祖先重解析点和写前复读后确认无 P0／P1。保留的 P2 是同一普通用户权限内的文件系统／Registry 瞬时竞态：Windows Registry 不提供这里可直接使用的原子比较写入，现实现以两次值快照和两次归属／缺失检查缩小窗口；它不提升权限、不修改旧目标文件，仍必须在精确候选上用本机遗留 Run 值完成动态迁移验证，不能仅凭单元测试宣称完全消除竞态。

## 2026-08-31 最终修复与验收

- 已修复备份恢复语义校验、安装器目录／祖先链／ACL 子项枚举 fail-open 及“已存在目标被误判为缺失”、硬件 IPC 异常帧耗尽接收循环、LibreHardwareMonitor AMD GPU 原生更新导致服务进程崩溃、硬件维护取消状态、设置同步数据库读取与同 key 写入顺序、天气刷新取消源竞态、待办完成状态读改写竞态、快捷方式图标原子写入／所有权清理、未签名门禁信任调用方清单，以及测试日志写入真实用户目录的问题。
- TDD 过程包含针对非法备份、四个异常 IPC 客户端、目录枚举错误、失效启动项迁移、设置写入顺序、天气迟到完成、原子待办更新、图标原子发布／内容所有权、修复进程取消语义、未签名门禁和日志隔离的回归测试；独立复核进一步锁定完全限定路径、已存在非字符串／空白 Run 值、祖先重解析点和写入前快照复核。当前工作区已使用仓库钉住的 SDK `10.0.302` 完成 `595/595` 通过，`0` 跳过，GitHub CI 仍须在最新正式提交上重新验证。五个关键并发／竞态用例此前连续运行 `10/10` 轮，每轮 `5/5` 通过。
- 使用仓库钉住的 .NET SDK `10.0.302` 完成锁定还原、Release 构建和依赖漏洞查询：构建 `0` 警告、`0` 错误，未发现 NuGet 直接或传递依赖已知漏洞；版本一致性、PowerShell AST 与 `git diff --check` 通过。
- `dotnet format --verify-no-changes` 仍因仓库既有空格／换行格式债务失败，首批结果包含本轮未修改的 `AssemblyInfo.cs` 和 `NoteService.cs`；它不是当前 CI 或项目发布门禁，本轮未批量格式化无关代码。
- 当前脏工作区完成了含载荷指纹的端到端预验证出包：`artifacts\prevalidation\v2.1.0-final-fingerprint-20260831-014010\installer-split\UniDesk_Setup_2.1.0.exe`，大小 `124565644` 字节，SHA-256 `F4B7791F94A54CA55F16AEB1345C856938E4C489659514D20104550C72CB6BE5`，产品版本 `2.1.0`，安装包和八个一方 PE 均为 `Authenticode: NotSigned`，载荷 PDB 数量为 `0`。Inno Setup 对单个版本字段存在 64 字符上限，首次实编译暴露完整哈希被截断；修订后将 `release-source.json` 的 64 位 SHA-256 拆分到 `FileDescription` 与 `LegalCopyright` 两个 32 位片段，去除版本资源尾部填充后可精确重组为 `f62e4ddf594e446e66def2eacbf7985fa3afd5c51c5611ff8d67cea907238c47`。该载荷源清单仍为 schema `3`、SDK `10.0.302`、提交 `4e090edcfae04f1c25b9197738658212ed245fd6`、`isDirty=true`；正式门禁按预期拒绝，故该包仅证明完整构建与指纹链可用，不是正式候选。
- 新增 `scripts/Test-UnsignedReleaseReadiness.ps1`，只接受精确版本 `2.1.0`，并验证 40 位源码提交、严格布尔 `isDirty=false`、当前 `HEAD`、包含未跟踪文件的干净工作区、版本／SDK／`global.json`／锁文件、完整载荷、安装包绑定的源清单 SHA-256 指纹、无 PDB、安装器及全部一方 PE 为 `NotSigned`、PawnIO 固定哈希与上游有效签名；回归确认非布尔清单状态、非当前提交和脏／未跟踪工作区都会在进入制品信任前被拒绝。首次最终 `main` 出包还暴露门禁只识别斜杠、误把 `artifacts` 下诊断工程计入项目清单的问题；枚举现同时识别 Windows 反斜杠与规范化斜杠，并由回归锁定。
- GitHub 只读核验时，公开仓库 `main` 与本地基线同为 `4e090edcfae04f1c25b9197738658212ed245fd6`，该提交的 CI 运行 `33310598438` 成功；但它不包含本轮未提交修复。默认分支要求严格的 `build-and-test`、管理员同样受保护、禁止强推与删除并要求对话解决；开放 PR、Issue、Dependabot 告警和 Secret Scanning 告警均为 `0`。公开 Release 仍是 `v2.1.0-rc.2`／`v2.1.0-rc.1` 预发布和 `v2.0.0` 最新稳定版，尚无 `v2.1.0` 正式 Release。Code Scanning 返回“no analysis found”，且未发现仓库级 ruleset／tag protection，作为后续治理增强项记录；本轮没有修改 `.github` 或 GitHub 状态。
- Windows 事件日志曾记录 LibreHardwareMonitor AMD GPU 原生路径的服务进程崩溃。修订后的硬件服务明确跳过顶层及嵌套 `HardwareType.GpuAmd` 的 LibreHardwareMonitor `Update()`，保留 CPU、主板、NVIDIA 和 Intel 更新，并由主程序继续使用 AMD ADL／Windows GPU Engine；策略回归已锁定只隔离 AMD GPU。当前修订源码在受影响的 RX 9060 XT 设备上完成 `300/300` 次、`608` 秒连续采样，持续输出 `64` 项非 AMD LibreHardwareMonitor 传感器，AMD ADL 同时读取到使用率和温度，且对应事件日志时间窗没有新的 `AccessViolationException`；该证据仍是提交前源码级验收，最终精确安装候选仍须重新执行 `H-01`。
- 本机现有 `UniDeskHardwareService` 已重新处于 `LocalSystem／Automatic／Running`，`ImagePath` 为带双引号的 `C:\Program Files\Common Files\UniDesk\HardwareService\UniDesk.HardwareService.exe`；它仍是先前 RC2，而不是本轮最终候选，不能用该运行状态替代最终安装验收。

## 2026-08-30 未签名正式版修订

- 仅 `v2.1.0` 接受未签名正式发布；SignPath 工作流保留为后续首选能力，未运行或未配置不得表述为签名通过。
- 正式候选必须由新增的未签名发布就绪门禁确认全部一方 PE 与安装包为 `NotSigned`，同时验证精确干净源码提交、版本、锁文件、完整载荷、无 PDB 和 SHA-256。
- 发布说明必须明确 Windows SmartScreen／企业策略风险；用户最终确认前不得创建 `v2.1.0` tag 或 GitHub Release。
- 本轮终审新增的发布阻断为：备份恢复缺少完整语义校验、安装器目录枚举 fail-open、硬件 IPC 异常帧可耗尽接收循环和 LibreHardwareMonitor AMD GPU 原生更新导致服务进程崩溃。上述问题均已形成针对性修复与回归；AMD GPU 隔离、UI 线程数据库同步执行及请求取消竞态仍必须在最终精确候选验收中单独记录，不得被自动测试通过掩盖。

## 2026-08-30 模型雷达候选修订

- `2.1.0` 正式版范围新增默认关闭的模型雷达模块：运行时只访问固定 ModelDial HTTPS `latest.json`，展示官方综合第一与首个 `value` 推荐，提供四类 Top 5、缓存／离线／SchemaError 状态、六小时刷新上限、取消和迟到结果抑制；不调用模型、不抓取 HTML，也不修改模型工具配置。
- 全新安装默认启用时间天气、硬件监视、待办事项和快速便签，默认关闭快捷方式、快捷文本和模型雷达；升级继续保留已经保存的模块开关和顺序，旧布局只追加关闭的 `ModelRadar`。
- `v2.1.0-rc.1` 和先前 `97913bc` 未签名安装包均不包含上述修订，保持不可变历史证据，不得覆盖、改名或冒充新候选继续验收。
- `main` 已干净同步到 `ea7838a`，对应 CI 运行 `33274275192` 已通过；`ea7838a` 的本地安装包仅为发布前验证包，不能直接标记为 `v2.1.0-rc.2`、上传、签名或公开发布。
- 真正的 RC2 必须来自包含本轮公开下载文档的最终干净 `main`；源码、安装包、签名清单、版本、SHA-256 与人工测试记录必须重新绑定到该提交。
- 新增人工范围至少覆盖：默认关闭时零网络／零刷新、启用与关闭生命周期、缓存新鲜度和离线状态、SchemaError／Pending、四类排序与官方标签、固定外链，以及全新安装默认四模块和覆盖升级保持原布局。

`ea7838a` 本地发布前验证包：

- 路径：`artifacts\release\2.1.0-ea7838a920e9-20260830-044521\installer\UniDesk_Setup_2.1.0.exe`
- 大小：`124597572` 字节
- SHA-256：`3DB19794AB04BFAC96D892E1B7C614D8AAD10C8CCD9351B6860BF40C1FC73721`
- 状态：`NotSigned`；安装器产品版本 `2.1.0`；载荷无 PDB。
- `release-source.json`：schema `3`，源提交 `ea7838a920e9915b0638c8cffc4593da2e79c8f8`，`isDirty=false`，SDK `10.0.303`；载荷文件和目录清单及 SHA-256 已由 `Test-ReleasePayloadIntegrity.ps1` 复核一致。该包不代表最终 RC2，最终 RC2 必须在包含本轮文档的干净提交上重新生成。
- 本轮源码验证：Release 构建 `0` 警告、`0` 错误；全量测试 `550/550` 通过；版本一致性通过；使用 SDK `10.0.303` 执行与发布脚本相同的 NuGet 直接／传递依赖漏洞查询，未发现已知漏洞。
- 精确发布脚本 `Test-PackageVulnerabilities.ps1` 仍被本机缺少仓库钉住的 SDK `10.0.302` 阻塞；等价的 `10.0.303` 查询不能替代正式 RC2 的精确 SDK 门禁。该包仅供本机人工预验收，不得公开分发。

## 2026-08-11 安装兼容修订结论

- 主程序安装页重新允许选择本地固定 NTFS／ReFS 盘上的安全目录，覆盖安装默认沿用同一 AppId 的原位置；UNC、映射网络盘、可移动盘、FAT／exFAT、磁盘根目录、重解析路径和无法确认归属的非空目录在复制前被拒绝。
- 硬件服务、修复工具、PawnIO 安装包和 Inno 卸载器与主程序位置解耦，固定到系统 `{commoncf}\UniDesk`；Inno Setup 的 `{commonpf}` 实际表示 Program Files 根，不能用于 Common Files。安装器在复制或执行这些提权组件前验证 Common Files 及其直属 Program Files 父目录，并只收紧该固定组件目录，绝不递归修改用户选择的主程序目录，也不因盘符根目录的无关宽 ACL 误拒绝。
- 首次人工覆盖测试发现 Windows PowerShell 5.1 会继承 PowerShell 7 的模块路径，导致 `Get-Acl` 自动加载不兼容模块并把正常系统目录误报为不安全。ACL 预检已改为直接使用 .NET `DirectoryInfo.GetAccessControl`，不再依赖 PowerShell 模块；同一污染环境下旧命令返回 `21`，新命令返回 `0`。
- 硬件包 Authenticode 校验和服务停止确认在调用 Windows PowerShell 5.1 时会把 `PSModulePath` 固定到其自身模块目录，避免继承 PowerShell 7 模块路径后把有效 PawnIO 包误判为签名失败；已注册但仍不可用的 PawnIO 只执行一次验证后的修复、服务重启和健康复查，最终失败则进入兼容模式而不循环安装。
- 同 AppId 旧版位于自定义目录时，新安装器只从固定 HKLM 卸载注册读取旧路径；先锁定并收紧旧目录、退役旧目录中的 owned 服务并从固定组件目录注册新版服务；只有新版卸载器已经创建后，才删除严格匹配注册归属的旧 `uninsNNN.exe／.dat／.msg`，绝不执行旧卸载器或宽泛删除。旧卸载文件清理失败是带日志路径的非致命警告，新版仍可正常卸载。仅当主程序位置改变时清理 owned 启动项，不整体删除旧目录，用户数据继续保留。
- 服务安装、覆盖和卸载均验证 `ImagePath` 所有权，并以 `disable → stop → 按服务名 taskkill → 轮询真实停止 → delete` 收口；同名外部服务绝不接管。
- 设置保存改为单事务批次；备份使用同一数据库事务快照和原子文件替换；持久化失败不再伪装成功；恢复事务提交与后续 UI 刷新使用不同成功边界。
- 签名流水线拆成五个独立 GitHub-hosted job。签名前清单记录全部 `1431` 个文件、`20` 个目录、SHA-256、固定签名目标与 Authenticode 规范化内容哈希；文件／目录增删、reparse point、非签名文件变化、签名目标代码变化和安装器内容变化均被门禁拒绝。
- 主窗口和设置窗口按 Win32 物理像素选择真实显示器，选定后才转换 WPF DIP；已覆盖右侧 150% DPI、负坐标、断屏和非矩形排列。
- 许可证、第三方源码可得性、四语错误提示、首次语言、普通权限清单、天气网络错误和硬件陈旧快照均已复核。

本轮统一自动验证：

| 项目 | 结果 |
| --- | --- |
| `dotnet restore UniDesk.sln -r win-x64 --locked-mode` | 通过；锁文件已包含发布 RID |
| `dotnet build UniDesk.sln -c Release --no-restore -m:1` | 0 警告，0 错误 |
| `dotnet test UniDesk.sln -c Release --no-restore` | `503/503` 通过，0 跳过 |
| 版本一致性、NuGet 漏洞、PowerShell AST、`git diff --check` | 全部通过 |
| 五 job 签名工作流与固定 Action SHA 静态回归 | 通过 |
| 对抗式只读复核 | 未发现剩余 P0／P1 |
| `dotnet format --verify-no-changes` | 非项目门禁；因仓库既有空格格式债务未通过，未全仓格式化无关代码 |

先前未签名验证包（均已废弃）：

- 路径：`artifacts\release\2.1.0-2a7356fe1b2e-20260809-122412\installer\UniDesk_Setup_2.1.0.exe`
- 大小：`124567828` 字节
- SHA-256：`8FF238E0189DD4111244BE39CE88B482B978C496E42E6A28F774468387B298A3`
- 状态：`NotSigned`；`release-source.json` 为 schema 3、`isDirty=true`、源提交 `2a7356fe1b2ec00efbfe590af24910a1973f6b29`。它不包含本轮安装兼容修订，不得再用于测试或发布；新候选必须从本轮干净提交重新生成。
- `artifacts\release\2.1.0-3d3f1bc95fbd-20260811-121628\installer\UniDesk_Setup_2.1.0.exe` 包含分离安装布局，但仍受 Windows PowerShell 模块路径冲突影响，已由人工测试确认失败并废弃。
- `artifacts\release\2.1.0-4c1fe3d64280-20260811-130653\installer\UniDesk_Setup_2.1.0.exe` 误把 Inno `{commonpf}` 当成 Common Files；实际展开到 `C:\Program Files` 后错误检查卷根 ACL，人工覆盖测试返回 `22`，且错误消息显示字面量 `{log}`。该候选已废弃，不得继续测试或发布；修订版统一使用 `{commoncf}` 并通过 `FmtMessage` 注入实际日志路径。
- `artifacts\release\2.1.0-43c8f6e1a20d-20260811-132815\installer\UniDesk_Setup_2.1.0.exe` 已正确迁移到 `{commoncf}`，但维护工具把包含读取位的 `FullControl`／`Modify`／`Write` 复合枚举合并为危险权限掩码，导致实际只有 `ReadAndExecute` 的 `BUILTIN\Users` 仍被误判为可写并返回 `26`。该候选已废弃，不得继续测试或发布；修订版只按基础写入、创建、追加、删除、属性、ACL 与所有权权限位判定危险权限。

当前通过人工覆盖安装的未签名候选：

- 路径：`artifacts\release\2.1.0-97913bc55f3a-20260811-134023\installer\UniDesk_Setup_2.1.0.exe`
- 大小：`124586690` 字节
- SHA-256：`DEE09DCDCB00491D1398E84ECD7FEF8C6EA593E1A65B69CB3AF6D6BF389AC0CE`
- 状态：`NotSigned`；`release-source.json` 为干净源提交 `97913bc55f3adb8f669e5a91acbb224be9599c80`。该包仅用于最终人工验收，正式发布仍须由 GitHub 签名流水线从最终 `main` 重新生成。
- 实机结果：主程序继续位于 `D:\Program Files\UniDesk` 并以非提升权限运行；卸载器、修复工具与硬件服务迁移到 `C:\Program Files\Common Files\UniDesk`；`UniDeskHardwareService` 为 `LocalSystem／Automatic／Running`，`ImagePath` 完整加引号；组件根 ACL 为 `SYSTEM／Administrators: FullControl`、`Users: ReadAndExecute`；维护日志最终返回 `0 (Success)`。

## 已修复

### LocalSystem 服务路径

- 安装和修复工具此前向 `sc.exe create/config` 传入未加引号的服务路径，含空格安装目录会形成未加引号服务路径风险。
- 当前实现始终将完整硬件服务可执行文件路径包围在双引号中。
- 回归测试同时验证 `create` 和 `config`，避免覆盖安装或应用内修复重新写回不安全值。

### 测试稳定性

- 诊断导出测试不再用可被线程池复用破坏的 Managed Thread ID 比较。
- 新测试使用阻塞采集源直接证明调用线程会先返回、后台采集不会阻塞调用方。
- 该测试连续执行 `10/10` 通过。

### 可重复出包

- `Publish-ReleasePayload.ps1` 从精确 Git 提交发布主程序、硬件服务和修复工具，并生成 `release-source.json`。
- 正式载荷默认拒绝脏工作区；每次输出到全新目录，不再读取历史 `publish` 目录。
- `Build-ReleaseInstaller.ps1` 只接受显式载荷目录，验证版本后再将三个目录传给 Inno Setup。
- `Build-Release.ps1` 已完成一次端到端本地验证。

本轮本地验证安装包：

- 路径：`artifacts\release\2.1.0-compact-layout-test-20260802-153336\installer\UniDesk_Setup_2.1.0.exe`
- 大小：`124508035` 字节
- SHA-256：`F29A10DDE9343F7AD1BA068F4EF211C1C47A2826C3F599B154A56C4287BA057D`
- 状态：`NotSigned`、源清单 `isDirty=true`，仅用于构建验证，禁止发布。

### 可信签名

- 新增仅允许正式仓库 `main` 分支手动触发的 `release-signing.yml`。
- 工作流使用 SignPath 官方 GitHub Action，令牌来自 GitHub Secret，组织／项目／策略标识来自 GitHub Variables；所有 Action 固定到其官方仓库 tag 当前指向的完整 commit SHA，并保留版本注释。
- 第一阶段签署所有 UniDesk 自有 EXE 和实际承载托管代码的 DLL；第三方运行时不冒用 UniDesk 身份重新签名。
- 第二阶段使用已签名载荷编译安装包，再签最终安装包。
- `Test-ReleaseReadiness.ps1` 会拒绝脏源清单、提交不匹配、版本不匹配、任一签名无效或 PawnIO 固定哈希变化，并生成最终哈希清单。
- 签名工作流只生成候选制品，不创建 tag 或 GitHub Release。

### 天气隐私与来源署名

- `AutoLocation` 设置缺失或无法解析时一律按关闭处理，避免旧数据缺项或设置损坏时隐式请求 Windows 位置权限；新增回归测试覆盖该缺省路径。
- 全新用户数据库显式写入 `Startup=true` 与 `AutoLocation=true`；覆盖安装和升级不重写已有值，用户主动关闭的选项保持关闭。
- 隐私政策明确披露时间天气模块和全新安装自动定位默认启用、启动与约 30 分钟自动刷新、手动城市或自动定位坐标发送到和风天气，以及对应的关闭方式。
- 天气主界面新增指向和风天气官网的可见来源署名，展开态和收缩态共用同一视图且四种语言均有文本；外部链接打开失败时只记录本地日志，不导致应用退出。

### 文档与治理

- 项目规范目标框架已从过期的 `.NET 9` 更新为 `.NET 10`。
- 五份 README 已统一为同一个 Release 构建入口。
- 新增公开的 `CODE_SIGNING_POLICY.md` 和双语 `PRIVACY.md`，五份 README 与发布说明均包含 SignPath Foundation 资助声明和政策入口。
- 普通 CI 使用 Node.js 24 运行时的 `actions/checkout@v6` 和 `actions/setup-dotnet@v5`；含密钥的签名工作流固定到对应官方完整 commit SHA，并由回归测试同时约束运行时版本与不可变引用。
- 发布说明、人工矩阵和 SignPath 配置指南已同步，并新增 QWeather 来源署名人工检查项。
- 原 `UniDesk_Final_Release_Audit.md` 已明确标记为 `v1.3.3` 历史归档，不再代表当前版本。

### 发布前终审补强

- 安装器以 `MinVersion=10.0.18362` 对齐应用 Windows API 兼容基线，旧系统在复制文件前被拒绝。
- 全新安装默认开启剪贴板历史的事实、仅本机保存、关闭入口和清理入口已在设置页与双语隐私政策直接披露。
- 备份导入在读取前限制 25 MiB 文件大小，并在生成预览前限制分区条数和字段长度；超限不会触发数据库写入。
- 收缩态四行硬件指标使用统一的标签／数值两列排版，不再混用标签字号和字重。

## 验证结果

| 项目 | 结果 |
| --- | --- |
| `dotnet test UniDesk.sln -c Release --no-restore -m:1` | `365/365` 通过 |
| `dotnet build UniDesk.sln -c Release --no-restore` | 0 警告，0 错误 |
| 线程回归测试重复执行 | `10/10` 通过 |
| 版本一致性 | 通过，`2.1.0` |
| NuGet 直接与传递依赖漏洞 | 未发现 |
| PowerShell 脚本语法 | 通过 |
| GitHub Actions YAML | 通过 |
| 签名工作流 Action SHA 与官方 tag 指向 | 4 项逐项一致 |
| 全新目录端到端出包 | 通过 |
| 未签名载荷进入正式安装包阶段 | 被门禁正确拒绝 |
| 脏源清单进入发布就绪阶段 | 被门禁正确拒绝 |

## 外部发布门禁

以下事项不能由本地代码伪造为完成：

1. 当前全部修复形成干净提交并推送；新的 GitHub CI 必须在该精确提交上通过。
2. 从该提交重新生成未签名候选，取得 `Authenticode=NotSigned`、精确源码清单和匹配的 SHA-256 校验清单。
3. 运行未签名发布就绪门禁，确认版本、依赖、完整载荷、无 PDB、源码提交和全部一方 PE 的未签名状态。
4. 安装该精确候选并检查 `UniDeskHardwareService` 注册路径带双引号，完成剩余人工矩阵和本轮新增缺陷的针对性验收。
5. 用户最终确认后，才能创建 `v2.1.0` tag 和 GitHub Release；Release 必须明确披露未签名与 SmartScreen／企业策略风险。

当前 Windows 11 电脑已用 `97913bc` 候选完成上述安全迁移：弱 ACL 的 D 盘目录只承载普通权限主程序，`LocalSystem` 服务和提权维护载荷已固定到受保护的 `{commoncf}\UniDesk`，原有用户数据和开机启动路径得到保留。该结果确认当前机器的 I-09／I-13 覆盖安装路径通过，但不能替代 Windows 10 LTSC、标准账户、取消 UAC、安全软件拦截和卸载场景的独立人工门禁。

除上述发布门禁外，本轮没有发现需要在 `v2.1.0` 增加的新功能或升级的大版本依赖。
