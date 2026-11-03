# v0.11.1 Preview · 验证记录

本轮范围：[PPT 设计工作流](PPT_DESIGN_0.11.1.md)与[输出恢复和新手体验](OUTPUT_RECOVERY_0.11.1.md)。

## 已完成检查（2026-11-03）

- 原生：`build.ps1 -Test`，**915 tests passed**，记录 `test-output/release-0.11.1-native-final.log`。覆盖四主题 × 九种原生构图的形状边界、字体/色块/指标值、过密内容拒绝，输出截断与输入超限区分、有限恢复、请求上限、输出预留、JSON 兼容协议及流式末尾文字/用量。
- 创作与交互：最终九项回归全部通过，记录 `test-output/full-20261103-184617-7408cb40`：`agent-output-recovery`、`creative-e2e`、`creative-artifacts`、`agent-presentations`、`presentation-save`、`presentations-agent`、`agent-document-edit`、`agent-workflow`、`i18n`。实际覆盖三份资料到报告、手动与 Agent 局部修改、八页 PPT、原生重启后继续、命名版本恢复、真实 DOCX/PPTX 导出回读，以及截断后有界重生成、审批前不写入、失败后保留目标和资料的分段继续。
- PPT 设计与直接编辑：`presentation-design`、`presentation-editing` 通过，记录 `test-output/full-20261103-182235-47d336b5` 中这两项的独立结果。八页可编辑构图、字体与色块导出回读；双击改字并自动保存、撤销/重做、复制后的唯一对象 ID、拖动排序、删除与撤销、重新打开、按需展开属性、当前页 Agent 仅填草稿均已验证。同一批次随后阅读套件因旧夹具失败，不计为整批通过。
- 阅读与资料工具：修正旧夹具后，`reading-renewal` 通过，记录 `test-output/full-20261103-183729-2775e264/reading-renewal`；`study-tools` 通过，记录 `test-output/full-20261103-184300-7032ea30`。验证自动深色纸张、选段解释、翻译恢复、原生窗口状态、私密移动、多格式对比与引用、缓存与取消。以上共 **13 项不同桌面套件**，分别按对应记录核验。
- 静态：`npm run check` 通过；本地化 **2,628 条消息 / 43 动态模式**，版本契约 0.11.1，PowerShell 脚本语法检查通过。依赖审计无已知漏洞，锁文件仅更新本项目版本。
- 公开源检查：源文件发行隐私检查与 Git 历史检查通过，无凭据或私密数据模式命中。新增来源保留 Apache 2.0 许可与固定上游版本，未引入上游未授权编辑器。
- 本地交付包：解压后的 **6 项套件全部通过**，记录 `test-output/package-a0fdcbd096e546139cb85c5b9cf91473`：`presentation-editing`、`presentation-design`、`agent-output-recovery`、`creative-e2e`、`agent-document-edit`、`agent-presentations`。校验可执行文件版本、归档 SHA-256 与运行前后指纹；实际执行改字、排序、重启续作、恢复与导出。首轮测试窗未就绪时，测试器按既有规则重启后完成，不跳过断言。

## 验证方式

真实 Windows WebView2、隔离资料库、本机模型夹具；无真实模型付费调用。夹具验证应用流程、权限与文件一致性，不作为真实模型写作或设计质量指标。设计页面已逐页截图检查，报告记录在 `test-output/presentation-design/report.json`。

GitHub 公开包由仓库 Windows 2022 / 2025 流水线重新构建，发布门槛包括原生测试和解压后 **26 项桌面套件**。通过后由流水线发布 Preview；公开包摘要以 Release 附件为准，本地候选包与流水线包可能因构建时间不同而字节不同。
