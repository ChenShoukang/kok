# v0.6.1 · 新建文档

点击主窗口右上角“新建”，或按 **Ctrl+N**。选择 Markdown、Word（DOCX）或纯文本，填写名称和分类，然后创建并编辑。重名文档自动编号，不覆盖已有文件。

- Markdown 默认打开源码和阅读预览；可切换编辑视图，支持基础标题、加粗、斜体、列表和引用。只切换视图不会改写原来的 Markdown。
- DOCX 默认使用可视化正文编辑，保存为真实的 OpenXML Word 文件，支持基础段落、标题、加粗和斜体；不需要安装 Office。复杂 Office 排版、嵌入对象和宏不属于此轻量编辑范围。
- 停止输入约 0.7 秒后自动保存，也可点击“保存”或按 **Ctrl+S**。关闭编辑器或窗口时保存当前内容。新建的文件保存在资料库的 `data/documents/`；文件菜单可打开其所在文件夹，也可“另存副本”到其他位置。
- 新建文档重新打开会直接进入编辑器。导入的文件继续采用草稿与另存副本流程；直接保存限于 kok 新建的文档。
- 正常保存采用临时文件替换，并保留上一份 `.bak`。检测到外部修改时停止覆盖，保留本地恢复草稿，供重开、重试保存或另存副本。恢复草稿不会自动覆盖外部版本。
- 私密文件夹中禁用新建和直接保存，避免在加密索引之外自动创建明文；可返回普通资料库创建，再按现有确认流程归入私密文件夹。私密文件夹现有的原文件边界不变。

参考 [MarkText](https://github.com/marktext/marktext) 的简洁编辑、源码与阅读布局，以及其[编辑器布局实现](https://github.com/marktext/marktext/blob/develop/packages/desktop/src/renderer/src/components/editorWithTabs/index.vue)。表单、菜单与动画沿用 kok 的短过渡及中断管理，参考来源见[动画说明](MOTION_0.6.1.md)。未复制项目代码或引入新的编辑器运行库。

验证：`build.ps1 -Test` 包含文档原生检查；`tools/test-desktop.ps1 -Suites documents` 使用独立纯净模式资料库，检查实际创建、UTF-8 与 DOCX 保存、自动保存、关闭前保存、重开、重名、取消、无效名称、外部修改恢复、导入原件保护、中英文、深浅主题和窄窗口。测试不联网、不读取个人文档或 API 配置。

## English

Choose **New** in the main window or press **Ctrl+N**, then select Markdown, Word (DOCX) or plain text, a name and a collection. Duplicate names receive a numeric suffix.

Markdown opens source plus a reading preview, with a switchable editing view and basic headings, bold, italic, lists and quotes. Switching views alone preserves the source. DOCX uses basic visual text editing and saves an actual OpenXML file without requiring Office; complex Office layout, embedded objects and macros are outside its scope.

Changes auto-save after about 0.7 seconds of inactivity; **Ctrl+S**, Save and closing the editor/window also save. Managed documents live under `data/documents/` and reopen in the editor. Reveal them through the file menu or export a copy. Imported originals retain the draft/copy workflow.

Atomic replacements keep a previous `.bak`. Conflicting external changes stop direct overwrite and retain a local recovery draft. Creation/direct saving is disabled inside the private folder to avoid automatically writing plaintext outside the encrypted index; create in the regular library first.

Design references: MarkText's minimal editing/source layout and kok's existing interruptible transitions. No project source or additional editor runtime is bundled. Native and isolated desktop checks cover real file persistence, conflicts, immediate closing, original protection, localization, themes and compact windows, without network or personal data.
