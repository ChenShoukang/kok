# v0.6.1 · 阅读与照片编辑

阅读闲置约 3.5 秒后收起上下工具栏与进度条，纸张和边缘保持同色。收起只改变显隐，不重建文字、不改变分页或阅读位置。鼠标在正文中移动、键盘翻页不会反复唤出工具栏；靠近上下边缘、按 Tab 或选中文字可恢复。悬停工具栏、键盘聚焦按钮、打开菜单 / 排版弹窗 / 笔记时保持可见。隐藏的工具栏不接收点击和键盘焦点。

照片编辑使用与预览一致的深色中性配色，修复深色面板继承浅色文字的问题。工具区域使用清晰的系统界面字体，常用标签至少 12 px；正文、画布与全局字体选择不受此局部样式影响。统一滑杆、按钮、预设、导出尺寸与弹出菜单的颜色和选中状态。修复编辑标题和撤销按钮被窗口标题栏遮挡；小窗口改为上下布局，保留画布和可滚动的编辑区。

参考 [Foliate 工具栏显隐](https://github.com/johnfactotum/foliate/blob/gtk4/src/book-viewer.js) 对边缘、菜单和交互状态的处理，以及 [TOAST UI Image Editor 深色主题](https://github.com/nhn/tui.image-editor/blob/master/apps/image-editor/examples/js/theme/black-theme.js) 对面板、标签、滑杆和选中状态的区分。仅参考交互与设计思路，未复制其代码或引入运行库。

定向检查：`tools/test-desktop.ps1 -Suites workspace-clarity`。隔离静音测试自动生成阅读样本和原创照片，检查阅读显隐与恢复、分页稳定、原生全屏和小窗口；测量常用编辑文字在深浅主题下的对比度和字号，并实际点击工具、预设、撤销 / 重做、裁剪 / 取消及导出尺寸菜单。不读取个人资料或 API 配置，不请求网络服务。

## English

Reading controls and the progress slider fade after about 3.5 seconds of inactivity. Paper and edges share a surface; hiding controls does not rebuild text or repaginate. Moving over the text or using page-navigation keys does not repeatedly reveal chrome. Edges, Tab and text selection restore it; hovered/focused controls, menus, settings and notes keep it visible. Hidden controls are inert.

Photo editing uses a consistent neutral media palette and clear system UI type, with common labels at least 12 px. This local styling leaves document/canvas fonts and the global font choice intact. It fixes light-theme text inherited by dark panels, mismatched popups, and editor controls obscured by the window header. Compact windows stack the canvas and scrollable editing area.

References are Foliate's interaction-aware toolbar visibility and TOAST UI Image Editor's dark-theme control hierarchy (links above). No source code or runtime from either project is bundled. Focused isolated checks cover native fullscreen/compact layouts, reading-state stability, common-label contrast and sizes, editing controls, keyboard recovery and English labels. No personal configuration or network services are used.
