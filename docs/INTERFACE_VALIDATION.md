# 界面改版验证记录 · 2026-11-01

本次采用独立测试资料库和本地模拟服务，检查修改后的真实 Windows / WebView2 界面。测试不会调用收费模型服务。

## 自动验证

桌面套件：**25 / 25 通过**。逐页截图：**120 张**；几何检查发现 **0 项**；逐页运行时错误 **0 项**。

| 套件 | 最新结果 | 详细日志 |
| --- | --- | --- |
| ui | 通过 | `regression-ui.log` |
| features | 通过 | `regression-features.log` |
| translation | 通过 | `regression-translation.log` |
| i18n | 通过 | `regression-i18n.log` |
| drop | 通过 | `regression-drop.log` |
| workspaces | 通过 | `regression-workspaces.log` |
| video-layout | 通过 | `regression-video-layout.log` |
| menus-covers | 通过 | `regression-menus-covers.log` |
| organizer | 通过 | `regression-organizer.log` |
| companion | 通过 | `regression-companion.log` |
| private-space | 通过 | `regression-private-space.log` |
| agent-interactions | 通过 | `regression-agent-interactions.log` |
| agent-reliable | 通过 | `regression-agent-reliable.log` |
| modules | 通过 | `regression-modules.log` |
| documents | 通过 | `regression-documents.log` |
| warehouse | 通过 | `regression-warehouse.log` |
| viewer-shell | 通过 | `regression-viewer-shell.log` |
| library-services | 通过 | `regression-library-services.log` |
| reading-flow | 通过 | `regression-reading-flow.log` |
| photo-tools | 通过 | `regression-photo-tools.log` |
| presentations-agent | 通过 | `regression-presentations-agent.log` |
| interface-usage | 通过 | `regression-interface-usage.log` |
| flat-design | 通过 | `regression-flat-design.log` |
| interface-audit | 通过 | `regression-interface-audit.log` |
| minimal | 通过 | `regression-minimal.log` |

## 排版检查

本地输出目录：`test-output/interface-audit/`（逐页截图、`report.json`、联系表）与 `test-output/interface-usage/`。

范围包括主导航各页、空数据与有数据的集合、四种空间视图、长中英文文件名、PDF / EPUB / DOCX / Markdown / TXT、照片 / 视频 / 音乐预览、设置各页、模块、私密文件夹、命令、模式选择与首次提示、Inspector 各页。尺寸覆盖 1440×1000、960×720、650×520，并检查中英文和深浅主题。

首轮 99 张截图已通过联系表逐张人工复核，并放大检查了 PDF 工具栏、Agent 窄窗口、音乐页、文件菜单、列表与设置。发现并修复的问题包括：

- PDF 工具栏在窄窗口中将操作挤到视口外，改为换行。
- Agent 欢迎内容被可收缩容器压住，音乐播放条也可能覆盖输入区；调整了空状态与低高度布局。
- 音乐分类页仍有唱片式装饰占位图，统一为线性图标。
- 时长标记游离在列表行外，调整为行内元数据。
- 多个空集合页没有解释或下一步操作，增加对应说明与入口。
- 快速关闭再打开菜单时旧浮层尚未退出顶层，改为即时关闭并恢复焦点。
- 英文切换后部分分组、搜索选项和视觉/联网设置漏译，补齐字典并复测。

## 说明

测试断言保留实际功能要求。旧测试的入口、审批步骤和异步等待按当前产品行为更新：先展开工具分组、明确确认修改、等待音频元数据就绪，再验证结果；没有删除功能结果断言。

本轮验证使用已有原生程序加载新版前端。没有修改原生桥接接口。评审复核时 `build.ps1 -Test` 已通过（542 项原生检查）；发布前的 ZIP 与 14 组包级桌面套件结果另行记录。

## 最终复核（2026-11-01）

发布前在最终工作树上完成了完整验证：

- `build.ps1 -Test`：**542 项原生检查通过**；`npm run check`、`npm run check:windows`、文档链接与版本契约检查通过。
- 25 组界面/回归桌面套件：**25 / 25 通过**；其中 organizer 英文切换在异步重绘后保持英文，documents 私有文件夹步骤可重复通过。
- interface-audit：**120 张逐页截图**，几何越界与运行时错误均为 0；摘要见 [interface-flat-ui-0.9.4.json](validation/interface-flat-ui-0.9.4.json)。
- 发布 ZIP 的包级桌面门禁：**14 / 14 通过**（原 11 组 + flat-design + interface-usage + interface-audit）；记录见 [VALIDATION_0.9.4.md](VALIDATION_0.9.4.md)。

本轮修复包括：interface-audit 的创建空间竞态等待；organizer 标记在插入前本地化（并让英文测试等待 i18n 目录及异步重绘）；documents 私有文件夹前重新加载渲染器并清理残留弹层；移除指向未跟踪 `test-output` 的文档链接；把三个界面套件加入包级门禁。
