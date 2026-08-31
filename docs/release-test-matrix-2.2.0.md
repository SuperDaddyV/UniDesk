# UniDesk 2.2.0 正式发布测试矩阵

## 范围与边界

- 目标版本：`v2.2.0` 正式版。
- 安装包：`UniDesk_Setup_2.2.0.exe`。
- 发布性质：项目所有者于 2026-08-31 单独批准的未签名正式版例外；安装包与全部 UniDesk 一方 PE 必须为 `Authenticode: NotSigned`，并公开披露 Windows SmartScreen／企业策略风险。
- 本版本相对 `v2.1.0` 的主要增量为 Calm Glass 视觉系统、内嵌字体、自适应面板尺寸与模块排版。安装器安全模型、硬件组件边界和数据库 schema 不变。
- 动态证据（最终源码提交、安装包 SHA-256、清单 SHA-256、GitHub Actions 运行和回下载结果）记录在 GitHub Release 正文及随包发布的 `release-manifest.json`、`SHA256SUMS.txt` 中，避免把构建后才产生的哈希反写进候选源码。

## 已完成的人工视觉验收

以下项目由项目所有者在隔离预览中逐项检查，并在最终视觉修订后确认可发布：

| ID | 检查项 | 通过标准 |
| --- | --- | --- |
| CG-01 | 透明毛玻璃与色盘 | 主面板保留壁纸透出的着色玻璃、白色信息层级；切换色盘能立即改变玻璃色调 |
| CG-02 | B 版中英文字体 | 中文使用 Source Han Sans SC、拉丁文字使用 Inter；模块标题字号、字重、基线与图标一致 |
| CG-03 | 硬件监视网络行 | RX／TX 两组整体居中；标签与实时数值处于同一水平线，数值刷新不跳动 |
| CG-04 | 待办事项行 | 正文、到期信息与勾选圆圈在 46 DIP 行框内视觉居中，点击热区不缩小 |
| CG-05 | 快捷方式编辑入口 | 右上角使用透明细线铅笔／对勾图标，无错误私有码位或文字替代 |
| CG-06 | 天气来源署名 | 位置行不重复显示来源图标；和风天气链接图标与文字尺寸协调 |
| CG-07 | 模块标题层级 | 硬件监视、快捷方式、待办事项、快速便签、快捷文本和模型雷达使用统一标题样式 |

## 自动化与源码门禁

| ID | 检查项 | 通过标准 |
| --- | --- | --- |
| AU-01 | 锁定还原 | `dotnet restore UniDesk.sln -r win-x64 --locked-mode` 成功 |
| AU-02 | 依赖漏洞 | `scripts/Test-PackageVulnerabilities.ps1` 无已知传递依赖漏洞 |
| AU-03 | 版本一致性 | `scripts/Test-VersionConsistency.ps1 -ExpectedVersion 2.2.0` 通过 |
| AU-04 | Release 构建 | `dotnet build UniDesk.sln -c Release --no-restore` 零警告、零错误 |
| AU-05 | 全量测试 | `dotnet test UniDesk.sln -c Release --no-build` 全部通过、无跳过 |
| AU-06 | 视觉与布局回归 | Calm Glass、字体打包、模块几何、自适应尺寸、系统主题和 WPF 交互测试全部通过 |

## 精确候选与分发门禁

| ID | 检查项 | 通过标准 |
| --- | --- | --- |
| RS-01 | 候选来源 | PR 合并后从公开 `main` 的精确 40 位提交在全新干净工作树构建 |
| RS-02 | SDK 与锁文件 | 使用 `global.json` 固定的 .NET SDK `10.0.302`；源清单中的 SDK、`global.json` 和全部锁文件哈希与源码一致 |
| RS-03 | 载荷完整性 | `release-source.json` 为 schema 3、`isDirty=false`；目录／文件清单与递归 SHA-256 完整匹配；载荷无 PDB |
| RS-04 | 未签名发布门禁 | `scripts/Test-UnsignedReleaseReadiness.ps1` 仅按明确允许列表接受 `2.1.0`／`2.2.0`，并对本候选通过 |
| RS-05 | Authenticode | 安装包与全部 UniDesk 一方 PE 为 `NotSigned`；PawnIO 安装器保持固定 SHA-256 和有效上游签名 |
| RS-06 | 安装包绑定 | 安装包产品版本为 `2.2.0`，版本资源中的两段载荷指纹可重组为 `release-source.json` 的 SHA-256 |
| RS-07 | 公开资产 | Release 显示名称仅为 `UniDesk v2.2.0`，不在仓库首页侧栏标题中附加“未签名”；资产只包含 `UniDesk_Setup_2.2.0.exe`、`SHA256SUMS.txt` 和 `release-manifest.json`，详情正文醒目披露未签名风险 |
| RS-08 | GitHub 回下载 | 从公开 Release 重新下载全部资产；安装包 SHA-256、版本、签名状态、标签提交和资产集合与发布前证据一致 |

## 自适应尺寸与升级兼容

| ID | 检查项 | 通过标准 |
| --- | --- | --- |
| PS-01 | 全新默认尺寸 | 推荐宽度为 340 DIP；推荐高度取工作区 70%，并限制在 560–840 DIP 及可用工作区内 |
| PS-02 | DIY 范围 | 宽度偏好限制为 320–520 DIP，高度偏好限制为 560–1040 DIP；实际窗口继续受当前显示器工作区约束 |
| PS-03 | 已有用户偏好 | 升级时保留已保存的宽高、透明度、色盘、字体比例、模块开关与排序，不按分辨率覆盖 |
| PS-04 | 跨显示器 | 窗口移入不同工作区时重新限制实际宽高与位置，不改写用户的首选尺寸；模块内部继续按容器对齐 |
| IN-01 | 现有数据 | 数据库 schema 不变；缺失的面板尺寸设置才写入当前显示器推荐值，已有值不迁移、不清空 |
| IN-02 | 运行验证 | 从最终发布载荷启动主程序，确认版本资源、主窗口和设置窗口可加载，无字体或资源缺失异常 |

## 发布停止条件

出现以下任一情况即停止创建或继续公开 Release：CI 未通过；候选不是精确干净 `main`；版本／SDK／锁文件不一致；载荷或安装包指纹不匹配；任一 UniDesk 一方 PE 不是 `NotSigned`；PawnIO 哈希或上游签名异常；存在 PDB；公开资产哈希与本地候选不一致；Release 被错误标记为预发布或没有成为 Latest。
