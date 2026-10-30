param([switch]$SkipTests)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
& (Join-Path $root 'build.ps1') -Test:(-not $SkipTests)
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
$source = Join-Path $root 'dist\kok'
$stage = Join-Path $root ('dist\release-' + [guid]::NewGuid().ToString('N') + '\kok')
New-Item -ItemType Directory -Force $stage | Out-Null
# Explicit allowlist: never include the data directory or development test binaries.
foreach ($file in @('kok.exe','kok.exe.config','Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.WinForms.dll','WebView2Loader.dll','README.md','README.en.md','ROADMAP.md','ROADMAP.en.md','CHANGELOG.md','CHANGELOG.en.md','SECURITY.md','SECURITY.en.md','LICENSE','THIRD_PARTY_NOTICES.md','SUPPORT.md','CODE_OF_CONDUCT.md','CONTRIBUTING.md','WEBVIEW2-LICENSE.txt','WEBVIEW2-NOTICE.txt')) {
    Copy-Item -LiteralPath (Join-Path $source $file) -Destination $stage -Force
}
& (Join-Path $root 'tools\copy-runtime-ui.ps1') -Destination (Join-Path $stage 'ui')
foreach ($folder in @('samples','docs')) { if(Test-Path -LiteralPath (Join-Path $root $folder)){Copy-Item -LiteralPath (Join-Path $root $folder) -Destination $stage -Recurse -Force} }
foreach($dependency in (Get-Content -LiteralPath (Join-Path $root 'tools\youtube-dependencies.json') -Raw | ConvertFrom-Json)){Copy-Item -LiteralPath (Join-Path $source ($dependency.id+'.dll')) -Destination $stage -Force}
Copy-Item -LiteralPath (Join-Path $source 'TagLibSharp.dll') -Destination $stage -Force
Copy-Item -LiteralPath (Join-Path $source 'sqlite3.dll') -Destination $stage -Force
& (Join-Path $root 'tools\audit-release.ps1') -PackageDirectory $stage
$version = (Get-Content (Join-Path $root 'package.json') -Raw | ConvertFrom-Json).version
$archive = Join-Path $root ("dist\kok-$version-win-x64.zip")
Compress-Archive -LiteralPath $stage -DestinationPath $archive -Force
& (Join-Path $root 'tools\audit-release.ps1') -Archive $archive
$hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath ($archive+'.sha256') -Value ($hash+'  '+[IO.Path]::GetFileName($archive)) -Encoding Ascii
$size = [math]::Round((Get-Item -LiteralPath $archive).Length / 1MB, 2)
Write-Output "Package: $archive ($size MB)"
Write-Output "SHA-256: $hash"
# Verification passed; drop the GUID staging copy so dist/ does not accumulate full app trees.
$stageRoot=Split-Path -Parent $stage
$distBoundary=[IO.Path]::GetFullPath((Join-Path $root 'dist')).TrimEnd([IO.Path]::DirectorySeparatorChar)+[IO.Path]::DirectorySeparatorChar
$resolvedStage=[IO.Path]::GetFullPath($stageRoot)
if(-not $resolvedStage.StartsWith($distBoundary,[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($resolvedStage) -notmatch '^release-[a-f0-9]{32}$'){throw 'Refusing to remove a staging path outside the release directory.'}
if(Test-Path -LiteralPath $resolvedStage){Remove-Item -LiteralPath $resolvedStage -Recurse -Force}
