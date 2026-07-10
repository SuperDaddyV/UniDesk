# UniDesk 稳定性与数据完整性设计

## 目标

本设计覆盖已批准的阶段 0、1、2：补齐项目规范；保证 JSON 备份恢复原子、可验证且结果真实；让数据库初始化和设置持久化失败可见；启用 WAL 并修正数据库版本比较。

## 范围

### 纳入

- 项目级 `AGENTS.md` 与受影响的 `docs/DESIGN.md` 约束。
- 备份文件的版本、分区和记录级语义校验。
- 单连接、单事务恢复设置、快捷方式、待办、快速便签、剪贴板历史和快捷文本。
- 恢复提交后的设置缓存失效，以及快捷方式图标的提交后补全。
- 数据库初始化失败上抛、初始化事务、WAL、语义版本比较。
- 设置缓存和 debounce 状态同步、写入失败传播、失败批次重新入队。

### 不纳入

- CI/CD、DPAPI、密钥轮换、日志删除、单实例、硬件采集、MainWindow 拆分和新功能。
- 数据库 schema 变更或历史业务数据迁移。
- `Cache=Shared`、自定义 3 秒 `busy_timeout` 或 `synchronous=NORMAL`。

## 恢复架构

`IDatabaseService` 继续提供单次操作 API，并新增 `ExecuteInTransactionAsync<T>`。事务回调只接收 `IDatabaseSession`；session 封装同一个 `SqliteConnection` 与 `SqliteTransaction`，所有命令显式绑定事务。

`TodoBackupService` 的恢复分两步：

1. 读取、反序列化并构建经过验证的恢复计划。空集合表示用户明确要清空该分区；集合内存在无效记录则整个文件无效。
2. flush 既有设置写入，在一个事务内完成所有被包含分区的删除与插入。提交后清空设置缓存，再补全快捷方式图标。图标是派生文件，失败不回滚已提交的数据。

备份版本只接受 `1` 至当前版本 `4`。旧版 Todo-only v1 保持兼容；未来版本拒绝导入，防止新字段被旧程序静默丢弃。

## 数据校验

- Settings：空白 key 使导入失败；受保护的默认天气凭据继续跳过；模块设置先规范化。
- Shortcuts：`Name` 与 `Path` 必须非空；排序按备份 `SortOrder` 和原始顺序规范成连续整数。
- Todos：`Title` 必须非空。
- QuickNotes：`Title` 与 `Content` 不得同时为空。
- ClipboardHistory：规范化后的 `Content` 必须非空；哈希由内容重新计算，不信任外部文件中的哈希。
- TextSnippets：`Content` 必须非空；空分类规范为“默认”。

## 错误与缓存

- 恢复校验失败抛出 `InvalidDataException`，不打开写事务。
- 事务内任一 SQL 失败都回滚并原样向上抛出。
- 设置手动 flush 失败时，失败批次重新放回待写队列并抛出；设置窗口据此保持打开并显示失败。
- 后台 debounce flush 捕获并记录异常，待写值保留供下一次 flush 重试。
- `DatabaseService.InitializeAsync` 记录异常后重新抛出，由 `App.OnStartup` 现有启动失败路径提示并退出。

## 数据库初始化

- 打开文件数据库后执行 `PRAGMA journal_mode=WAL`，保留 provider 默认 30 秒命令超时。
- schema 创建、迁移、默认值和版本写入在一个显式事务内完成。
- 版本比较使用 `System.Version`，无效数据库版本视为初始化错误。

## 测试策略

- 用真实临时 SQLite 文件验证语义无效备份不改数据。
- 用 SQLite trigger 在事务中强制插入失败，验证所有分区与设置回滚。
- 验证旧版 v1 备份、空集合清空、真实计数、设置缓存刷新和快捷方式顺序。
- 用可切换失败的 `IDatabaseService` 测试设置 flush 的异常传播、重新入队与重试。
- 验证无效数据库路径上抛、WAL 已启用、`1.10 > 1.9`。

## 成功标准

- 所有新增测试先在旧实现上按预期失败，再在新实现上通过。
- `dotnet test UniDesk.sln -c Release --no-restore` 全部通过。
- 本轮不产生 schema 变更、CI 配置、密钥修改、文件清理逻辑或无关重构。
