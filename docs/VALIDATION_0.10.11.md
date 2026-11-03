# v0.10.11 Preview · 验证记录

本版完成创作项目 P2：**版本浏览/差异/恢复 UI**、**手动保存前差异预览**、**选中文字/幻灯片 → Agent 局部改稿入口**。审批、证据核对、撤销与沙箱语义不变；差异计算全部在本机完成。

## 验证范围

- 静态检查：`npm run check`（样式、界面脚本语法、模块架构契约、本地化目录、文档链接、版本契约、30 项任务样本离线校验）。
- 原生检查：`build.ps1 -Test`。
- 桌面回归：隔离资料库 + 本地模拟服务 + 真实 WebView2，覆盖版本面板/差异/恢复、保存前确认、文档选中片段→Agent、PPT 当前页→Agent，以及既有 Agent/作品/空间/演示/文档/i18n/功能套件。
- 交付包复验：`tools/test-package.ps1` 从 ZIP 运行 **4 个桌面套件全部通过**（agent、agent-document-edit、agent-presentations、works-navigation），记录 `test-output/package-d7cb74584b6244d38dc6c5725b5ee118/`（`archive.json` 保存套件列表、源 / 运行时指纹与 ZIP SHA-256）。
- 本轮不发起真实模型请求，不使用真实 API 预算。

## 结果（2026-11-03）

- 版本契约：`node tools/check-version.cjs` 通过，`package.json` / `AssemblyInfo` / 浏览器扩展 / README 包名一致为 **0.10.11**。
- 原生检查：**869 项通过**（上一基线 862 项；新增 7 项 `documentVersions`/`documentVersion`/`documentVersionRestore` action 检查，并恢复/保留既有 55 项文档检查）。
- 版本与恢复：文档编辑器「版本」面板列出当前+历史版本、逐行 diff（`+N/−M`）、任意版本恢复（补偿式操作、可再撤销，编辑器/文件/签名同步）；DOCX 文本不可用降级为 hash/大小；私密库禁用。
- 保存前差异：显式保存且内容变化时显示「保存前确认」，取消不写文件且保持未保存，确认后走原保存路径；自动保存与失败重试不变。
- 局部改稿：文档选中片段 →「让 Agent 改这段」（≤2000 字，含文档名/ID、引用原文与 replace_document_text 约束）；PPT 当前页 →「让 Agent 改这页」（页码/标题/页文字 ≤1500 字、只改本页约束）；均只填充输入框并带上下文，不自动发送、不新建任务。
- 桌面回归：**17 个套件全部通过**——`agent`、`agent-kernel`、`agent-interactions`、`agent-workflow`、`agent-reliable`、`agent-quality`、`pi-agent-ui`、`agent-runtime`、`agent-platform`、`i18n`、`features`、`works-navigation`、`workspaces`、`agent-document-edit`、`agent-presentations`、`presentations-agent`、`presentation-save`。运行记录：`test-output/full-20261103-121555-28050920/`（源/运行时指纹运行结束后校验一致）。
- 交付包：`dist/kok-0.10.11-win-x64.zip`，**30.74 MB**，SHA-256 `e9d791a0c66ea9c995654fd5e76910acc1df5890bb919c0f53c4c760df8be9ee`；产品版本与 `package.json` 一致。

## 证据

- 原生测试输出：`869 tests passed`（`build.ps1 -Test`，构建产物 `dist/kok`）。
- 桌面运行记录：`test-output/full-20261103-121555-28050920/`。
- 交付包与校验和：`dist/kok-0.10.11-win-x64.zip` 及同名 `.sha256`；包级复验记录在 `test-output/package-*/`。
- 新增/更新测试：`tests/DocumentTests.cs`（P2 action 7 项 + 既有 55 项文档/版本检查）、`tests/agent-document-edit.cjs`（版本/diff/恢复、保存前确认、选中→Agent）、`tests/agent-presentations.cjs`（当前页→Agent）。
- 文档：[版本与局部改稿](VERSIONS_0.10.11.md)。

## 边界

- 差异面板最多 100 条版本、单侧 380k 字符/4000 行、渲染行数有上限；差异是本机 LCS，不发起模型请求。
- DOCX 版本支持字节恢复与 hash 对比，文本 diff 需要可提取文本；PPT 版本对比与命名检查点未做。
- 局部改稿只生成提示词与上下文，不自动发送、不新增审批语义；写入仍走原有审批/证据/撤销链路。
