# v0.10.1 Preview · 验证记录

本版把一级入口按使用场景收敛为 Agent、我的作品、资料库，并保留独立的观看、收听、阅读体验。
不改变数据格式和原生桥接接口。小说章节/设定管理与音视频从零生成不在本版范围内。

## 验证范围

- 静态检查：`npm run check`（界面脚本、架构契约、本地化目录、文档链接、版本契约）。
- 原生检查：`build.ps1 -Test` 编译程序并运行 `kok.Tests.exe` 原生用例。
- 桌面回归：隔离资料库 + 本地模拟服务 + 真实 WebView2，覆盖三区域导航、我的作品、资料库首页、
  Agent 首页、播放条共存、模式切换、窄窗口、中英文和减少动效。
- 交付包复验：`package.ps1` 构建 ZIP 后运行 `tools/test-package.ps1` 的桌面套件。
- 本轮不发起真实模型请求，不使用真实 API 预算。

## 结果（2026-11-02）

- 静态检查：`npm run check` 通过（45 张样式表 → 9 个运行时样式包；架构、模块契约、本地化目录、文档链接与版本契约校验通过）。
- Windows PowerShell 兼容：`npm run check:windows` 通过（16 个 .ps1 文件可被 PowerShell 5.1 解析）。
- 隐私审计：`tools/audit-release.ps1` 通过（643 个待发布文件，无凭据或私有数据模式）；`npm run audit:history` 通过（70 次提交、1786 个 blob）。
- 原生检查：`package.ps1` 编译程序与测试程序并通过 **608 项原生检查**。
- 桌面回归：**15 个套件全部通过**，运行记录 `test-output/full-20261102-145418-22fa9ea3`，套件总耗时 216 秒；
  运行器在结束后校验源文件与运行时指纹一致。套件包括 works-navigation、agent-home-layout、agent、app-modes、
  flat-design、interface-usage、interface-audit、motion-quality、features、private-space、knowledge、
  surface-design、modules、ui、i18n。
- 交付包：`dist/kok-0.10.1-win-x64.zip`，30.41 MB，SHA-256
  `22ab0cfb57d69686606b1e5e11d29037a4b8a13d0409acb27ec019a01ee6b383`。
- 交付包复验：`tools/test-package.ps1` 从 ZIP 运行 **16 个桌面套件全部通过**，记录
  `test-output/package-e9857cc4ca9848bca6b9361aae7cc68e/`（`archive.json` 保存套件列表、源/运行时指纹与 ZIP SHA-256）。
- 本轮未调用真实模型，真实 API 请求账本不变。

## 证据

- 桌面运行记录：`test-output/full-20261102-145418-22fa9ea3/`（含 `run.json`、`results.json`）。
- 新增套件：`tests/works-navigation.cjs`，截图输出在 `test-output/works-navigation/`。
- Agent 首页与播放条共存截图：`test-output/agent-home-refresh/`。
- 交付包与校验和：`dist/kok-0.10.1-win-x64.zip` 及同名 `.sha256`；包级复验记录在
  `test-output/package-e9857cc4ca9848bca6b9361aae7cc68e/`。

## 边界

- “我的作品”按已有资料与编辑草稿推导，作品级原生状态、版本和导出记录留待后续接入。
- 小说作品级大纲、人物设定、章节管理与连续性检查尚未实现。
- 从零生成可播放的音乐或视频需要对应生成引擎，本版只支持已有媒体的剪辑与拼接。
- 观看、收听、阅读依赖 WebView2 / 系统编码与格式支持，边界见[格式兼容性](FORMATS.md)。
