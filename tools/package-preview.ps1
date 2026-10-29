$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$metadata=Get-Content -LiteralPath (Join-Path $projectRoot 'package.json') -Raw | ConvertFrom-Json
$buildRoot=Join-Path $projectRoot 'dist\kok'
if(-not(Test-Path -LiteralPath (Join-Path $buildRoot 'kok.exe'))){throw 'Build kok before packaging.'}
# A fresh staging directory keeps the existing portable user's data untouched.
$stagingRoot=Join-Path $projectRoot ('dist\preview-'+$metadata.version+'-'+[DateTime]::Now.ToString('yyyyMMdd-HHmmss'))
if(Test-Path -LiteralPath $stagingRoot){throw 'Preview staging directory already exists.'}
$packageRoot=Join-Path $stagingRoot 'kok'
New-Item -ItemType Directory -Path $packageRoot | Out-Null
$files=@('kok.exe','kok.exe.config','Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.WinForms.dll','WebView2Loader.dll','TagLibSharp.dll')
$files+=@((Get-Content -LiteralPath (Join-Path $projectRoot 'tools\youtube-dependencies.json') -Raw | ConvertFrom-Json) | ForEach-Object {$_.id+'.dll'})
$files+='sqlite3.dll'
foreach($file in ($files | Select-Object -Unique)){
    if([IO.Path]::GetFileName($file) -ne $file){throw 'Invalid runtime file name.'}
    Copy-Item -LiteralPath (Join-Path $buildRoot $file) -Destination $packageRoot
}
& (Join-Path $PSScriptRoot 'copy-runtime-ui.ps1') -Destination (Join-Path $packageRoot 'ui')
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs') -Destination $packageRoot -Recurse
# The in-app sample action is part of the offline welcome flow. Include only
# repository-owned sample assets, never an existing portable user's data folder.
$samplesRoot=Join-Path $projectRoot 'samples'
if(Test-Path -LiteralPath $samplesRoot){Copy-Item -LiteralPath $samplesRoot -Destination $packageRoot -Recurse}
foreach($file in @('README.md','README.en.md','CHANGELOG.md','CHANGELOG.en.md','ROADMAP.md','ROADMAP.en.md','SECURITY.md','SECURITY.en.md','LICENSE','THIRD_PARTY_NOTICES.md','SUPPORT.md','CODE_OF_CONDUCT.md','CONTRIBUTING.md')){Copy-Item -LiteralPath (Join-Path $projectRoot $file) -Destination $packageRoot}
foreach($file in @('WEBVIEW2-LICENSE.txt','WEBVIEW2-NOTICE.txt')){Copy-Item -LiteralPath (Join-Path $buildRoot $file) -Destination $packageRoot}
& (Join-Path $PSScriptRoot 'audit-release.ps1') -PackageDirectory $packageRoot
$archive=Join-Path $stagingRoot ('kok-'+$metadata.version+'-win-x64-preview.zip')
Compress-Archive -LiteralPath $packageRoot -DestinationPath $archive -CompressionLevel Optimal
& (Join-Path $PSScriptRoot 'audit-release.ps1') -Archive $archive
$hash=(Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText($archive+'.sha256',$hash+'  '+[IO.Path]::GetFileName($archive)+[Environment]::NewLine,[Text.UTF8Encoding]::new($false))
Write-Output ('Local preview: '+$archive)
