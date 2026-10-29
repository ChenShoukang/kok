# 动画与点击响应

[English](MOTION_INPUT.en.md) · [Agent 交互](AGENT_INTERACTIONS.md)

采用有阻尼、不越界的弹簧进度，统一显现、菜单、侧栏和选中指示器的节奏。外观变化与输入响应分开：新页面状态同步提交，按钮和菜单的点击矩形保持原位；图标、封面、指示器及不可交互的旧内容可以移动。原生窗口命令立即发送，不等待退出动画。

修复了快速切换到 Agent 后被旧页面覆盖、普通界面更新关闭下拉菜单、菜单显现时命中位置移动的问题。Agent 保留打开的下拉菜单和输入框；通用菜单只在同一工作区、同一弹窗、相同选项与当前值时重新绑定。收藏与更多按钮增至 32 px。装饰快照、提示层不拦截点击。

快速重复操作会取消旧动画，保留最新状态。拖动或缩放窗口时沿用已有的动画收尾机制；隐藏页面或开启减少动态效果时直接收尾。没有引入 Remotion / Motion / Radix 运行时依赖。

参考：[Remotion spring](https://www.remotion.dev/docs/spring)、[Motion animate](https://motion.dev/docs/animate)、[Radix 动画挂载与卸载](https://www.radix-ui.com/primitives/docs/guides/animation)。这是对时序和交互边界的适配，不是移植视频渲染器或组件代码。

验证使用静音隔离窗口和生成资料，包括快速切页、动画期间点击、菜单保持、反复打开关闭、快速切换设置页、减少动态效果和真实原生拖动／缩放；不调用真实 AI 服务。测试报告另存于本机 `test-output`。
