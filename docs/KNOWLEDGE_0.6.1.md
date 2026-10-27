# 笔记与知识图谱

[English](KNOWLEDGE_0.6.1.en.md) · [返回](../README.md)

![本地原创示例的深色知识图谱](screenshots/knowledge-061-dark.png)

从左侧“知识图谱”进入全局关系图，或在正文编辑器点图谱按钮，只看当前笔记的一层关联。全部功能在本机运行，纯净模式也可使用。

| 功能 | 用法 |
| --- | --- |
| 双向链接 | 输入 `[[`，搜索并选择资料库内的笔记；方向键选择、Enter / Tab 确认、Esc 取消。也支持 `[[笔记名#标题\|显示文字]]` |
| 创建关联笔记 | 点虚线链接或补全列表的“创建笔记”，确认名称和分类后创建；同名笔记优先当前分类，否则显示候选 |
| 链接与大纲 | 编辑器侧栏查看出链、反向链接和标题大纲；点击打开笔记或定位标题 |
| 标签与待办 | 正文写 `#标签`、`- [ ] 待办`；预览里可勾选。支持行内 frontmatter `tags: [标签]`、`aliases: [别名]` |
| 模板 | Ctrl+N 选择空白、日常记录、会议纪要、阅读笔记，创建 Markdown / Word（DOCX）/ TXT |
| 图谱 | 滚轮缩放、拖动节点或背景，单击看关联，双击打开；旁边列表同样可选择和打开，支持搜索、分类、标签、独立笔记与一 / 两层关联 |

Markdown 源码与编辑视图、预览、自动保存、Ctrl+S 和重开恢复沿用[文档功能](DOCUMENTS_0.6.1.md)。代码块、行内代码、图片和外部网址不生成关系；不执行笔记内脚本。已有文件的本地 Markdown 链接参与索引。

**范围**：这是轻量笔记工具。DOCX 编辑重建简洁正文，不保留所有 Word 排版；Markdown 支持常用标题、强调、列表、引用、任务和代码块，不提供完整插件系统、块嵌入或 Obsidian 插件兼容。导入文档保留原件，编辑存草稿或另存副本；直接自动写入限于 kok 新建文档。

**数据与性能**：图谱来自普通资料库的文件名、分类、标签、笔记、批注想法及可读正文，不调用 AI。正文索引支持 Markdown / TXT / DOCX / EPUB；PDF 不在此处做全文解析。每篇正文最多 12 万字，正文总预算 600 万字符，最多索引 2000 个条目；过大或不可读内容显示提示并保留元数据。图谱每次最多显示 500 个节点，布局计算放在 Worker，收敛后一次淡入，筛选复用坐标，拖动节点后保持位置，空闲时停止计算与重绘。关闭窗口终止 Worker，系统减少动态效果时直接显示稳定布局。

私密文件夹不参与图谱、不建立正文缓存、不显示反向链接；切换资料库清空界面索引和布局坐标。图谱不会读取未导入的文件夹，不会上传内容。

设计思路参考 [Logseq 的局部 / 全局图谱](https://github.com/logseq/logseq/blob/master/src/main/frontend/components/graph.cljs)、[思源的关联笔记](https://github.com/siyuan-note/siyuan)、[d3-force 的有界布局与衰减](https://github.com/d3/d3-force/blob/main/src/simulation.js)及 [Radix Presence 的进出状态处理](https://github.com/radix-ui/primitives/blob/main/packages/react/presence/src/presence.tsx)。采用自己的 Canvas / Worker 与现有 WAAPI 动画实现，没有复制这些项目的组件代码或新增其运行时。
