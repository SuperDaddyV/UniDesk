# UniDesk 2.2.1 正式发布测试矩阵

> **当前候选记录**：本文件只记录 `v2.2.1` 精确候选和最终制品的本轮证据。`v2.2.0` 的 PASS、测试数量、安装结果和截图不得继承；未实际执行的项目必须保持未勾选。

## 发布范围

- 目标版本：`v2.2.1` 正式版。
- 安装包：`UniDesk_Setup_2.2.1.exe`。
- SDK：仓库 `global.json` 固定的 `.NET SDK 10.0.302`。
- 平台：Windows 11 x64，以及 Windows 10 Enterprise／IoT Enterprise LTSC 2021 或更新版本 x64。
- 发布性质：项目所有者于 2026-09-01 单独批准、仅适用于精确 `v2.2.1` 的未签名正式版例外。安装包和全部 UniDesk 一方 PE 必须为 `Authenticode: NotSigned`；README 与 Release 使用简短、中性措辞说明 Windows 可能显示 SmartScreen／企业策略提示。

## 自动化门禁

| ID | 门禁 | 当前证据 |
| --- | --- | --- |
| AU-01 | SDK 锁定 | [x] 2026-09-01：`dotnet --version` 精确为 `10.0.302` |
| AU-02 | 锁定还原 | [x] 2026-09-01：`dotnet restore UniDesk.sln -r win-x64 --locked-mode` 通过，锁文件无变化 |
| AU-03 | 依赖漏洞 | [x] 2026-09-01：`scripts/Test-PackageVulnerabilities.ps1` 通过 |
| AU-04 | 版本一致性 | [x] 2026-09-01：`scripts/Test-VersionConsistency.ps1 -ExpectedVersion 2.2.1` 通过 |
| AU-05 | Release 构建 | [x] 2026-09-01：零警告、零错误 |
| AU-06 | 全量测试 | [x] 2026-09-01：`691／691` 通过，零失败、零跳过 |
| AU-07 | 差异检查 | [x] 2026-09-01：`git diff --check` 通过 |
| AU-08 | GitHub CI | [ ] 精确最终 `main` 提交的 `build-and-test` 成功 |

## 制品完整性门禁

| ID | 门禁 | 当前证据 |
| --- | --- | --- |
| RS-01 | 干净来源 | [ ] 最终制品来自公开 `main` 的精确干净提交 |
| RS-02 | 载荷清单 | [ ] `release-source.json` 覆盖 App、HardwareService、HardwareRepair 全部文件和目录，递归哈希零差异 |
| RS-03 | 载荷卫生 | [ ] 安装载荷无 PDB、未知调试文件、临时文件或来源不明文件 |
| RS-04 | 未签名门禁 | [ ] `scripts/Test-UnsignedReleaseReadiness.ps1` 对 `2.2.1` 通过，且允许列表只包含单独批准版本 |
| RS-05 | PE 状态 | [ ] 安装包和全部 UniDesk 一方 PE 均为 `NotSigned`；PawnIO 固定哈希匹配且上游签名有效 |
| RS-06 | 版本绑定 | [ ] 安装包 ProductVersion／FileVersion、清单版本和源码提交一致 |
| RS-07 | 公开资产 | [ ] Release 资产恰好为安装包、`SHA256SUMS.txt`、`release-manifest.json` |
| RS-08 | 独立哈希 | [ ] GitHub API digest、下载后 SHA-256、`SHA256SUMS.txt` 与清单完全一致 |

## 人工安装与交互验收

| ID | 场景 | 当前证据 |
| --- | --- | --- |
| I-01 | 支持系统上的全新安装、首次启动和退出 | [ ] 未执行 |
| I-02 | 从公开 `v2.2.0` 覆盖升级，设置、数据库和用户内容保留 | [ ] 未执行 |
| I-03 | 标准用户 UAC、取消 UAC 与安装失败回滚 | [ ] 未执行 |
| I-04 | 开机自启启用／禁用及路径带空格 | [ ] 未执行 |
| I-05 | 完整硬件组件安装、服务启动、修复、兼容模式和卸载 | [ ] 未执行 |
| I-06 | 卸载后程序文件、服务与用户数据保留策略符合界面选择 | [ ] 未执行 |
| I-07 | SmartScreen／企业策略实际提示与 Release 说明一致 | [ ] 未执行 |
| I-08 | 100%／125%／150%／175%／200% DPI 和多分辨率展开／收缩 | [ ] 未执行 |
| I-09 | 默认浅色、深色及透明主题下主界面模块无裁切、重叠或错位 | [ ] 未执行 |

## 已取得的视觉证据

- [x] 2026-09-01：项目所有者完成隔离预览验收，确认收缩窗口 `350 × 190` DIP、待办基线、主题字体，以及单行 `TOP`／`VAL` 模型雷达摘要效果。
- [ ] 上述预览不是安装包运行证据，不能替代 I-01 至 I-09。

## 发布停止条件

- 任一自动化或制品门禁失败；最终 `main`／标签／清单／安装包提交不一致；资产数量或哈希不一致；出现 PDB 或未知文件；任何一方 PE 不是 `NotSigned`；PawnIO 哈希或上游签名无效；或必要人工安装矩阵未完成时，均不得创建或更新 GitHub Release。
