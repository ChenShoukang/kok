# kok v0.11.1 Preview

从资料到可继续修改的作品，让第一次使用 Agent 的人也能顺着完成任务。

- **PPT 设计**：默认杂志风格，四套主题、九种原生可编辑构图。演示任务自动加载 baoyu-design 中明确以 Apache 2.0 授权的视觉指引；封面、对比、流程、指标与章节页先预览，再确认保存。字体、图形与备注可继续修改并导出 PPTX。
- **直接编辑**：双击页面文字输入，离开文字自动保存；属性面板按需打开。左侧支持复制、拖动排序和删除，支持撤销/重做；顶部只保留一个当前页 Agent 入口。
- **回答恢复**：区分输出上限与输入上下文超限。较长回答在原请求和费用额度内自动调整，最多额外两次；仍未完成可“分段继续”，保留原目标与当前资料。截断的工具参数不会执行。
- **持续创作**：项目保留目标、资料、会话与成果；支持报告手改、Agent 定向改稿、历史版本对比/恢复与命名检查点。成果跨分支、新会话和重启可达。
- **资料欣赏**：资料库继续独立观看视频、听音乐、阅读文档和电子书，保留进度、收藏与笔记。

新手制作 PPT：新建演示文稿 → 填主题/页数/风格 → 用 Agent 制作 → 预览 → 确认保存。后续从同一作品选页修改。

完整创作验收路径：三份资料 → 报告 → 手改一段 → Agent 改第二节 → 八页 PPT → 重启换会话继续 → 恢复命名版本 → DOCX / PPTX 导出。

[设计接入范围与许可](PPT_DESIGN_0.11.1.md) · [回答恢复](OUTPUT_RECOVERY_0.11.1.md) · [验证记录](VALIDATION_0.11.1.md) · [完整版本记录](../CHANGELOG.md)

Windows 包为 `kok-0.11.1-win-x64.zip`，核对随附 `.sha256`，完整解压后运行 `kok.exe`。以本 Release 附件的大小和摘要为准。

## English

PPT creation now defaults to Editorial, with four themes and nine native editable compositions. The explicitly Apache-2.0 visual guidance distributed in baoyu-design loads automatically for presentation tasks; its original text and license are retained. Preview, targeted revisions and PPTX export share editable typography, shapes and notes.

Double-click text to edit directly with automatic saving. Properties are optional; duplicate, drag-reorder, delete and undo sit beside the page thumbnails. One current-page Agent entry prepares a scoped revision draft.

Bounded output recovery stays within the original request and cost limits. Continue in parts restores the goal and current materials; truncated tool arguments never execute. Projects, editable reports, named versions and persistent artifacts support continuation across sessions and restarts.

Tests use real WebView2, isolated profiles and local model fixtures. See the validation record for checks and limits. The reconstructed upstream editors and unlicensed references are excluded. Download the Windows ZIP and matching SHA-256, extract the full folder and run `kok.exe`.
