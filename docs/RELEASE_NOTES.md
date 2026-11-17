# kok v0.16.2 Preview

- 新增关键结论原文核对：记录当前来源版本、引文位置和实际阅读记录，可确认原句是否存在于已保存的 MD/TXT/DOCX 报告。
- 未读、过期、重复或找不到原文分别报告。支撑、冲突与推论仍明确标为调用者判断；定位和阅读记录不构成事实或逻辑证明。
- 报告必需章节检查独立标题和非空正文，正文提及栏目名称不能代替章节。缺项反馈用于局部修复，旧任务保留原验收版本。
- 新增显式启用的 18 题真实模型完整任务评测，检查实际成果、回读、未改页面及代码验证；边界题单列，累计保留失败和请求账本。
- 实现、实测和剩余限制见 [v0.16.2 验证记录](VALIDATION_0.16.2.md)；后续见[近期执行计划](NEXT_PHASE_0.16_TO_0.18.md)和[总规划 3.0](PROJECT_PLAN_0.14_TO_1.0.md)。

完整解压 `kok-0.16.2-win-x64.zip` 后运行 `kok.exe`，保留 `runtime` 目录。Agent 业务使用 TypeScript 和既有 Deep Agents / LangGraph，Windows 桌面、权限、日志和本地工具保留 C#；本版未切换桌面壳。

实际渲染需本机已安装且空闲的 PowerPoint，每次批准最多 8 页。Office 不随包分发；不接管个人正在使用的会话，不支持宏、OLE 或外部资源包。`rendered=true` 只表示已有真实图片，`visualReviewed=false` 表示尚未完成视觉评审。结构检查不代表事实和专业视觉质量已经通过。

18 项确定性规划检查继续单列，不混入实际模型交付结果。原文定位和结构检查不代表专业成品质量；两轮同题与未见素材的质量验收、整体效率目标仍需继续测量。

照片、视频、音乐、表格和通用电脑操作继续按[详细计划](PROJECT_PLAN_0.14_TO_1.0.md)实施。

## English

- Adds current text quotation checks, source versions, exact locations and native reading ranges, with optional verbatim claim lookup in saved MD/TXT/DOCX reports.
- Stale, unread, repeated and missing quotations remain explicit. Support, contradiction and inference remain caller assessments, not deterministic semantic proof.
- Required report sections need separate headings and nonempty bodies. Missing sections receive targeted repair feedback while old tasks retain their acceptance version.
- Adds an opt-in 18-scenario real-model complete-task evaluator with actual files, read-back, page preservation and code verification, separate boundary outcomes and a persistent cumulative request ledger.
- See [validation](VALIDATION_0.16.2.md), [next-phase execution](NEXT_PHASE_0.16_TO_0.18.md) and [migration boundaries](AGENT_TYPESCRIPT_0.16.0.md).

Extract the complete ZIP including runtime. The TypeScript kernel and C# native host remain. Rendering requires an installed, idle PowerPoint; Office is not bundled. Source location, structural checks and rendered pages do not certify semantic or professional visual quality. Full repeated-round and held-out-material evaluation and an overall efficiency improvement remain further work.
