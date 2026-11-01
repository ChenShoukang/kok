# v0.9.5 验证与发行记录

[项目首页](../README.md) · [扁平界面说明](FLAT_UI.md) · [界面参考](INTERFACE_REFERENCES.md) · [English summary](#english-summary)

验证日期：2026-11-01（Asia/Taipei）。v0.9.5 是紧凑导航与动效质量修正版：窄窗口只保留四个主入口和「更多」，提高图标/提示/小标签的可读性，修复面板动画中断跳变和菜单焦点样式。数据格式、原生桥接与 Agent 能力边界不变；真实模型请求总账保持 **359**，本版**没有新增真实模型调用**。

## 本机确定性验证

- 原生回归：**542 项通过**（`build.ps1 -Test`）。
- `npm run check`、`npm run check:windows`、文档链接检查与版本契约检查通过。
- 界面/回归桌面套件：**25 / 25 通过**（与 0.9.4 相同的 25 组矩阵；library-services 在紧凑宽度通过「更多」进入资料检索与备份）。
- 逐页审计：**120 张截图**；页面/控件越界与运行时错误均为 **0**；摘要见 [interface-flat-ui-0.9.5.json](validation/interface-flat-ui-0.9.5.json)。

## 紧凑导航与动效

- ≤820px：可见控件为 **今天 / Agent / 全部文件 / 空间 / 更多** 加底部设置；无侧栏滚动条；宽屏侧栏不变。
- 图标 **20px**、点击目标 **40px**、图标提示 **12px**、菜单项 **13px**、小组标签/计数 **12px**。
- 「更多」从顶部栏下方展开，只有目的地名称和分隔线；私密文件夹、收藏、最近打开、配方、回顾、知识图谱、新建分类仍可达。
- 菜单首项用整行高亮而不是额外外框；补充 `aria-haspopup`，Escape 返回焦点。
- 面板退出从当前 transform 开始，不再跳回 0；导航指示条只动画 transform/opacity（240ms），减少动效下禁用 transition。

## 实测预览

- 菜单打开：最大帧间隔 **11ms**，超过 20ms 的帧 **0**。
- 预热后的路由切换：最大帧间隔 **5.7ms**；首次进入 Agent 的首次 DOM 构建帧 **38.8ms**，后续不再出现。
- 减少动效模式：运行中动画 **0**。

## 本机发布包

- `kok-0.9.5-win-x64.zip`，**31,912,551 bytes**，SHA-256 `fb1fe329ffa05fd91396953ece41955f876da9c668266e771db626f107382e81`。
- 包级门禁：**14 / 14 套件通过**（原 11 组 + flat-design + interface-usage + interface-audit），针对 ZIP 解压后的 `kok.exe` 运行；记录见 [package-0.9.5-local.json](validation/package-0.9.5-local.json)。
- 源码指纹：`3b15e7764581aec0c3cac69234caa4aeeae2c622b52a5eecbfed216ca6a783b8`（435 个文件）；运行目录指纹：`44bd0483d4ed0bbba92a449d48e9570c0177f59e9c9f6941934cd6cb4eb771e6`（366 个文件）；`kok.exe` SHA-256 `638ac52500150b38de23f1f65db6ddcbd57ed0c47a0942a0054cccc2b2b12a8e`。

## 云端发布

云端 Windows 2022 / 2025 构建、原生测试、14 组包级桌面套件、隐私/依赖审计与发布作业以 GitHub Actions 的实际状态为准；发布后的下载校验记录写入 `docs/validation/release-0.9.5-download.json`。

## English summary

v0.9.5 is a compact-navigation and motion-quality release: the ≤820px rail keeps four primary destinations plus one overflow, with 40px targets, 20px icons, 12px tooltips and calmer menu focus. Panel exits preserve their current transform, and the navigation indicator animates only transform/opacity with reduced-motion support. Local validation covers 542 native checks, 25 UI/regression suites and 120 reviewed screenshots (zero geometry/runtime findings); the 14-suite package gate is recorded with the local candidate ZIP. The real-model ledger stays at 359 with no new model calls.
