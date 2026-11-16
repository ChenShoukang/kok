# kok v0.16.0 Preview

- Agent 需求判断、工具选择、交付验收、来源与成果检查迁到 TypeScript；Deep Agents / LangGraph 模型循环保持，C# 收敛为桌面、权限、日志和本地工具宿主。旧任务沿用兼容规则。
- 同一原生会话分支中的“继续优化”可接续实际工作区或作品；不继承旧审批、验证和请求额度。
- 新增 review_sources、inspect_document、inspect_presentation；明确区分来源解析、实际阅读、结构检查与事实/视觉审核。被截断的 PPT 页面不再计为完整阅读。
- PPT 支持按原生对象修改位置、尺寸和字号，保留其他页；可审批调用本机空闲 PowerPoint 渲染实际 PNG，不捆绑 Office。
- 命令退出 0 但缺少声明产物时标记失败，避免空的成果入口。
- 详细范围、测试与限制见 [v0.16.0 验证记录](VALIDATION_0.16.0.md)；语言边界与后续迁移见 [TypeScript 主体说明](AGENT_TYPESCRIPT_0.16.0.md)。

完整解压 `kok-0.16.0-win-x64.zip` 后运行 `kok.exe`，保留 `runtime` 目录。默认新任务的业务主体进入 TypeScript；Windows 桌面、权限、任务日志和真实系统工具保留 C#，已有任务继续使用兼容规则。

实际渲染需本机已安装且空闲的 PowerPoint，每次批准最多 8 页。Office 不随包分发；不接管个人正在使用的会话，不支持宏、OLE 或外部资源包。`rendered=true` 只表示已有真实图片，`visualReviewed=false` 表示尚未完成视觉评审。结构检查不代表事实和专业视觉质量已经通过。

修复长任务重复保存历史完整检查点导致过早达到大小上限的问题。文档检查按汉字、非空白字符和英文单词分别返回原生实测值及当前目标是否达标，减少重复改写。

照片、视频、音乐、表格和通用电脑操作继续按[详细计划](PROJECT_PLAN_0.14_TO_1.0.md)实施。

## English

- Moves task requirements, tool routing, delivery acceptance and source/artifact inspection into TypeScript. Deep Agents / LangGraph remains the model runtime; C# retains desktop, permissions, journaling and local tool adapters. Existing tasks keep their compatibility behavior.
- Resolves same-branch follow-ups from actual native task records without copying old approvals or verification.
- Adds source-reading status and saved document/PPTX inspection. Clipped presentation text no longer proves full reading.
- Adds targeted native PPT geometry/font edits and optional approved PNG rendering with an installed, idle PowerPoint. Office is not bundled.
- Treats a missing declared command output as failure even when the process exits zero.
- See [validation](VALIDATION_0.16.0.md) and [migration boundaries](AGENT_TYPESCRIPT_0.16.0.md).

Extract the complete ZIP including runtime. Rendering requires an installed, idle PowerPoint. Office is not bundled. Structural checks and rendered pages do not certify factual or visual quality. Restart checkpoints retain the current state and pending calls without accumulating every historical full state. Document reviews report the requested native length unit to avoid unnecessary rewrites.
