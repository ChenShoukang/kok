# 版本与更新

这一页说明当前 kok 的版本状态、如何查看版本，以及怎样把已有数据带到新版本。

## v0.11.2 Preview

- 空白的 Agent 首页和「我的作品」可以用示例创建一份笔记。练习在本机完成。[第一篇笔记](../getting-started/first-work.md)
- 「全部项目」支持按名称或目标搜索。在项目内可以添加成果并查找已经导出的副本。[继续创作项目](../works/continue-editing.md)
- 中文帮助中心补充了从资料到报告与 PPT 的教程。[创作项目教程](../getting-started/first-agent-project.md)

[GitHub 发布页与下载](https://github.com/ChenShoukang/kok/releases/tag/v0.11.2) · [上一版说明](https://github.com/ChenShoukang/kok/releases/tag/v0.11.1)

## 查看版本

打开 **设置 → 资料库 → 关于 kok**，可以看到 kok 版本、WebView2 版本和资料库位置。反馈问题时请附上这些信息。

## 更新方式

退出 kok 并备份当前资料库。下载并解压新 ZIP，使用原资料库启动新版本。默认资料库位于程序目录的 `data/`，另装到新目录时需要复制它。若使用了 `--data-dir`，启动时继续指向原目录。[安装 kok](../getting-started/install.md)

> [!warning]+ 更新前先备份
> `data/` 里是资料索引、笔记、进度和设置，不是可以随手重建的缓存。预览版仍在持续调整，更新前请保留 `data/` 的独立备份。

> [!info]+ Preview 预览版
> 当前版本是未签名的 Preview 预览版，功能和界面仍可能调整。发布包提供 SHA-256 校验文件，请从 Release 页面下载；重要资料请另外保留副本。

## 更新记录与验证

版本变化和本版验收范围分别见[版本记录](../../../CHANGELOG.md)与[v0.11.2 验证记录](../../VALIDATION_0.11.2.md)。

---

**相关**

- [v0.11.2 验证记录](../../VALIDATION_0.11.2.md)
- [版本记录](../../../CHANGELOG.md)
- [安装 kok](../getting-started/install.md)
- [帮助与支持](../help-and-support.md)

[返回帮助中心](../index.md)
