# v0.10.13 Preview · 验证记录

范围：[创作项目 P4 完整流程](CREATIVE_ACCEPTANCE_0.10.13.md)。

## 已完成检查（2026-11-03）

- 原生：`build.ps1 -Test`，**898 tests passed**，记录 `test-output/p4-native.log`。本轮新增四项，覆盖项目已有成果默认可操作、显式范围保持、同一任务后续消息与成果保留、重复同步不重置分支。既有 894 项检查继续通过。
- 桌面：**19 个套件全部通过**，审计目录 `test-output/full-20261103-162036-96187634`，运行版本 0.10.13，开始与结束的源/运行时指纹一致。套件：`creative-e2e`、`creative-artifacts`、`agent-workflow`、`agent-document-edit`、`agent-presentations`、`agent-kernel`、`agent-reliable`、`pi-agent-ui`、`works-navigation`、`agent`、`agent-interactions`、`agent-quality`、`agent-runtime`、`agent-platform`、`i18n`、`features`、`workspaces`、`presentations-agent`、`presentation-save`。
- 静态：`npm run check` 通过，记录 `test-output/p4-static.log`；版本契约 0.10.13，本地化 2,618 条消息 / 43 个动态模式、文档链接 1,253 项。`git diff --check` 和另存为测试 PowerShell 语法检查通过。依赖版本未变。
- 完整流程：真实 WebView2 与原生任务读取三份资料、保存报告、手改并确认保存、选第二节交给 Agent、确认局部替换、读取改后正文创建八页 PPT、结束并重启隔离原生进程、新建同项目会话后修改同一 PPT 的第二页备注、恢复报告命名检查点、取消一次另存为、实际导出 DOCX / PPTX 并原生回读。
- 文件边界：三份原始资料 SHA-256 不变；报告保持同一文件，第二节之外与手改段落一致；PPT 续作保持同一文件，第二页之外的七页不变；恢复后的 DOCX 保留手改段落；导出 PPTX 恰好八页且包含续作备注，托管原件 hash 不变。
- 请求边界：选段交给 Agent 只填草稿；重启、版本恢复、取消与导出不新增模型请求或任务。固定夹具四个创作任务共 12 次模型协议请求，用于验证应用流程，不作为真实模型的 token 成本或质量指标。

## 环境与限制

测试使用隔离资料库、真实 Windows WebView2 和本机模拟模型，没有真实模型付费调用。重启模拟隔天续作，没有修改系统时钟或声称完成跨日长期测试。PPT 版本可视化比较仍未接入。审批、证据核对、补偿式恢复、撤销和私密边界沿用现有机制。

## 交付

本地发行包为 `dist/kok-0.10.13-win-x64.zip`。打包直接复制已验证的原生程序和运行时 UI，保留既定发行白名单与隐私审计；排除测试资料库、凭据和开发测试程序。解压完整目录后运行 `kok.exe`。

归档摘要与独立解压复验在生成 ZIP 后写入工作区附录，避免在 ZIP 内嵌入自身摘要。

## 最终归档复验（工作区附录）

- ZIP：`dist/kok-0.10.13-win-x64.zip`，**32,845,716 字节（31.32 MiB）**。
- SHA-256：`77edb14ef2dfdbbfd285d2f577f584bed698f2db306e1d2e6ecd903fd851d280`；同目录 `.sha256` 文件已生成。
- 发行隐私审计：773 个文件通过，无凭据或私密数据模式命中。
- 从该 ZIP 独立解压复验 **4 组全部通过**：`creative-e2e`、`creative-artifacts`、`agent-document-edit`、`agent-presentations`。
- 归档证据：`test-output/package-ff17bedc061a4301857523b9fdbbe7c7/archive.json`；完整桌面证据：`test-output/full-20261103-162036-96187634/run.json`。源指纹相同，ZIP 摘要复核一致。
- 发行运行时逐文件比对：447 个程序、CLI、DLL、配置和 UI 文件与此前 19 组回归的构建目录字节一致，记录 `test-output/p4-delivery-consistency.json`。发行包排除了构建目录里的 5 个开发测试程序配置文件，因此整目录指纹不同；这是发行白名单的预期差异。
- P4 完成。此附录在生成归档后写入工作区，未重新改写已验证 ZIP。
