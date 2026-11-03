# v0.10.12 Preview · 验证记录

本轮范围为 [持续创作与命名检查点](CREATIVE_CONTINUATION_0.10.12.md)。

## 已完成检查（2026-11-03）

- 原生：`build.ps1 -Test`，**894 tests passed**，记录 `test-output/p3-build-continued.log`。覆盖加密会话成果记录、分支投影与分叉、损坏条目跳过、命名/重命名/移除、命名元数据写入失败回滚和备份，以及超过 100 次保存后的命名版本可见性与恢复。原有文档/版本检查继续执行。
- 桌面：**18 个套件全部通过**，记录 `test-output/full-20261103-140102-73d02d45`，运行版本 **0.10.12**，源与运行时指纹在结束时校验一致。套件：`creative-artifacts`、`agent-workflow`、`agent-document-edit`、`agent-presentations`、`agent-kernel`、`agent-reliable`、`pi-agent-ui`、`works-navigation`、`agent`、`agent-interactions`、`agent-quality`、`agent-runtime`、`agent-platform`、`i18n`、`features`、`workspaces`、`presentations-agent`、`presentation-save`。
- P3 真实界面流程：创建项目并由 Agent 保存文档；切到早期消息分支后继续访问项目成果；重载后保持所选分支且不重放工具卡片；切换/分叉成果节点；新建无任务的项目会话并重载，仍能打开实际文件。项目索引与当前对话的同一成果只显示一次。
- 时序与预算：迟到的会话索引不能进入新会话，迟到的项目记录不能覆盖新记录或 workspace 缓存；清除项目上下文后不保留其他会话成果。上述导航不新增模型请求、不新建任务、文件字节保持一致。
- 版本界面：历史版本命名、重命名、移除、重新打开后的持久化和差异标题；保留 P2 的保存前取消/确认、恢复后编辑器同步、选段/选页交给 Agent 的检查。
- 使用隔离资料库、真实 WebView2 与本地模拟模型；本轮无真实模型付费调用，不以夹具结果宣称实际模型创作质量。

## 交付范围

补充续作边界：同一任务等待补充信息后再次完成时，新增成果会继续登记到项目和会话索引；相同完成阶段的重复回调保持去重。新增两项原生检查验证后续成果可达且不会重复增加操作或会话条目。

交付包为 `dist/kok-0.10.12-win-x64.zip`。打包复用已通过上述检查的运行时，保留 `kok-agent.exe`，仅复制既定运行依赖、UI、文档与样例，排除用户资料和开发测试程序。ZIP 摘要及独立解压复验记录在交付后补充到工作区记录；归档内不嵌入自身 SHA-256。

P3 完成；P4 的完整“报告→改稿→八页 PPT→换会话续作→恢复→导出”场景仍待下一阶段。命名针对历史版本，PPT 版本可视化对比不在本轮范围。

## 最终交付复验（工作区追加）

- 静态检查：最终 `npm run check` 通过，记录 `test-output/p3-static-final.log`；版本契约为 0.10.12。
- 最终 ZIP：`dist/kok-0.10.12-win-x64.zip`，32,637,740 字节（31.13 MiB），发行隐私审计 717 个文件通过。
- SHA-256：`b21850f744ff8730a7fa50347a5c8ddd69cace025da9c95d7d5a38f9ce61a3de`。
- 解压复验：`creative-artifacts`、`agent-document-edit`、`agent-presentations` 三组全部通过，记录 `test-output/package-70f2e232449345a8b8153bafa2ab64a8`。其中 `archive.json` 保存归档摘要与源/运行时指纹。
- 本节作为归档生成后的独立工作区记录，不重新改写已验证的 ZIP。
