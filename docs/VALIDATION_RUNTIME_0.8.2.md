# v0.8.2 · 运行能力候选验证

[功能与边界](AGENT_RUNTIME_0.8.2.md) · [English guide](AGENT_RUNTIME_0.8.2.en.md) · [首批候选记录](VALIDATION_0.8.2.md)

本地 Preview 后续候选，尚未发布。本页单独记录语义压缩、加密记忆、只读子代理、JavaScript/WASM 隔离计算及工具权限，不沿用首批候选的 326 项 / 六组结果。

## 验证方法

```powershell
npm run check
npm run check:windows
npm audit --omit=optional --audit-level=info
.\package.ps1
.\tools\test-package.ps1
```

打包默认运行原生测试及源码 / 包隐私模式扫描。包级验证从 ZIP 新建目录解压启动，连续运行 `agent`、`agent-interactions`、`agent-reliable`、`agent-runtime`、`agent-kernel`、`mcp`、`agent-platform` 七组。运行前后校验源码 / 测试、EXE 和运行资源指纹，不把不同构建或分次定向结果拼成一次通过。

新增检查包括：记忆加密、范围、来源变更与冲突安全撤销；子任务不复制父会话、不能写入或继续派生、父子共用请求及费用限制；压缩保留调用对与准确回查入口；WASM 实际计算、禁用宿主接口、死循环、内存与输出上限；界面关闭竞态、500 / 1280 像素、中英文、纯净与私密边界。

## 真实模型：两项定向检查

2026-10-30，DeepSeek `deepseek-chat`，使用合成资料及独立加密 profile，**2/2 通过，8 次请求，上限 14 次**：

| 案例 | 实际结果 |
| --- | --- |
| 隔离计算 | 2 次请求，确认后运行 `run_code`，返回并核对 1800−1200 = 600 |
| 只读委派 | 6 次请求，完成一个子任务，引用两份来源；包含一次参数修复，父子用量合计未超预算 |

这两项验证工具执行和有限结果断言，没有重新验收所有旧案例，也没有对语义摘要或记忆质量作真实模型评分。首批六例 3/6 与后续失败记录仍保留在[历史验证](VALIDATION_0.8.2.md)，不因这次通过而改写。

报告不含密钥、提示词、回答或本地路径，凭据清理已确认。单价未配置，记录 `cost: 0` 不代表免费。[两项实测与指纹](validation/agent-runtime-eval-0.8.2.json)保留各自用量。真实测试与最终包级模拟回归分别记录运行文件指纹；两者源码 / 测试指纹相同。

## 最终包记录

2026-10-30（Asia/Taipei）实际完成：

| 检查 | 结果 |
| --- | --- |
| 默认 `package.ps1` 原生自检 | **346 项通过**，未跳过测试 |
| 最终 ZIP 解压桌面回归 | **同一次 7/7 通过**；源码、测试和运行文件指纹在运行前后不变 |
| 语法、架构、文案与文档 | `npm run check` 通过，102 个 UI 扩展、2,243 条文案；486 个本地文档目标有效（补证据后的链接另校验） |
| Windows PowerShell 5 | 12 个脚本语法通过 |
| 隐私模式扫描 | 暂存目录 / ZIP 各 513 个文件通过 |
| 依赖公告 | `npm audit --omit=optional --audit-level=info` 返回 0 个已知漏洞 |

包级脱敏证据：[同包汇总与指纹](validation/package-0.8.2-runtime.json)。完整本地记录位于 `test-output/package-e1c437813b9948a686a44b17da163932/` 的 `archive.json`、`desktop/run.json` 和 `desktop/results.json`；原生日志为 `test-output/agent-platform-final-package.log`。

测试包 `kok-0.8.2-win-x64.zip` 为 **22,771,460 字节（21.72 MiB）**，SHA-256：

```text
d2c501587a343dd3d5e659437002ad4f7c9f14a829b84c2782eb6f40229813db
```

ZIP 保持测试时原样；本页最终结果与汇总在测试后单独保存，不把文档补充冒充为再次打包或测试。`dist/kok-0.8.2-validation.json` 是随附汇总，旧首批包证据保留在原路径。

## 边界

上下文量是保守 UTF-8 上界，不是精确 tokenizer；记忆是可审核的本地词项检索，不是向量记忆平台。子代理限一层只读、逐次批准。隔离执行是受限 JavaScript/WASM，不覆盖任意 shell / Python 或 MCP 进程。第三方 MCP 兼容矩阵、远程认证、操作系统容器和目标语义自主验收尚未实现。

本地 Windows / WebView2 验证不等于多设备覆盖。新七组 CI 配置尚未在远端运行。隐私模式扫描不等于全面安全审计。自动发布关闭，GitHub 的 v0.8.1 下载保持原样。

## English

This follow-up local candidate is validated separately from Preview 1. **346 native checks and all seven packaged Agent suites passed** on October 30, in one invocation against an extracted ZIP with unchanged source/test and runtime fingerprints. See [package evidence](validation/package-0.8.2-runtime.json). The archive is unchanged; post-test prose and evidence are provided separately. Package size: 22,771,460 bytes. Updated remote CI has not run.

Two opt-in DeepSeek checks passed, using **8 requests out of a maximum of 14**: approved isolated arithmetic (2), and approved read-only delegation with two sources (6, including one repair). See [live evidence](validation/agent-runtime-eval-0.8.2.json). They do not replace the previous six-case quality evaluation, assess all memory/summary semantics, or establish general autonomy. Credentials were cleared. Zero recorded cost reflects unconfigured prices.
