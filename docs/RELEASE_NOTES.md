## v0.2.1 · 顺滑菜单、视频封面与右键属性

- 设置和资料库排序使用统一的玻璃选择菜单，支持打开 / 收起动画、键盘导航、深浅主题、中英文和减少动态效果。
- 视频导入后在空闲时静音提取真实画面并缓存封面；已有资料库也会补齐。打开预览或播放媒体时暂停后台提取，不修改原片。
- 视频右键菜单新增属性：文件名、位置、大小、修改时间，以及内核实际读取的时长、分辨率。
- 汇总此前本地版本的主题统一、照片编辑增强、视频快捷键、无边框全屏、小窗口适配与大视频稳定性修复。
- 整理截图目录、开发 / 测试说明和版本记录，通过 GitHub Actions 构建并发布 Windows 便携包。

封面提取取决于当前内核能否解码视频。本版不新增解码器，编码与多音轨详情暂不解析。

---

## v0.2.1 · Smooth menus, video covers and properties

- Settings and library sorting share animated glass menus with keyboard navigation, themes, Chinese / English and reduced motion.
- Imported and existing videos receive cached real-frame covers when idle. Extraction stays paused and muted, yields to previews or playback, and preserves source files.
- Video context menus show file name, location, size, modification date, and actual duration / resolution from the playback engine.
- Includes the previous local builds: consistent themes, expanded photo editing, configurable video shortcuts, borderless fullscreen, adaptive windows and large-video crash fixes.
- Organizes screenshots, development / testing instructions and version history, with Windows portable builds published through GitHub Actions.

Cover extraction requires a decodable video. This release adds no codecs and does not parse codec or multiple-audio-track details.

更新时保留原来的 data 文件夹。
Preserve the existing data folder when updating.
