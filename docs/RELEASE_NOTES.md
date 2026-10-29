# kok v0.8.1 Preview

把媒体资料与知识工作放进同一个仓库：今天、空间、摘录、检查器、智能集合、配方和回顾。

便携包未签名。调试参数 `--debug` 会开放本机 9225 端口；密码失败限速在重启后重置；交互桌面回归尚未进入 CI。私密移动不会追溯加密此前独立 AI 历史或导出，参见[私密数据边界](MODES.md)和[验证范围](VALIDATION_0.8.1.md)。

- 文档和 PPT 实现迁入 ES modules，草稿串行保存，新建入口 / 格式 / 快捷键跟随模块状态。
- 修复移入私密文件夹后的旧封面、普通撤销字段、索引和迁移快照残留；加密副本先保存，再按目标记录清理，保留其他资料，支持中断后启动重试。
- 检查器保存笔记后同步查看器与关闭快照，避免锁定时回写旧内容；原生错误提示经过界面语言翻译。
- 共用工具栏保留按钮与焦点，侧栏定位和照片布局由同一组件样式负责；修复工具栏空格误翻页和窄窗口断行。
- Agent 的三个全局脚本迁入单一 ES module；草稿在停用前保存，会话串行写入，同内容不重复落盘。
- 后台检索保留超过 5,000 个匹配项，避免限定范围时遗漏资料；重复请求合并，资料库切换后丢弃旧结果。
- 所有业务动作统一经过模块注册表、参数校验和模式策略；同一能力清单用于界面、Agent 和配方。
- SQLite 分开保存核心资料、模块分面、关系和操作历史，支持中文全文搜索。迁移保留原 JSON 与备份；私密数据库在内存中处理后加密保存。
- 全局操作审核与冲突安全撤销、托管文档字节版本；统一活动展示后台进度和取消，可续跑任务才提供暂停 / 继续。
- 四种空间视图、带来源的摘录、上下文 AI、四种可编辑配方模板；摘要有输入 / 输出上限和本地缓存，字幕模块提供可取消的 Agent 工具。
- 修复独立窗口切换编辑、返回旧正文、页签反馈、真实鼠标拖放和编辑菜单范围。集合分页限制渲染量；保留默认得意黑、中英文与大小窗口支持。
- 工作区与查看器实现迁入 ES modules；42 份样式源生成 9 个有序运行时资源。既有功能通过显式兼容层继续工作，旧组件和样式的语义清理仍在进行。

本版本按 Preview 发布，GitHub 工作流在两组 Windows 构建、测试与审计通过后创建预览版，附 ZIP 和 SHA-256。打包会检查目录和最终 ZIP，排除密钥、数据库、私密内容和测试资料。AI 检查使用本机模拟服务；字幕引擎没有执行真实语音识别。完整范围见[进度与验收边界](WAREHOUSE_0.8.1.md)。

## English

Preview: Today, Spaces, source-linked Clips, Inspector, smart collections, editable Recipes and Review connect media and knowledge work.

Document/PPT implementations also use real ES modules with serial draft saves and module-aware creation. The common toolbar preserves nodes/focus and owns viewer/photo geometry; keyboard activation no longer triggers media shortcuts.

Agent session, rendering and task interactions moved from three global scripts into one real ES module; saves serialize and coalesce, and disable/re-enable protects drafts without replay. Background search preserves matches beyond 5,000 before filtering, merges duplicate requests and drops stale library-scope results.

All business actions use the module registry, schemas and policy. SQLite separates core items, facets, common links and history, with full-text search and preserved migration backups. Private stores are sealed from memory. Operations provide review/conflict-safe undo; managed documents retain byte versions. Jobs centralize activity and expose only supported controls. AI summaries have bounded input/output and caching; the subtitle module contributes a cancellable Agent tool.

Native preview editing, refreshed reading content, tab feedback, real drag/drop and editor-menu scope are repaired. Collection pages bound rendering. Workspace/viewer implementations use ES modules, and 42 stylesheet sources generate nine runtime bundles. Legacy component consolidation remains incremental.

GitHub publishes this as a prerelease after both Windows build/test/audit jobs pass, with a portable ZIP and SHA-256. Package audits exclude credentials, databases, private content and test inputs. AI tests use loopback mocks; real speech recognition was not run. Private transfers now preserve encrypted covers and clean targeted ordinary records, undo fields, indexes and snapshots without clearing unrelated data. Packages remain unsigned; `--debug` opens local port 9225, password backoff resets on restart and desktop suites run outside CI. Private transfers do not retroactively protect independent AI history or exports. See [data boundaries](MODES.en.md), [validation](VALIDATION_0.8.1.md#english) and [scope and acceptance limits](WAREHOUSE_0.8.1.en.md).
