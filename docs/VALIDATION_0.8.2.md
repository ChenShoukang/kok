# v0.8.2 Preview 1 · 首批候选验证 / Initial candidate validation

[功能与边界](AGENT_0.8.2.md) · [English feature guide](AGENT_0.8.2.en.md) · [v0.8.1 发布验证](VALIDATION_0.8.1.md)

这份记录对应本地 `v0.8.2-preview.1`（`888a199`）候选，不代表后续运行能力改动的验证结果。语义压缩、记忆、子代理与隔离计算另见[后续候选验证](VALIDATION_RUNTIME_0.8.2.md)。模拟服务检查执行机制，真实模型评测检查有限任务的实际表现，两者分开记录。旧记录中跨多个构建的“6/6”不作为同一构建通过的证据。

## 复核方式

```powershell
npm run check
npm run check:windows
.\package.ps1
.\tools\test-package.ps1
```

`package.ps1` 默认执行构建、原生测试、源码 / 包隐私模式扫描，生成 ZIP 与 SHA-256。`test-package.ps1` 检查校验文件，安全解压到新建隔离目录，再直接运行包里的程序。首批候选连续执行 `agent`、`agent-interactions`、`agent-reliable`、`agent-runtime`、`agent-kernel`、`mcp` 六组，不重新编译；后续默认列表增加了 `agent-platform`，不可把旧六组结果当作新七组结果。

每次桌面运行在 `run.json` 记录版本、所请求的组、开始 / 结束时间、源码 / 测试指纹、运行文件指纹与 EXE 的 SHA-256。测试过程中源码或运行文件变化会失败；`results.json` 逐组记录结果。包级 `archive.json` 再绑定 ZIP 校验值。不得把多个目录的定向通过拼成最终通过。

- 原生检查覆盖计划、回执核对、加密重放、撤销 / 改要求后失效、选定资料顺序、HTTP 类型与预算、JSON 回退重试、同任务补充信息，以及真实 stdio 子进程。
- `agent` / `agent-interactions` / `agent-reliable` / `agent-runtime` 覆盖会话、审核、撤销、来源范围、协议兼容、重载 / 重启与显式交接。
- `agent-kernel` 覆盖完成后撤销、手动改源、旧计划失效、需要信息后继续、请求追踪、重试 / 降级、拒绝与预算上限；中英文、深浅色与 500–1280 像素窗口。
- `mcp` 覆盖可视化添加服务、schema 审核、配置加密、两种协议下的参数修复、错误正文、本地结果 / 费用入口、逐项确认、停用、纯净与私密关闭连接。

每组使用原创合成资料、独立 profile、`--debug --test-silent`。媒体静音，模型请求指向本机模拟服务；浏览器外部请求被拦截。测试发现问题后重跑相关组，不通过增加运行次数掩盖失败。

## 真实模型：实际执行，不预报通过

[首次六例报告](validation/agent-eval-0.8.2-initial.json)：DeepSeek `deepseek-chat`，**3/6 通过**，共 20 次请求（上限 24）。事实 / 未知信息、注入案例、缺失信息通过；计划后写入、拒绝修改、版本对比在每例 4 次请求内未完成核对，均诚实显示上限。

排查发现：目录使用最新导入顺序，与“第一份”意图冲突；模型将跳过的回执当成功依据，或只更新计划、忘记同轮核对，导致额外轮次。已保留选择顺序，明确成功证据与拒绝语义，并加入剩余请求和合并计划 / 核对指导。提示改进不等于语义保证，首次失败报告保留。

[三例定向复测](validation/agent-eval-0.8.2-followup.json)实际使用 10 次请求（上限 12）：计划写入因工具参数修复耗尽预算，拒绝修改仍未完成最终核对；对比通过当时的自动断言，但人工发现模型将 1800−1200 算为 500，正确应为 600，故也不作为质量通过。后续评测断言已收紧，要求笔记实际执行、拒绝只提议一次，以及正确的预算差额；没有继续花费 API 去刷通过结果。模拟测试通过不意味着这些模型表现已解决。

真实测试只发送新建的合成资料，未扫描用户目录。凭据仅在内存与隔离的本机加密配置使用，报告确认清除；报告不含密钥、完整提示词、回答或本地文件路径。单价未配置，`cost: 0` 不代表服务商免费；用量为服务商返回的 token。自动断言不是通用语义评分器，注入案例通过也不证明能抵御所有注入。

可选命令（默认和 CI 都不执行）：

```powershell
$env:KOK_LIVE_AI='1'
$env:KOK_LIVE_KEY_FILE='X:\private\model-key.txt'
$env:KOK_LIVE_MODEL='deepseek-chat'
$env:KOK_LIVE_CASES='facts,injection' # 只选所需案例，每例最多 4 次请求
.\tools\test-desktop.ps1 -Suites agent-live-eval
Remove-Item Env:KOK_LIVE_AI,Env:KOK_LIVE_KEY_FILE,Env:KOK_LIVE_MODEL,Env:KOK_LIVE_CASES
```

## 首批候选结果与当时边界

2026-10-30（Asia/Taipei）首批候选结果，归档保留：

| 检查 | 实际结果 |
| --- | --- |
| 默认 `package.ps1` 构建 / 原生自检 | **326 项通过**，未跳过原生测试 |
| 最终 ZIP 解压启动 | **同次 6/6 通过**，版本 0.8.2，源码 / 测试与运行文件在运行前后保持一致 |
| 静态 / 文案 / 文档 | `npm run check` 通过；102 个 UI 扩展、2,170 条文案 |
| Windows PowerShell 5 | 12 个脚本语法通过 |
| 包隐私模式扫描 | 暂存目录与 ZIP 各 494 个文件通过，不等于全面安全审计 |
| npm 依赖公告 | `npm audit --omit=optional --audit-level=info` 返回 0 个已知漏洞 |
| 真实模型 / 远端 CI | 真实模型有上述未解决项；远端新 CI 未运行 |

可提交的无内容证据：[包级汇总与指纹](validation/package-0.8.2.json)。完整本地记录在 `test-output/package-6ea24cc19532412786ba0f7ab3897ef0/` 的 `archive.json`、`desktop/run.json` 与 `desktop/results.json`；原生日志为 `test-output/agent-082-review-package.log`。这是 SHA-256 以 `f98f3c44` 开头的旧包记录，不是 `dist` 下后续重建包的校验依据。测试后的文档更新不冒充为再次测试。

新增桌面 CI 配置尚未在远端运行，不能说云端已绿。CI 只收集汇总、指纹与合成测试截图，不上传 profile、凭据或完整任务内容。源码 / 包的凭据模式扫描不等于全面安全审计。

首批候选只验证合成 stdio MCP 服务，未完成第三方兼容矩阵。MCP 解释器脚本 / 依赖未哈希，无系统沙箱、HTTP/OAuth。当时尚无长期记忆、子代理和语义压缩；后续实现及其边界见[运行能力指南](AGENT_RUNTIME_0.8.2.md)。模型自检与工具回执检查不能独立验收目标语义。

`release.publish=false`；已发布的 v0.8.1 ZIP、tag 与 Release 不因本地开发改变。

## English

The initial six-case DeepSeek evaluation passed **3/6**, using 20 requests with a hard maximum of 24. Failure records are preserved; selection order and request-efficiency guidance were then improved. Do not present the initial run as a pass on the final tree. Live evaluation uses synthetic sources, clears credentials, and reports only check results and usage. No configured prices means zero recorded cost is not a billing claim.

The three-case follow-up used 10 more requests: two did not finish; the comparison passed its old assertions but failed manual arithmetic review (500 instead of 600). Future assertions are stricter. These runs do not establish final-tree model quality.

The initial `v0.8.2-preview.1` candidate (`888a199`) passed **326 native checks and all six packaged desktop suites in one invocation** on October 30 (Asia/Taipei). See the historical [package evidence](validation/package-0.8.2.json), tied to ZIP hash `f98f3c44…`. These results do not validate the later semantic-context, memory, delegation or sandbox changes; see the [subsequent candidate validation](VALIDATION_RUNTIME_0.8.2.md).

Package validation starts the extracted ZIP and runs all six Agent fixture suites in one invocation. Source/test and runtime fingerprints must remain unchanged throughout. `archive.json`, `run.json` and `results.json` bind the results to the archive and executable. The new remote desktop CI job has not run. Receipt checks, limited model evaluations and pattern-based privacy scans do not prove semantic autonomy, comprehensive security or third-party MCP compatibility.
