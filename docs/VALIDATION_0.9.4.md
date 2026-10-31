# v0.9.4 验证与发行记录

[项目首页](../README.md) · [扁平界面说明](FLAT_UI.md) · [界面验证](INTERFACE_VALIDATION.md) · [English summary](#english-summary)

验证日期：2026-11-01（Asia/Taipei）。v0.9.4 是界面改版版本：统一扁平视觉、分组导航、文件卡片/列表、阅读与媒体工具栏和逐页布局检查。数据格式、原生桥接与 Agent 能力边界不变；真实模型请求总账保持 **359**，本版**没有新增真实模型调用**。

## 本机确定性验证

- 原生回归：**542 项通过**（`build.ps1 -Test`）。
- `npm run check`、`npm run check:windows`、文档链接检查、版本契约检查通过。
- 25 组界面/回归桌面套件：**25 / 25 通过**（ui、features、translation、i18n、drop、workspaces、video-layout、menus-covers、organizer、companion、private-space、agent-interactions、agent-reliable、modules、documents、warehouse、viewer-shell、library-services、reading-flow、photo-tools、presentations-agent、interface-usage、flat-design、interface-audit、minimal）。
- 逐页截图：**120 张**，覆盖主页面、空间四种视图、摘录/智能集合/配方、文件预览、设置、命令、私密与模式页面；1440×1000、960×720、650×520，中英文与深浅主题。页面/控件越界检查与运行时错误均为 **0**；画布内允许平移的内容单独记录。
- 摘要记录：[interface-flat-ui-0.9.4.json](validation/interface-flat-ui-0.9.4.json)；逐页截图画廊与联系表保留在本地 `test-output/interface-audit/`（不随源码提交）。

## 本机发布包

- `kok-0.9.4-win-x64.zip`，**31,832,283 bytes**，SHA-256 `6baa9745a90b8afededd76da673180b692799fbb045c561b52a7c5207e53f24d`。
- 包级门禁：**14 / 14 套件通过**（原 11 组 + flat-design + interface-usage + interface-audit），针对 ZIP 解压后的 `kok.exe` 运行；记录见 [package-0.9.4-local.json](validation/package-0.9.4-local.json)。
- 源码指纹：`af714a387a2be1209ddc03b3d51c78b251f97ff39291822e88f2654a513b7f01`（435 个文件）；运行目录指纹：`c428a4cca092b9b482f6d209c489d6e3d09d976d26c2f6ca164e97ee4c7ef8a6`（366 个文件）；`kok.exe` SHA-256 `8a176e0f8e79f0250beec6667d702340a3d5ae653cf1e573e08149deab16da1a`。

## 本版修复

- organizer 英文切换：组织页标记在插入前本地化，覆盖后台刷新重绘；测试等待 i18n 目录并处理异步重绘。
- documents 私有文件夹步骤：切换前等待残留弹层/编辑器关闭并重新加载渲染器，消除偶发的 New document 停留与文件占用。
- interface-audit：创建空间后等待数据落库，再选择并截图。
- 文档不再链接未跟踪的 `test-output` 产物（CI `check-docs` 阻断项）。
- 新界面套件纳入 `tools/test-package.ps1` 发布门禁（14 组）。

## 云端发布

云端 [run 41225192571](https://github.com/ChenShoukang/kok/actions/runs/41225192571) 通过 Windows 2022 / 2025 构建、原生测试、14 组包级桌面套件、隐私/依赖审计与发布作业。公开 [v0.9.4 Preview Release](https://github.com/ChenShoukang/kok/releases/tag/v0.9.4) 的 `kok-0.9.4-win-x64.zip` 为 **32,222,039 bytes**，SHA-256 `852314319d475aa21b0eff1287817e3e04b79d036bb952f92b6b43cf76abeebb`；独立下载后的哈希、随附 `.sha256` 与 GitHub asset digest 一致。记录见 [release-0.9.4-download.json](validation/release-0.9.4-download.json)。

## English summary

v0.9.4 is a UI refresh release: a flat visual system, grouped navigation, unified file cards/lists, and consistent reading/media toolbars. Deterministic coverage is 542 native checks, 25 local UI/regression desktop suites and a 14-suite package gate; 120 screenshots were reviewed with zero geometry or runtime findings. The real-model ledger stays at 359 with no new model calls. Local candidate ZIP: `kok-0.9.4-win-x64.zip`, 31,832,283 bytes, SHA-256 `6baa9745a90b8afededd76da673180b692799fbb045c561b52a7c5207e53f24d`.
