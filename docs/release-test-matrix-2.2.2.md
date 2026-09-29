# UniDesk 2.2.2 正式发布测试矩阵

本表只记录 `v2.2.2` 精确源码与最终安装包的证据。此前 `v2.2.1` 或本地 dirty 候选的通过结果不自动继承。项目所有者于 2026-09-30 明确要求不等待签名直接正式发布；[代码签名政策](../CODE_SIGNING_POLICY.md)中的例外仅适用于 `v2.2.2`。任何未执行的原生场景必须如实保持未验证。

- 源码提交：`2a6e8c6fa9bee871a015f7454664f09bfbcfa147`
- GitHub Release：[v2.2.2](https://github.com/SuperDaddyV/UniDesk/releases/tag/v2.2.2)
- 安装包：`UniDesk_Setup_2.2.2.exe`，147,473,217 bytes
- 安装包 SHA-256：`e568325de565c15769b9d1dcbdb60adac8e7df6497c186ae58208c47cf43a25b`
- 载荷来源清单 SHA-256：`4d6d37902f56157b34fea7fc8733ad3ba9440a9ae21dc96485bd0a51237662e8`
- SDK：`.NET 10.0.302`；载荷来源清单为 `schema=3`、`isDirty=false`、`win-x64`、1,431 个文件和 20 个目录

## 自动化与制品门禁

| ID | 要求 | 最终证据 |
| --- | --- | --- |
| AU-01 | `.NET SDK 10.0.302`、锁定还原、依赖漏洞检查、版本一致性 | [x] 本地正式构建：全部通过 |
| AU-02 | Release 构建零警告零错误、`dotnet test UniDesk.sln -c Release --no-restore` 全部通过 | [x] Release 构建 0 警告／0 错误；全量测试 `747/747` |
| AU-03 | `main` 精确提交的 GitHub CI 成功 | [x] [CI run 36606490417](https://github.com/SuperDaddyV/UniDesk/actions/runs/36606490417) |
| RS-01 | 干净 `main` 提交、schema 3 来源清单、载荷文件和目录哈希 | [x] `sourceRevision=2a6e8c6…`、`isDirty=false`、载荷完整性通过 |
| RS-02 | 安装包及全部一方 PE 为 `NotSigned`；PawnIO 哈希与上游签名有效 | [x] 安装器和 9 个清单项目为 `NotSigned`；PawnIO `Valid`，SHA-256 与固定值一致 |
| RS-03 | `Test-UnsignedReleaseReadiness.ps1` 输出清单与 SHA-256；GitHub 下载复核一致 | [x] 未签名门禁通过；Release 三资产下载后哈希与本地清单一致 |

## 原生安装与交互

| ID | 场景 | 当前证据 |
| --- | --- | --- |
| U01 | 从 `v2.2.1` 覆盖安装、设置与用户数据保留 | 未验证 |
| U02 | 托盘主区／隐藏区悬停、菜单、双击、退出与至少 30 次组合循环，确认左上角白框不再残留 | 未验证；实例级 WPF 回归不等于 Shell 实机通过 |
| U03 | Shell 辅助技术可访问名称与通知显示 | 未验证 |
| U04 | 窄宽、字体缩放、亮暗主题、透明度端点、多屏与 DPI | 未验证；离屏 WPF 渲染只覆盖等价夹具 |
| U05 | 天气 AQI 来源悬停、城市与「和风天气」字号和链接 | 未验证 |
| U06 | 待办长文本、日期、完成、排序及提醒 | 未验证 |
| U07 | 模型雷达榜单标签悬停、分数、离线与折叠态 | 未验证 |
| U08 | 七模块、七设置页、编辑弹窗、焦点、保存／取消与错误反馈 | 未验证；合成夹具仅覆盖布局渲染 |
| U09 | 服务、驱动、标准用户 UAC、取消和失败回滚 | 未验证 |
| U10 | 完整卸载与数据保留选项 | 未验证 |

项目所有者要求直接发布，不将上述未执行项写为 PASS。公开 Release 须说明安装包未签名、测试覆盖边界及可复核的最终制品身份。
