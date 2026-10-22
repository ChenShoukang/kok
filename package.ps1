$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
& (Join-Path $root 'build.ps1') -Test
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
$source = Join-Path $root 'dist\kok'
$stage = Join-Path $root ('dist\release-' + [guid]::NewGuid().ToString('N') + '\kok')
New-Item -ItemType Directory -Force $stage | Out-Null
# Explicit allowlist: never include the data directory or development test binaries.
foreach ($file in @('kok.exe','kok.exe.config','Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.WinForms.dll','WebView2Loader.dll','README.md','LICENSE','THIRD_PARTY_NOTICES.md','WEBVIEW2-LICENSE.txt','WEBVIEW2-NOTICE.txt')) {
    Copy-Item -LiteralPath (Join-Path $source $file) -Destination $stage -Force
}
foreach ($folder in @('ui','samples','docs')) { if(Test-Path -LiteralPath (Join-Path $source $folder)){Copy-Item -LiteralPath (Join-Path $source $folder) -Destination $stage -Recurse -Force} }
$archive = Join-Path $root 'dist\kok-1.0.0-win-x64.zip'
Compress-Archive -LiteralPath $stage -DestinationPath $archive -Force
$hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath ($archive+'.sha256') -Value ($hash+'  '+[IO.Path]::GetFileName($archive)) -Encoding Ascii
$size = [math]::Round((Get-Item -LiteralPath $archive).Length / 1MB, 2)
Write-Output "Package: $archive ($size MB)"
Write-Output "SHA-256: $hash"
