# v0.8.1 · 验证记录 / Validation

2026-10-29，Windows x64。以下为发布前本地验证；远程 Windows 构建与审计结果见 [Actions](https://github.com/ChenShoukang/kok/actions)。

| 验证 | 结果与范围 |
| --- | --- |
| 原生测试 | 277 项通过：迁移保留、分面、中文检索、整批回滚、操作冲突、真实文件撤销、私密索引、配方恢复、继承取消、完整历史导出 |
| 私密移动修复 | 精确保留并加密已有封面、重映射摘录；清理目标普通记录 / 索引 / 撤销字段及旧快照，压实 SQLite 并重建备份；验证无关记录、缓存和备份字段保留，同路径冲突拒绝、封面占用失败与启动恢复、原文件不变 |
| 私密桌面流程 | private-space：实际配置页、保存笔记 → 锁定 → 解锁、窗口失焦 / 最小化锁定、英文移动确认表单、原生分发、原文件字节保留及加密记录重开；修复检查器保存后旧查看器笔记未同步的问题 |
| 英文桌面流程 | i18n：完整英文界面与中文往返、原生错误 toast、用户文字保留、帮助与导出、当前音乐 / 阅读控件、紧凑窗口；补齐任务通知与返回按钮文案。静态文案目录共 2,044 条 |
| 资料与交互 | warehouse、warehouse-interactions：四种空间视图、实际鼠标拖放、摘录来源、草稿保护、智能集合、离线配方 |
| 大集合 | workspace-scale：2,500 条合成摘录，单页最多 120 张卡片，页面切换 / 保留、集合缩小、空间视图、中英文和窄 / 宽布局 |
| 独立窗口 | viewer-shell：查看 → 编辑 → 查看，托管文字保存 / 重开，照片调整草稿恢复，按钮实际可点击、照片工具不被遮挡 |
| 阅读 | reading-flow：单页 / 双页 / 滚动、滚轮翻页、快速缩放时延后排版、收藏完整、全屏、大小窗口、减少动态效果、双语对齐 |
| 模块与 AI | modules、agent-reliable、inspector-ai：策略与停用、纯净 / 私密边界、确认 / 撤销、取消、过期回复、费用限制、摘要缓存、AI 侧栏历史 |
| 可视化编辑 | presentations-agent：PPTX 编辑 / 导出回读、指定对象修改、键盘右键菜单、预览 / 应用 / 撤销、切页取消、Markdown 选区保护 |
| 实际资料 | real-files：用户提供的 11 份资料通过；原文件只读验证，哈希保持一致 |
| 本轮补充 | agent-save 验证私有模块作用域、重叠保存、同内容合并和失败重试；6,001 条 SQLite 资料检查中 / 短词、智能集合与 Agent 范围筛选；搜索单测检查并发合并、锁库清理和过期结果隔离 |
| 文档与查看器模块补充 | document-modules：格式 / 快捷键与开关同步；documents：自动保存、关闭保存、外部冲突、恢复与私密限制；presentation-save：并发写入、失败重试、重开与源文件字节保护；viewer-shell：节点 / 焦点保留、方向键、侧栏留白、多窗口开关同步、540 / 1280px 编辑布局；reading-flow：工具栏空格不误翻页；knowledge / presentations-agent：迁移后实际功能回归 |
| 静态检查 | JavaScript / ES modules 解析、禁止函数覆盖、模块生命周期、字体 / 样式资源、中英文目录与文档链接通过 |
| 干净便携包 | 单独暂存，不带现有 data；包含仓库原创示例。目录与 ZIP 均检查密钥 / 私密数据模式，使用隔离 profile 运行包内程序验证 |

AI 使用本机 loopback 模拟服务，本轮真实模型请求为 0；不读取真实 API 配置来执行测试。字幕能力通过模拟模块端口验证，没有运行真实语音识别。合成集合测试检查渲染量和交互，不能替代数万真实文件与长时间运行的压力测试。

复现入口：`build.ps1 -Test`、`npm run check` 与 `tools/test-desktop.ps1`。桌面套件数组应在 PowerShell 内传入，例如 `& ./tools/test-desktop.ps1 -Suites @('warehouse','viewer-shell','workspace-scale')`。测试会单独创建资料目录并关闭自己启动的窗口，不应同时运行多个桌面测试器。

尚存的兼容实现和样式收敛范围见[实际进度](WAREHOUSE_0.8.1.md)。这份验证记录不把整个旧界面的重写标为完成。

## English

The local Windows x64 preview passed 277 native checks and the desktop suites listed above. Private transfers additionally cover exact encrypted covers, remapped clips, targeted history/snapshot cleanup, compact backup regeneration, unrelated-data preservation, duplicate-path rejection and recovery after a locked cover interrupts cleanup. The private-space desktop suite verifies the actual move form, unlock, original-byte preservation and synchronized notes across locking. Tests also cover transactional migration, search, conflict-safe history/undo, encrypted private indexing, Recipes, Jobs, real pointer drag/drop, isolated preview editing, reader resizing, scoped AI interactions and PPTX round trips. Eleven user-provided files were checked read-only. A synthetic collection of 2,500 clips retains a 120-card page bound.

## 公开预览边界 / Public preview limits

- 便携包未签名；SHA-256 用于核对文件一致性，不代表发布者身份认证。Portable packages are unsigned; checksums verify file consistency, not publisher identity.
- `--debug` 会开启本机 9225 调试端口，只用于隔离测试，不应对可信网络边界以外开放。`--debug` enables the local 9225 debugging port; use it only for isolated testing.
- 私密密码失败限速只在当前进程内生效，重启会重置；不能替代强密码或磁盘加密。Password-attempt backoff is process-local and resets on restart.
- CI 执行静态、原生和打包审计；交互桌面套件当前在本机运行，尚未进入 CI。Interactive desktop suites run locally, outside CI.
- 私密移动处理应用可追踪的目标记录；此前独立 AI 历史、摘要缓存、导出及文件系统历史副本不受追溯加密保证，详见[数据边界](MODES.md)。Private moves do not retroactively encrypt independent AI history, summary caches, exports or filesystem copies; see [boundaries](MODES.en.md).

AI tests use loopback mocks, with zero real provider requests. Real speech recognition and long-duration stress over tens of thousands of disk files were not tested. A fresh portable staging directory includes original repository samples and excludes user data. Both staging and ZIP undergo credential/private-data pattern audits; packaged executable checks use isolated profiles. GitHub builds and release status are recorded in [Actions](https://github.com/ChenShoukang/kok/actions). [Remaining scope](WAREHOUSE_0.8.1.en.md) is explicit.
