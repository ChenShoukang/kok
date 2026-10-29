# v0.8.1 公开预览准备 / Public preview readiness

2026-10-29。本页记录 v0.8.1 Preview 的发布门槛；它不是 1.0 稳定版。实际提交、云端构建及发布状态以仓库历史、Actions 和 Releases 为准。

| 项目 | 当前状态 |
| --- | --- |
| 私密移动 | 已修复可追踪目标记录、旧封面、普通撤销字段、正文索引和迁移快照残留；保留无关记录与缓存。独立 AI 历史、导出、原路径及文件系统历史仍有明确边界，见[私密说明](MODES.md) |
| 本地验证 | 原生 277 项通过；私密与完整双语桌面流程通过；静态检查、源码与 Git 历史凭据模式审计通过。其余验证及局限统一见[验证记录](VALIDATION_0.8.1.md) |
| 文档与社区 | 当前验证进入双语索引；旧 TESTING 明确标为历史记录；补充支持、社区约定、贡献要求与默认代码所有者；关闭空白 Issue |
| 源码提交 | 本次变更按构建、原生模块、界面、测试和文档分批提交；Release 标签应指向最终通过检查的提交 |
| 仓库状态 | 用户已授权公开仓库；公开动作必须在云端构建、测试与审计通过后执行 |
| 版本标签 | 旧标签未删除或重写。发布前核对现有 `v1.0.0` 的指向及历史用途，避免将它当成稳定版；新 Preview 标签只能指向已验证的最终提交 |
| 发布与 CI | 用户已授权发布 Preview；启用 `release.publish`，两组 Windows 构建与审计通过后上传 ZIP 和 SHA-256。CI 使用干净 checkout，结果见 Actions |

公开前依次完成：核对并提交源码 → 推送并确认 CI → 干净 clone 演练 → 确认仓库公开意愿和可见性 → 核对 README 下载 / Issue / 安全链接 → 发布 **v0.8.1 Preview** 的 ZIP 与 SHA-256。必须标明未签名、调试端口、进程内密码限速及桌面回归不在 CI 等[限制](VALIDATION_0.8.1.md#公开预览边界--public-preview-limits)。

The local candidate has targeted private-transfer cleanup and explicit residual-data boundaries. The user authorized publication and public visibility. Commit the tested tree, require both clean-checkout Windows build/test/audit jobs to pass, then publish a **Preview** with checksums and known limits and make the repository public. Actual status is recorded in repository history, Actions and Releases. Old tags are retained. See [validation](VALIDATION_0.8.1.md#english), [data boundaries](MODES.en.md), [support](../SUPPORT.md) and [community conduct](../CODE_OF_CONDUCT.md).
