param([switch]$Test)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
if (-not (Test-Path (Join-Path $root 'ui\vendor\pdfjs\pdf.mjs'))) {
    if (-not (Test-Path (Join-Path $root 'node_modules\pdfjs-dist'))) { throw 'Run npm ci before building to prepare the local PDF.js viewer.' }
    & node (Join-Path $root 'tools\vendor.cjs')
    if ($LASTEXITCODE -ne 0) { throw 'PDF.js asset preparation failed.' }
}
$version = '1.0.4258.31'
$sdk = Join-Path $root 'tools\webview2'
if (-not (Test-Path (Join-Path $sdk 'lib\net462\Microsoft.Web.WebView2.Core.dll'))) {
    $archive = Join-Path $root 'tools\webview2.zip'
    New-Item -ItemType Directory -Force (Join-Path $root 'tools') | Out-Null
    Invoke-WebRequest -Uri "https://api.nuget.org/v3-flatcontainer/microsoft.web.webview2/$version/microsoft.web.webview2.$version.nupkg" -OutFile $archive
    if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne '56F7F4B8BF9AEE4B8EFEFBBDD4F67D5F74EBD1B100ED0806DA71BF76AF481AA9') { throw 'WebView2 SDK checksum mismatch.' }
    Expand-Archive -LiteralPath $archive -DestinationPath $sdk -Force
}
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $compiler)) { throw '.NET Framework 4.8 x64 is required to build kok.' }
$out = Join-Path $root 'dist\kok'
New-Item -ItemType Directory -Force $out | Out-Null
$core = Join-Path $sdk 'lib\net462\Microsoft.Web.WebView2.Core.dll'
$forms = Join-Path $sdk 'lib\net462\Microsoft.Web.WebView2.WinForms.dll'
$references = @('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Web.Extensions.dll','System.Xml.dll','System.Xml.Linq.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','System.Net.Http.dll','System.Security.dll',$core,$forms)
$arguments = @('/nologo','/target:winexe','/platform:x64','/optimize+','/utf8output',('/out:' + (Join-Path $out 'kok.exe')))
if (Test-Path (Join-Path $root 'ui\kok.ico')) { $arguments += '/win32icon:' + (Join-Path $root 'ui\kok.ico') }
foreach ($reference in $references) { $arguments += '/reference:' + $reference }
$arguments += Get-ChildItem (Join-Path $root 'src\*.cs') | ForEach-Object { $_.FullName }
& $compiler $arguments
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
Copy-Item $core,$forms -Destination $out -Force
Copy-Item (Join-Path $sdk 'runtimes\win-x64\native\WebView2Loader.dll') -Destination $out -Force
Copy-Item (Join-Path $root 'ui') -Destination $out -Recurse -Force
if (Test-Path (Join-Path $root 'samples')) { Copy-Item (Join-Path $root 'samples') -Destination $out -Recurse -Force }
if (Test-Path (Join-Path $root 'docs')) { Copy-Item (Join-Path $root 'docs') -Destination $out -Recurse -Force }
Copy-Item (Join-Path $root 'src\kok.exe.config') -Destination (Join-Path $out 'kok.exe.config') -Force
foreach ($name in @('README.md','README.en.md','LICENSE','THIRD_PARTY_NOTICES.md')) { if (Test-Path (Join-Path $root $name)) { Copy-Item (Join-Path $root $name) -Destination $out -Force } }
if (Test-Path (Join-Path $sdk 'LICENSE.txt')) { Copy-Item (Join-Path $sdk 'LICENSE.txt') -Destination (Join-Path $out 'WEBVIEW2-LICENSE.txt') -Force }
if (Test-Path (Join-Path $sdk 'NOTICE.txt')) { Copy-Item (Join-Path $sdk 'NOTICE.txt') -Destination (Join-Path $out 'WEBVIEW2-NOTICE.txt') -Force }
if ($Test) {
    $testExe = Join-Path $out 'kok.Tests.exe'
    $testArgs = @('/nologo','/target:exe','/platform:x64','/utf8output',('/out:'+$testExe),('/reference:'+(Join-Path $out 'kok.exe')))
    foreach ($reference in $references) { $testArgs += '/reference:' + $reference }
    $testArgs += Join-Path $root 'tests\NativeTests.cs'
    $testArgs += Join-Path $root 'tests\TranslationTests.cs'
    & $compiler $testArgs
    if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
    & $testExe
    if ($LASTEXITCODE -ne 0) { throw 'Native tests failed.' }
}
Write-Output "Built: $out\kok.exe"
