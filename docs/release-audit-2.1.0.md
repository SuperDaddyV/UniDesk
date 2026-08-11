# UniDesk v2.1.0 最终发布审核

审核日期：2026-07-28（2026-08-11 安装兼容修订）
审核范围：`C:\Users\Administrator\Documents\UniDesk` 当前工作区
审核结论：最新代码未发现剩余 P0／P1；在干净提交、可信签名和跨环境人工门禁完成前仍不得公开发布。当前电脑的旧安装位于弱 ACL 的 `D:\Program Files\UniDesk`，不再视为安全候选，也不得运行其中的旧卸载器；应由新版安装器完成受信迁移。

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
| `dotnet test UniDesk.sln -c Release --no-restore` | `492/492` 通过，0 跳过 |
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

1. SignPath Foundation 审批项目并提供组织、项目、策略和 Artifact Configuration 标识。
2. 仓库管理员安装 SignPath GitHub App，并配置规定的 Secret 和 Variables。
3. 当前全部预发布修改形成干净提交并推送；新的 GitHub CI 必须在该精确提交上通过。
4. 在该提交上运行签名工作流，取得 `Authenticode=Valid` 的候选包和匹配的 `release-manifest.json`。
5. 安装签名候选包并检查 `UniDeskHardwareService` 注册路径带双引号，完成剩余人工矩阵。
6. 用户最终确认后，才能创建 `v2.1.0` tag 和 GitHub Release。

当前 Windows 11 电脑的只读复核发现：`D:\Program Files` 与卷根允许 `Authenticated Users` 修改，而旧 `UniDeskHardwareService` 以 `LocalSystem` 从该树运行；旧安装因此不再记为 I-09／I-13 安全通过。新版允许普通权限主程序继续位于 D 盘，但会退役旧目录服务，并把服务、修复工具和卸载器迁移到系统 `{commoncf}\UniDesk`；本轮未自动修改系统 ACL、停止服务或卸载旧版，仍须用最终候选包完成覆盖安装人工验证。

除上述发布门禁外，本轮没有发现需要在 `v2.1.0` 增加的新功能或升级的大版本依赖。
