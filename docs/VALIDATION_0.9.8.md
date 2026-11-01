# v0.9.8 Preview · 验证记录

本地交付包：`dist/kok-0.9.8-win-x64.zip`，随附 `.sha256`。完整解压后运行
`kok/kok.exe`。程序产品版本、程序集、包元数据和浏览器扩展统一为 0.9.8。

本版包含界面改版（主题、欢迎页、图标、导航与动画）与 Agent 聚焦改版（首屏、本地任务分流、片段级读取、成果卡片）。原生桥接接口与数据格式保持兼容。能力与边界见 [Agent 聚焦](AGENT_FOCUS.md)，度量口径见 [30 项任务评测基线](AGENT_TASK_EVAL.md)，设计参考和离线图标许可见[界面参考](INTERFACE_REFERENCES.md)。

## 界面改版验证

版本号同步前，界面实现已通过 `npm run check` 及 `motion-quality`、
`flat-design`、`interface-usage`、`features`、`app-modes` 五组桌面套件。
逐帧检查发现并修复菜单退出时的内边距和圆角跳变，随后动效与连续操作复测通过。
已有记录位置见[界面说明](FLAT_UI.md)。

独立画面评审覆盖浅深色资料库、窄窗口工具面板和模式预览；独立操作走查完成
搜索、DOCX 阅读、连续收藏和找回资料。修正重复的图标语义、深色模式差异对比，
并将收藏、最近打开和分类放到工具面板第一排。

新增动效断言覆盖当前帧续接、筛选节点与焦点保留、菜单即时释放输入、退出几何
一致、快速重新打开、排序、窄窗口两套主题及减少动态效果。截图、关键帧与走查
记录位于 `test-output/oil-ui-refinement`。关键帧通过暂停真实动画取样，属于静态
状态证据；单机帧时间采样不代表其他硬件的帧率。

## Agent 聚焦改版验证

- **608 项原生检查通过**（基线 542；新增 45 项本地分流检查与 14 项片段检索 / 读取覆盖 / 目标目录 / 稳定消息前缀检查）。覆盖纯模式与私密文件夹拒绝、本地分流只读性、范围隔离、消息前缀字节级稳定等确定性路径。
- **桌面套件**：`agent-interactions`（首屏高级设置默认折叠、随资料建议、批量摘要、成果卡、本地卡零模型请求与重载保留、500–1280 px、中英文、零外部请求、零页面错误）、`agent`，以及 `agent-reliable`、`agent-runtime`、`agent-kernel`、`agent-platform`、`agent-quality`、`mcp`、`modules`、`features`、`interface-usage`、`app-modes`、`ui`、`flat-design`；`library-services` 与 `i18n` 按新的短侧栏「更多」面板适配后复测通过。
- 关键证据目录：`test-output/full-20261101-172358-fc32b083`（agent-interactions，含 result-card / local-task 截图）、`test-output/full-20261101-172420-79f51579`（agent）、`test-output/full-20261101-172625-d6f9c116`、`test-output/full-20261101-172915-137bd688`、`test-output/full-20261101-173058-466a2b06`。
- 30 项任务样本离线校验通过。

## 真实模型评测（用户授权，2026-11-01）

用户明确授权用同一批 30 项样本做**改造前后两轮**真实模型评测（每轮预算约 ¥1–2）。评测在隔离测试 profile 中进行：密钥由用户提供的凭据文件写入应用 DPAPI 配置，评测器不读取密钥，结束后清除（`keyCleared:true`）；资料全部为 kok 自带原创示例与合成 Markdown，不含真实用户数据。改造后报告见[记录](validation/agent-live-0.9.8.json)，改造前（v0.9.5 交付包）报告见[改造前记录](validation/agent-live-0.9.8-before.json)，请求总账见[总账](validation/live-ledger-0.9.8.json)（359 → 549）。

- 改造后（v0.9.8）：**26/30 通过（86.7%）**；路由匹配 30/30；本地 8 项全部零模型请求、零回退、零错路由；引用可回溯 30/30；无未授权写入。76 次请求，输入 431,132 tokens（缓存 275,072），输出 23,898 tokens；保守单价约 ¥2.11、公开价估算约 ¥0.64。
- 改造前（v0.9.5 包，同批样本同模型）：26/30 通过；100 次请求，输入 566,586 tokens，输出 25,843 tokens，保守约 ¥2.68、公开价约 ¥0.71；8 个本地样本全部回退模型（21 次请求、131,833 输入 tokens）。
- **同批对比**：总请求 100 → 76（**−24.0%**），输入 tokens 566,586 → 431,132（**−23.9%**），保守费用 ¥2.68 → ¥2.11（**−21.4%**），总耗时 202.5s → 177.3s（−12.4%）。节省几乎全部来自本地任务分流（21 请求、131.8k 输入 → 0）；模型任务自身 79→76 请求、434.8k→431.1k 输入，基本持平，说明片段读取与稳定前缀没有带来额外上下文膨胀。
- 4 项未达验收（改造后）：`gen-note-from-docs`、`complex-long-task-note-followup` 在 6 / 8 次请求上限处以 `limited` 结束（读取、写入与引用已完成，但“读→写→回读→核对”超出预算）；`qa-pdf-second-page` 以 `needs_input` 结束（如实提示缺少信息，未伪造）；`needs-pdf-contact-info` 状态为 `needs_input`，但回答未命中评测器的“未提供/未知”措辞判定。改造前失败项不同（`local-list-recent-three-days`、`gen-pdf-notes`、`gen-video-subtitle-note`、`qa-pdf-second-page`），两轮通过数持平，单轮结果不足以判断正确率升降。
- 评测器自身修正：首轮暴露“选中资料 id 传递”和“成果打开判定”两个缺陷，修正后用子集复测确认 `opened` 计量正常；主结果保留首个完整运行，不重复整轮以避免超出授权预算。
- 上述结果是单轮固定样本观察，不等同于通用节省结论：真实模型输出有随机性，稳定区间仍需多轮重复。

## 交付包复验

在项目根目录执行：

```powershell
npm run check
.\package.ps1
.\tools\test-package.ps1 -Archive .\dist\kok-0.9.8-win-x64.zip
```

`package.ps1` 构建程序并执行原生检查，按明确清单打包，并检查目录与 ZIP。
解包测试核对 SHA-256 和产品版本，使用独立资料库启动真实 WebView2，保存源代码
及实际运行文件的指纹。包级验证结果保存在 `test-output/package-*/archive.json`。

本次交付包 `kok-0.9.8-win-x64.zip` 约 30.37 MB，SHA-256 为
`163a9178e53357cf33c11ca5b9822af86bac5551eaa40f5c2863deca2a36052d`；发布隐私审计
检查 637 个文件，未发现凭据或私密数据模式。包内程序产品版本为 0.9.8，含 UI、文档、
示例与浏览器扩展；不含资料库数据、个人配置、缓存或测试程序。

解包复验：在独立资料库中运行 `ui`、`features`、`agent`、`agent-interactions`、
`agent-reliable`、`agent-runtime`、`agent-kernel`、`mcp`、`agent-platform`、
`agent-quality`、`library-services`、`flat-design`、`interface-usage`、
`interface-audit`、`i18n` 共 15 组桌面套件全部通过，核对 SHA-256、产品版本、源文件
与实际运行文件指纹；记录见 `test-output/package-965b0dc2138a486b9caa131ef9ef41e0/archive.json`，
归档副本见[包验证记录](validation/package-0.9.8.json)。

真实资料库、个人配置、缓存和开发测试程序不进入便携包。除上面一次用户授权的 30 项基线外，本版验证使用本地样例与模拟服务，无其他新增收费模型调用。

## 边界

- 片段检索仍为词面 / 本机 embedding 混合，未加重排器；目标相关候选按词面匹配补充，可能漏掉改述。
- 本地分流只覆盖可判定的只读请求，语义改写仍走模型。
- 覆盖数字来自有界提取（正文上限 120k 字符 + 笔记 6k 字符），不是原始文件字节数。
- 成果卡片的来源引用以本机读取结果为准，不保证每个结论都被证据支持。
- 真实模型输出具有随机性；固定样本不等于全部使用场景。
