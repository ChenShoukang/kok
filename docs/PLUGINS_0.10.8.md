# 声明式扩展包（v0.10.8）

[项目首页](../README.md) · [Agent 首页与交互](AGENT_HOME_UI.md) · [本次验证](VALIDATION_0.10.8.md)

扩展包把**技能**（Agent Skills 规范的 `SKILL.md` 目录）与**提示模板**（Markdown）打包分发。它们是**纯静态文本**：kok 只读取、解析并在你明确选择时使用，**不会执行任何脚本或代码**，也不新增权限。

## 放置位置

按优先级从高到低（同名扩展包先到先得）：

1. `<kok 程序目录>\ui\plugins`
2. `%USERPROFILE%\.kok\plugins`
3. `<资料库目录>\.kok\plugins`

每个扩展包是一个目录，目录里（或向下一层）有 `plugin.json`：

```
%USERPROFILE%\.kok\plugins\my-pack\
├── plugin.json
├── skills\
│   └── pdf-tools\SKILL.md
└── prompts\
    └── write-notes.md
```

```json
{
  "name": "my-pack",
  "version": "1.0.0",
  "description": "我的技能与提示模板",
  "skills": ["skills/"],
  "prompts": ["prompts/"]
}
```

- `name`：小写字母/数字/连字符，1–64 字符，唯一。
- `version`（可选，≤32）、`description`（可选，≤200）。
- `skills` / `prompts`：扩展包内的相对目录，最多各 16 项；路径必须留在扩展包目录内（绝对路径与 `..` 会被拒绝），不存在的目录会被忽略。
- `plugin.json` ≤64 KB；最多 32 个扩展包。未知字段会被忽略，且永远不会被执行（包括 `scripts`/`main`/`hooks` 等）。
- 技能目录格式与 [Agent Skills 规范](https://agentskills.io/specification) 一致（`SKILL.md` + frontmatter `name`/`description`）；技能正文上限 20000 字符。
- 提示模板是 `prompts/` 下的 `*.md`，可选 frontmatter `name`/`description`；没有 frontmatter 时用文件名作名称、第一行 `# 标题` 作说明。模板内 `$ARGUMENTS`（不区分大小写）会被替换为调用时传入的参数；没有占位符时参数追加在模板末尾。

## 使用

- **技能**：Agent 启动时把扩展包技能的 name/description/location 放进 `<available_skills>` 索引；模型按需通过 `load_skill` 载入全文。Agent 页「技能」下拉会列出已发现技能（含扩展包技能），可点「刷新技能」。
- **扩展包面板**：Agent 高级设置中的「扩展包」按钮列出已安装扩展包、技能数与提示模板，点「重新加载」即时重新扫描（无需重启）。
- **提示模板**：在输入框输入 `/prompt:<名称> <参数>`，模板会展开到输入框（不会自动发送、不会作为原始命令发给模型），确认后按普通消息发送。

## 边界

- 只声明、不执行：没有 JS/TS/Python 入口，没有网络/文件 API，没有钩子注册；技能与模板进入上下文时仍标记为不可信参考文本。
- 技能/模板只影响提示词与输入框文字；写入、审批、证据核对与撤销仍由既有 Agent 流程控制。
- 扩展包中的路径不会被复制或上传；除解析文本外不会修改扩展包目录。
