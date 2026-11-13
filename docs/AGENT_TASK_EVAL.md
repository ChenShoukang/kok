# Agent 30 项任务评测基线

[项目首页](../README.md) · [文档目录](README.md) · [v0.9.8 验证记录](VALIDATION_0.9.8.md) · [v0.9.2 真实模型补充验证](VALIDATION_0.9.2_LIVE.md)

本页定义 kok Agent 的 **30 项固定任务样本集**、离线校验器和真实模型评测入口，用来在宣称“省 token / 省费用”之前先建立可复现的基线。样本集与评测器已经落地，并在 2026-11-01 用用户授权的 API 预算完成了**改造后（v0.9.8）与改造前（v0.9.5 交付包）两轮同批 30 项真实模型评测**；项目请求总账 359 → 549。本页记录两轮结果、对比口径与解读边界；单轮结果只作起点，不构成泛化结论。

## 首次真实基线（2026-11-01）

模型 `deepseek-chat`（`https://api.deepseek.com`），合成资料库，30 项样本一轮跑完：**26/30 通过（86.7%）**，路由匹配 30/30，本地 8 项全部零模型请求，无回退、无错路由。

| 类别 | 样本 | 通过 | 请求 | 输入 tokens | 输出 tokens | 缓存 tokens |
| --- | --- | --- | --- | --- | --- | --- |
| 本地确定性 | 8 | 8 | 0 | 0 | 0 | 0 |
| 生成 | 10 | 9 | 38 | 242,619 | 12,096 | 162,304 |
| 证据问答 | 7 | 6 | 17 | 63,300 | 5,063 | 25,344 |
| 复杂 / 多步 | 3 | 2 | 17 | 110,653 | 5,768 | 77,952 |
| 需补充信息 | 2 | 1 | 4 | 14,560 | 971 | 9,472 |
| **合计** | **30** | **26** | **76** | **431,132** | **23,898** | **275,072** |

- 全部 30 项引用可回溯（该轮记录名为 `citationsValid`，v0.12.1 起更名为 `citationIdsResolve`，含义不变：来源 id 可解析且在范围内）；本地 8 项命中数与资料库独立统计一致；无未授权写入；每个模型任务的创作文件在完成后可撤销并恢复资料库。
- 4 项未达验收：`gen-note-from-docs` 与 `complex-long-task-note-followup` 在请求上限（6 / 8 次）处以 `limited` 结束——任务已完成读取、写入与引用，但多步“读→写→回读→核对”消耗达上限；`qa-pdf-second-page` 以 `needs_input` 结束（模型提示缺信息，未伪造答案）；`needs-pdf-contact-info` 状态为 `needs_input`，但回答未命中评测器的“未提供/未知”措辞判定（诚实性措辞检查偏严）。
- 费用：按记录的 4 / 16 元每百万 token 保守单价约 **¥2.11**；按 DeepSeek 公开价（输入缓存命中 ¥0.5/M、未命中 ¥2/M、输出 ¥8/M）估算约 **¥0.64**。本轮两轮评测合计 190 次请求（含评测器验证），项目总账更新至 **549**（见[请求总账](validation/live-ledger-0.9.8.json)）。
- 密钥只通过用户提供的凭据文件写入隔离测试 profile 的 DPAPI 配置，运行结束即清除（`keyCleared:true`），报告与仓库不含密钥；完整报告见[机器可读记录](validation/agent-live-0.9.8.json)。
- 首轮运行暴露并修正了两个评测器缺陷：选中资料列表被传成对象而非 id（导致模型任务无法启动），以及“打开成果”判定错误依赖始终存在的按钮；修正后以 2 项子集复测确认 `opened` 计量正常（未计入上表的 30 项结果）。

## 改造前后对比（同一 30 项样本，2026-11-01）

方法：从 `dist/kok-0.9.5-win-x64.zip` 解出改造前的 v0.9.5 交付包，用**同一份 30 项样本、同一模型 `deepseek-chat`、同一预算（14000）与每类请求上限、同一计费口径**再跑一轮；两轮均为合成资料库、用户授权预算、密钥用后清除。改造前的构建没有本地任务路由与新结果卡片，因此 8 个本地样本按预期回退到模型路径（记录为 `fallback`），这正是“改造前”的真实行为。

| 指标 | v0.9.5（改造前） | v0.9.8（改造后） | 变化 |
| --- | --- | --- | --- |
| 验收通过 | 26/30 | 26/30 | 持平（失败项不同，单轮随机） |
| 本地 8 项 | 0 本地 / 8 回退，21 请求，131,833 输入 tokens | 8 本地 / 0 回退，0 请求 | **省 21 请求、131.8k 输入 tokens** |
| 仅模型任务 | 79 请求，434,753 输入 tokens | 76 请求，431,132 输入 tokens | −3.8% 请求 / −0.8% 输入 |
| 总请求 | 100 | 76 | **−24.0%** |
| 输入 tokens | 566,586 | 431,132 | **−23.9%** |
| 缓存命中 tokens | 419,456 | 275,072 | −34.4% |
| 输出 tokens | 25,843 | 23,898 | −7.5% |
| 保守费用（4 / 16 元每百万） | ¥2.68 | ¥2.11 | **−21.4%** |
| 公开价估算 | ¥0.71 | ¥0.64 | −9.9% |
| 总耗时 | 202.5s | 177.3s | −12.4% |

结论与边界：

- **确定的节省来自本地任务分流**：8 项显式只读请求由 21 次模型请求变为 0 次，这一项贡献了总量约 −24% 的请求与输入 token。模型任务本身的请求数与输入 token 基本持平（79→76、434.8k→431.1k），说明片段级读取、覆盖提示与稳定前缀**没有带来额外的上下文膨胀**，但单轮数据也不足以证明模型任务本身变便宜。
- 通过数持平：改造前失败 `local-list-recent-three-days`、`gen-pdf-notes`、`gen-video-subtitle-note`、`qa-pdf-second-page`；改造后失败 `gen-note-from-docs`、`complex-long-task-note-followup`、`qa-pdf-second-page`、`needs-pdf-contact-info`。两轮失败集合不同，属单轮随机与请求上限效应，不能据此判断正确率升降。
- 两轮都只有一轮且样本固定 30 项；要给出稳定节省区间，仍需重复多轮并固定模型参数与提示。改造前报告见[机器可读记录](validation/agent-live-0.9.8-before.json)。

## 为什么先测量再优化

本地任务分流、简化首屏、结果卡片等改动会同时影响正确率、引用可追溯性、token、费用、耗时和首次使用体验。只看“请求变少”或“回答看起来更好”会把幻觉、漏引用、重复读取和确认流程退化掩盖掉。因此先固定 30 项贴近真实用法的任务、固定验收口径和计量字段，在**同一批样本**上完成“改造前基线 → 改造后对比”，才有资格讨论节省。

评测分两步，互不替代：

1. **离线校验器**（无应用、无网络）：校验样本数量、分布、枚举、路由意图、覆盖项和字段一致性。
2. **可选真实模型评测**（显式 opt-in、用户授权预算）：在隔离测试 profile 中驱动真实桌面窗口，记录每项任务的正确性、引用、token、费用、耗时、重试、重复读取和首次使用信号。

## 30 项样本

样本定义在 `tests/agent-task-samples.cjs`，导出 `{version:1,samples:[…]}`，共 30 项，分布为 **本地 8 / 生成 10 / 证据问答 7 / 复杂 3 / 需补充信息 2**。本地样本全部是显式动词 + 显式约束的只读请求，可由原生 `agentLocalPlan` 路由在**零模型请求**下回答；生成、问答、复杂与缺失信息样本走模型路径。覆盖至少 2 个 PDF 任务、2 个视频/字幕任务、2 个多文件对比、2 个批量元数据任务和 1 个纯资料库统计。

### 本地确定性任务（8）

| id | 任务 | 路由 | 验收点 |
| --- | --- | --- | --- |
| local-count-videos | 统计资料库视频数量 | 本地 | 数量与独立统计一致，0 请求 |
| local-count-pdf | 统计资料库 PDF 数量 | 本地 | PDF 数量一致，0 请求 |
| local-list-collection-travel | 列出旅行分类资料 | 本地 | 分类「评估·旅行」3 份，条目可点开 |
| local-list-recent-three-days | 列出最近三天导入 | 本地 | 最近时间窗解析正确 |
| local-collections-overview | 查看分类及各分类数量 | 本地 | 每个分类名称与数量出现 |
| local-tags-design | 查找设计标签资料 | 本地 | #设计 标签分布与预置数据一致 |
| local-kinds-distribution | 统计各类文件数量 | 本地 | 各类型数量之和等于资料总数 |
| local-count-collection-travel | 统计旅行分类数量 | 本地 | 分类数量一致，0 请求 |

### 生成任务（10）

| id | 任务 | 路由 | 验收点 |
| --- | --- | --- | --- |
| gen-summary-selected-docs | 总结所选文档要点 | 模型 | 结果存在、无未授权写入 |
| gen-pdf-notes | PDF 阅读要点笔记 | 模型 | 新 Markdown 笔记可打开（.md） |
| gen-video-subtitle-summary | 视频字幕摘要 | 模型 | 基于字幕结果，不修改原文件 |
| gen-note-from-docs | 资料整理成笔记 | 模型 | 新笔记可打开并引用来源 |
| gen-presentation-five-pages | 生成五页演示文稿 | 模型 | 新演示文稿可打开（.pptx） |
| gen-compare-two-versions | 比较两版旅行计划 | 模型 | 两版预算/日期差异，引用两份来源 |
| gen-organize-recent-proposal | 建议整理最近资料 | 模型 | 只提建议，确认前不改动 |
| gen-batch-tags-proposal | 批量添加标签建议 | 模型 | 先出现确认卡片，确认后才写入并撤销 |
| gen-batch-collection-proposal | 批量移入分类建议 | 模型 | 确认前分类不变，写入可撤销 |
| gen-video-subtitle-note | 视频字幕时间点笔记 | 模型 | 带时间点的新笔记可打开 |

### 证据问答（7，必须引用来源）

| id | 任务 | 路由 | 验收点 |
| --- | --- | --- | --- |
| qa-travel-budget-unknown | 旅行预算与住宿问题 | 模型 | 引用来源，住宿费用说明为未提供 |
| qa-pdf-second-page | PDF 内容与页码引用 | 模型 | 引用 PDF 来源并给页码 |
| qa-compare-version-budget | 两版预算差异问答 | 模型 | 1200 / 1800 / 差额 600，分别引用 |
| qa-plant-watering | 植物记录问答 | 模型 | 2 盆、7 天一次，引用来源 |
| qa-release-checklist | 发布检查清单问答 | 模型 | 检查项逐条引用来源 |
| qa-reading-plan-dates | 阅读计划日期问答 | 模型 | 日期与任务对应，逐条引用 |
| qa-travel-preparation | 旅行准备清单问答 | 模型 | 摄影装备清单 + 未知项说明 |

### 复杂 / 多步（3）

| id | 任务 | 路由 | 验收点 |
| --- | --- | --- | --- |
| complex-delegation-two-docs | 委派子任务汇总两份资料 | 模型 | 一次只读委派 + 来源核对 |
| complex-long-task-note-followup | 长任务：总结后写笔记并读回核对 | 模型 | 计划、写入、读回核对，原文件不变 |
| complex-pdf-video-presentation | PDF 与视频字幕综合演示 | 模型 | 跨两类来源的 3 页演示文稿（.pptx） |

### 需补充信息（2，不得编造）

| id | 任务 | 路由 | 验收点 |
| --- | --- | --- | --- |
| needs-hotel-price | 询问未提供的酒店价格 | 模型 | 明确说明未提供，以需要补充信息结束 |
| needs-pdf-contact-info | 询问 PDF 未包含的联系方式 | 模型 | 明确说明缺失，不编造，以需要补充信息结束 |

每项样本还包含 `fixture`（合成资料说明，不含真实路径/个人数据）、`acceptance`（结果存在、成果可打开、是否必须引用、无无依据声明、可选成果后缀）、`metrics` 与 `noviceChecks` 固定字段。

## 指标定义

报告格式为 `kok.agent.samples` v1，逐样本记录 `passed`、`checks`、路由与下面这些度量；聚合部分给出总数与按类别汇总。

| 指标 | 含义 | 来源 |
| --- | --- | --- |
| inputTokens / outputTokens / cachedTokens | 模型实际用量（服务商返回；无用量时按估算并标记 `estimated`） | `agentTaskRead().usage` / 请求指标 |
| cost | 按任务配置的输入/输出单价估算的费用；未配置单价时任务 `cost=0`，不代表服务商免费 | `usage.cost`（由 `KOK_LIVE_INPUT_RATE` / `KOK_LIVE_OUTPUT_RATE` 启用） |
| requests | 该任务消耗的模型请求数；本地样本应为 0 | `usage.requests` |
| retries | 协议修复轮次 + HTTP 请求重试（`attempt > 1`） | `usage.repairs` + `metrics[].attempt` |
| durationMs | 提交到任务终态的实际耗时；另存每请求耗时 | 评测器计时 + `metrics[].durationMs` |
| duplicateReads | 相同读取工具 + 相同参数被再次调用的次数（含宿主复用结果的情况） | `agentTaskRead().messages` |
| reusedReadRefs | 上述重复读取中由宿主直接复用旧结果（未重新读取）的次数 | `resultEventId` 复用 |
| opened | 评测器通过真实查看器打开成果或来源成功的次数 | 实际打开 `#close-viewer` |
| artifactOpenable | 新建文档/演示文稿存在、文件在磁盘上、后缀符合预期 | 库条目 + 文件系统 |
| citationIdsResolve（旧名 citationsValid） | 要求引用的样本至少引用 1 个真实来源，全部来源 id 可解析且在本次资料范围内；**不证明来源支持结论** | 消息 `sourceIds` |
| writeClaimsMatchWrites（旧名 noUnsupportedClaims） | 只比对“已保存/已创建/已写入”等写入声称与实际 applied 写入是否一致；**不是事实核验** | 执行回执 + 答案文本 |
| factsPresent / forbiddenRespected | v0.12.1 新增：样本 acceptance.facts 每条须出现、forbidden 每条不得出现（字面包含，非语义判断） | 答案/本地卡/成品正文 |
| completedOutcome | v0.12.1 新增：生成/问答/复杂任务必须 completed；partial 单独统计，不再计入完整通过 | `agentTaskRead().status` |
| novice | 3 个首次使用检查通过数（见下） | 报告 `noviceChecks` |
| 路由 | `actualRoute` 记录实际走本地还是模型；`fallback` / `misrouted` 分别标记本地样本回退模型、模型样本被本地误判 | `agentTaskStart`/`agentLocalPlan` 实际调用 |

首次使用检查：

- `startsWithoutHelp`：首屏即可输入并发送，高级设置折叠在 `#agent-advanced` 内。
- `confirmsOnlyWhenNeeded`：需要修改时先出现确认卡片，确认前资料库元数据不变；只读任务不出现写入。
- `findsResult`：结束后的结果卡片（`[data-agent-result]` / `[data-agent-local]`）在流中可见。

## 如何运行

### 离线校验器（默认、无网络）

```powershell
node tests/agent-task-samples.cjs
```

校验器不启动应用、不调用模型、不读写资料库，只做断言并打印样本表和覆盖统计；可用于日常检查和 CI。

### 可选真实模型评测（显式 opt-in，需用户授权预算）

入口为 `tests/agent-live-samples.cjs`。没有 `KOK_LIVE_AI=1` 与 `KOK_LIVE_CONFIG` 时它只打印一行 `SKIPPED: live evaluation is opt-in …` 并以 0 退出，不启动浏览器、不发起任何模型请求，可安全放在默认测试里。

授权后的完整流程（真实密钥只通过本地配置文件进入隔离 profile，评测器本身不读取密钥）：

```powershell
$env:KOK_LIVE_AI='1'
$env:KOK_LIVE_CONFIG='X:\private\ai-settings.json'   # 已加密的 AI 配置，由授权人准备
$env:KOK_LIVE_LEDGER='test-output\live-ledger.txt'    # 可选：累计请求总账，只读
$env:KOK_LIVE_SAMPLES='local-count-videos,qa-pdf-second-page'  # 可选：只跑子集
$env:KOK_LIVE_MAX_REQUESTS='60'                       # 可选：全局请求上限
$env:KOK_LIVE_INPUT_RATE='4'                          # 可选：每百万输入 token 单价（与输出单价同时设置才计费）
$env:KOK_LIVE_OUTPUT_RATE='16'                        # 可选：每百万输出 token 单价
.\tools\test-desktop.ps1 -Suites agent-live-samples
Remove-Item Env:KOK_LIVE_AI,Env:KOK_LIVE_CONFIG,Env:KOK_LIVE_LEDGER,Env:KOK_LIVE_SAMPLES,Env:KOK_LIVE_MAX_REQUESTS,Env:KOK_LIVE_INPUT_RATE,Env:KOK_LIVE_OUTPUT_RATE
```

前置条件与集成点：

- `tools/test-desktop.ps1` 已在启动隔离 profile 前把 `KOK_LIVE_CONFIG` 复制为 profile 内的 `ai-settings.json`（`agent-live-quality` 与 `agent-live-samples` 两组 opt-in 套件使用同一入口）。
- 脚本会用 `--debug --test-silent` 启动真实窗口；评测器通过 CDP（`http://127.0.0.1:9225`）连接，先导入 kok 自带原创示例（PDF/视频/字幕/图片/音乐/文档），再创建固定的合成 Markdown 资料，然后逐项执行 30 个样本。
- 报告写入 `test-output/agent-samples/<时间戳>/report.json`；运行结束清除隔离 profile 的 AI 配置并删除 `ai-settings.json`，报告记录 `keyCleared`。
- 报告只含合成资料、匿名 id、计数与截断后的回答预览，不含密钥、完整提示词或个人路径；浏览器页面侧的外部网络请求会被拦截，模型请求由原生宿主按配置发出。

预算与时间估算（一次完整 30 项，基于 v0.9.2 真实评测的单请求用量）：

| 类别 | 样本 | 每项请求上限 | 期望请求（本地路由生效） |
| --- | --- | --- | --- |
| 本地 | 8 | 4（仅回退保护） | 0 |
| 生成 | 10 | 6 | 60 |
| 证据问答 | 7 | 6 | 42 |
| 复杂 | 3 | 8 | 24 |
| 需补充信息 | 2 | 6 | 12 |
| **合计** | **30** | — | **约 138** |

- 期望：约 138 次模型请求、约 60–70 万输入 token（含缓存命中）、约 4–5 万输出 token；按公开价典型值约 **¥1–2**，按保守上界（输入 ¥4/M、输出 ¥16/M）约 **¥3.5**。
- 最坏：8 个本地样本全部回退模型，绝对请求上限 **170**；保守费用上界约 **¥4**。若出现鉴权/配额/网络/预算类错误，评测器会停止后续样本。
- 时间：本地样本通常 1–2 秒；模型样本约 20–60 秒，完整一轮约 **15–30 分钟**，超时上限更久。实际金额以服务商账单为准。

## 费用与凭据边界

- 真实模型评测**只在用户明确授权预算、并由授权人提供配置时**运行；默认检查和 CI 永不执行。
- 评测器不读取、不打印、不入库 API 密钥；密钥只存在于授权人事先准备、由测试脚本复制进隔离 profile 的加密配置中，结束后清除。
- 报告写入 `test-output/`（已被 Git 忽略）；资料全部为 kok 自带原创示例和合成 Markdown，不使用真实用户文件。
- 项目保留真实模型请求总账（[v0.9.2 总账](validation/live-ledger-0.9.2.json) · [v0.9.8 总账](validation/live-ledger-0.9.8.json)，累计 549 次）。评测器不自动改写总账；运行后由授权人核对报告中的 `aggregate.requests` 再登记。

## 当前状态（2026-11-01）

- 样本集与离线校验器已完成：30 项分布 8 / 10 / 7 / 3 / 2，覆盖 PDF 5、视频/字幕 5、多文件对比 2、批量元数据 2、纯资料库统计 1；`node tests/agent-task-samples.cjs` 通过。
- 可选评测器已完成并在隔离 profile 中真实运行一次：无环境变量时打印 SKIPPED 并以 0 退出（已验证）；授权运行结果见上节与[机器可读记录](validation/agent-live-0.9.8.json)。
- 本地任务路由、简化首屏和结果卡片已进入本次构建；因此上表就是**改造后**的首次基线。节省比例仍需在未来改造后用同一批样本复测对比，本轮不给出。
- 两个评测器缺陷已修正（选中资料 id 传递、成果打开判定），否则会低估真实结果；修正只影响 `opened` 计量与资料范围，30 项结果来自修正前的第一个完整通过运行，两个子集复测用于验证修正。

## 如何解读节省

只有在**同一 30 项样本、同一模型、同一计费口径**下完成两次完整评测（基线 + 改造后）后才能讨论节省：

1. 先看安全与正确性：`passed`、引用有效性、成果可打开、确认流程、无编造。任何一项退化都不算优化。
2. 再看路由：本地样本 `actualRoute=local` 的比例、`fallback` 数量；本地路由生效时这些样本的 `requests` 应为 0。
3. 再算用量：对比 `aggregate.requests`、`inputTokens`/`outputTokens`/`cachedTokens` 与按配置单价估算的 `cost`；区分“请求变少”和“单价/缓存变化”。
4. 同时检查 `durationMs`、`retries`、`duplicateReads`、`novice`，避免用更多重试或更差体验换 token。
5. 模型输出有随机性，单轮结果只作观察；结论至少基于同一配置下重复两轮，且报告注明模型、构建指纹和日期。

## 已知假设与适配点

评测器按当前工作树中的 Agent 契约编写，UI/原生变更后可能需要同步：

- 结果卡片：`[data-agent-result]`（模型任务，属性值为结果消息 id）与 `[data-agent-local]`（本地任务）；结果消息为 `{role:'result',kind:'task'|'local',status,title,summary,sourceCount,citationCount,artifactId,local,stats?,items?,total?,followups?}`。
- 路由观测：通过包装页面全局 `native` 统计 `agentLocalPlan` / `agentTaskStart` 的调用；本地样本以“出现本地结果且无 `agentTaskStart`”判定 `actualRoute=local`，本地样本回退模型时记为 `fallback` 但继续按通用验收完成评测。
- 高级设置：`#agent-advanced`（折叠即首屏简化）；输入框 `#agent-input`，发送 `#agent-form button[type=submit]`；来源/成果打开沿用 `openItem(id,{play:false})` 与 `#close-viewer`。
- 原生任务快照：`agentTaskRead` 的 `usage`（tokens/requests/repairs/cost/estimated）、`messages[].sourceIds/args/resultEventId/undoChanges`、`metrics[].attempt`、`plan`、`verification`。
- 本地任务契约：`agentLocalPlan` 返回 `{handled,local,kind,title,summary,stats,items,total,truncated,followups}`（未命中时 `handled:false`）；8 个本地样本的数量/条目真值在评测器中按导入后的资料库独立计算。
- 若运行在尚未包含上述改动的旧二进制上：本地样本会回退模型路径（报告如实记录 `actualRoute`/`fallback`），结果卡片选择器缺失会让 `findsResult` 为假，但评测器不会崩溃，仍会输出完整报告——这正是“改造前基线”的预期形态。
