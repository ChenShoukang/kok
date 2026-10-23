$ErrorActionPreference = 'Stop'
$repo = $env:GITHUB_REPOSITORY
if ($repo -ne '878kouyier/kok') { throw 'Release publishing is configured for 878kouyier/kok.' }
if (-not $env:GITHUB_TOKEN) { throw 'GITHUB_TOKEN is required inside GitHub Actions.' }
$headers = @{ Authorization = 'Bearer ' + $env:GITHUB_TOKEN; Accept = 'application/vnd.github+json'; 'X-GitHub-Api-Version' = '2022-11-28' }
$version = (Get-Content (Join-Path $PSScriptRoot '..\package.json') -Raw | ConvertFrom-Json).version
$tag = 'v' + $version
$base = 'https://api.github.com/repos/' + $repo
$existing = $null
try { $existing = Invoke-RestMethod -Uri ($base + '/releases/tags/' + $tag) -Headers $headers }
catch { if ([int]$_.Exception.Response.StatusCode -ne 404) { throw } }
if ($existing) { Write-Output ('Release already exists: ' + $existing.html_url); exit 0 }
$notes = @'
kok Windows x64：完整中英文界面、流畅玻璃设计与 AI 翻译。

- 设置 → 界面语言，可切换中文 / English，立即生效并记住选择。
- 导航、菜单、工具、设置、提示、帮助与应用对话框标题均有中英文。
- 文件名、分类名、正文、笔记和译文保留原内容。

- 加宽侧栏，分类选中背景平滑移动，适配小窗口。
- 玻璃 Logo 柔和显现，主界面分层进入；页面采用新旧内容交叠过渡。
- 统一弹窗、菜单、笔记面板、照片与按钮的动画节奏，快速连续操作可安全打断。
- 增强侧栏、顶部栏、播放器与批注浮层的毛玻璃，保留减少动态效果支持。
- 可填写大模型 API 地址、模型与密钥，自动 / 手动翻译字幕、文档、PDF 与电子书。
- 双语对照、保持字幕时间轴、译文缓存、进度与取消，支持 TXT / SRT 导出。
- 密钥按当前 Windows 账户加密保存；AI 翻译需联网并按服务商规则计费。
- 支持浅色和深色。
- 视频、照片、音乐、文档、读书统一资料库，自动与自定义分类。
- 视频倍速、静音自动加速、同名文字字幕自动加载。
- 专辑页面与专辑播放队列。
- 三种常用字体，以及 TTF / OTF 导入。
- 文档插入文字、下划线、荧光标记；读书划线与想法弹窗。
- 照片亮度、对比度、饱和度、裁剪、旋转与翻转，另存副本。

解压后双击 kok.exe。需要 Windows 10 / 11 x64、.NET Framework 4.8 和 WebView2 Runtime。批注保存为本地阅读副本，原文件保留。完整格式范围与限制见 README。

English

- Switch between Chinese and English in Settings → Interface language. The choice is remembered.
- Navigation, menus, tools, settings, messages, help and application-owned dialog titles are localized.
- File names, collections, source text, notes and AI translations keep their original content.
- Smoother glass startup and transitions, a wider sidebar, and configurable AI translation.
- Enter your provider endpoint, model and key for subtitle, document, PDF and book translation. Bilingual view, persistent cache, cancellation and TXT / SRT export are included.
- Extract the ZIP and run kok.exe on Windows 10 / 11 x64 with .NET Framework 4.8 and WebView2 Runtime. AI translation requires a configured service and follows provider billing. See README.en.md for details.
'@
$body = @{ tag_name = $tag; target_commitish = $env:GITHUB_SHA; name = 'kok ' + $version; body = $notes; draft = $true; prerelease = $false } | ConvertTo-Json
$release = Invoke-RestMethod -Method Post -Uri ($base + '/releases') -Headers $headers -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($body))
$upload = $release.upload_url -replace '\{.*$', ''
foreach ($name in @("kok-$version-win-x64.zip", "kok-$version-win-x64.zip.sha256")) {
    $file = Join-Path $PSScriptRoot ('..\dist\' + $name)
    if (-not (Test-Path -LiteralPath $file)) { throw "Missing release asset: $name" }
    Invoke-RestMethod -Method Post -Uri ($upload + '?name=' + [Uri]::EscapeDataString($name)) -Headers $headers -ContentType 'application/octet-stream' -InFile $file | Out-Null
}
$publish = @{ draft = $false; make_latest = 'true' } | ConvertTo-Json
$release = Invoke-RestMethod -Method Patch -Uri ($base + '/releases/' + $release.id) -Headers $headers -ContentType 'application/json' -Body $publish
Write-Output ('Published: ' + $release.html_url)
