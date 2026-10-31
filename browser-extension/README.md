# Save to kok / 保存到 kok

Chrome / Edge Manifest V3 扩展。当前提供源代码加载，不代表已上架浏览器商店。

1. 在 kok 的“资料检索与备份”启用浏览器采集，显示连接信息。
2. 打开 `chrome://extensions` 或 `edge://extensions`，启用开发者模式，选择“加载已解压的扩展程序”，选择本文件所在目录。
3. 在扩展设置填写采集地址和配对密钥。
4. 点击扩展保存正文 / 选区，或右键保存选区 / 图片。内容进入 Inbox，带来源 URL 和时间。

正文保存为离线 Markdown；不承诺原网页排版、登录态、全部附件或复杂动态页面的完整归档。图片按需请求网站读取权限，不携带登录凭据抓取。关闭 kok 或撤销连接后，旧连接不能继续保存。扩展不调用 AI。

Chrome / Edge source-loaded extension; not submitted to a browser store. Enable paired capture in kok, load this folder from the extensions page, and enter the address and key in extension options. Use the popup for page text / selection, or context menus for selection / images. Captures retain provenance and are deduplicated by content. Pairing credentials stay in local extension storage; keep them private.
