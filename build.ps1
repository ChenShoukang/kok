param([switch]$Test)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
& node (Join-Path $root 'tools\vendor-sandbox.cjs')
if($LASTEXITCODE -ne 0){throw 'Sandbox asset verification failed.'}
& node (Join-Path $root 'tools\build-styles.cjs')
if($LASTEXITCODE -ne 0){throw 'Style bundle preparation failed.'}
& node (Join-Path $root 'tools\prepare-sqlite.cjs')
if($LASTEXITCODE -ne 0){throw 'SQLite preparation failed.'}
& node (Join-Path $root 'tools\vendor-presentations.cjs')
if($LASTEXITCODE -ne 0){throw 'PPTX export bundle verification failed.'}
$fontRoot=Join-Path $root 'ui\fonts'
foreach($font in (Get-Content -LiteralPath (Join-Path $fontRoot 'manifest.json') -Raw | ConvertFrom-Json).files){
    $file=Join-Path $fontRoot $font.file
    if(-not(Test-Path -LiteralPath $file) -or (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant() -ne $font.sha256){throw ('Bundled font is missing or has changed: '+$font.file)}
    if(-not(Test-Path -LiteralPath (Join-Path $root ('docs\licenses\'+$font.licenseFile)))){throw ('Bundled font OFL license is missing: '+$font.licenseFile)}
}
$iconRoot=Join-Path $root 'ui\icons\fluent'
if(-not(Test-Path -LiteralPath (Join-Path $iconRoot 'layout.css'))){throw 'Bundled icon alignment data is missing. Run node tools/generate_icon.cjs.'}
foreach($asset in (Get-Content -LiteralPath (Join-Path $iconRoot 'manifest.json') -Raw | ConvertFrom-Json).files){$file=Join-Path $iconRoot $asset.file;if(-not(Test-Path -LiteralPath $file) -or (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant() -ne $asset.sha256){throw 'Bundled Fluent icon is missing or has changed.'}}
if(-not(Test-Path -LiteralPath (Join-Path $root 'docs\licenses\FluentEmoji-MIT.txt'))){throw 'Bundled Fluent Emoji MIT license is missing.'}
if (-not (Test-Path (Join-Path $root 'ui\vendor\pdfjs\pdf.mjs')) -or -not(Test-Path (Join-Path $root 'ui\vendor\pdf-lib\pdf-lib.min.js'))) {
    if (-not (Test-Path (Join-Path $root 'node_modules\pdfjs-dist'))) { throw 'Run npm ci before building to prepare the local PDF.js viewer.' }
    & node (Join-Path $root 'tools\vendor.cjs')
    if ($LASTEXITCODE -ne 0) { throw 'PDF.js asset preparation failed.' }
}
$version = '1.0.4314.35'
$sdk = Join-Path $root 'tools\webview2'
if (-not (Test-Path (Join-Path $sdk 'lib\net462\Microsoft.Web.WebView2.Core.dll'))) {
    $archive = Join-Path $root 'tools\webview2.zip'
    New-Item -ItemType Directory -Force (Join-Path $root 'tools') | Out-Null
    Invoke-WebRequest -Uri "https://api.nuget.org/v3-flatcontainer/microsoft.web.webview2/$version/microsoft.web.webview2.$version.nupkg" -OutFile $archive
    if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne '90778DEB1B77E36292828B4B5833C9255C27533F40E0DDD55891AE718944EDC4') { throw 'WebView2 SDK checksum mismatch.' }
    Expand-Archive -LiteralPath $archive -DestinationPath $sdk -Force
}
if (-not (Test-Path (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\mscorlib.dll'))) { throw '.NET Framework 4.8 x64 is required to build kok.' }
# Roslyn from NuGet runs on .NET Framework 4.7.2+ and still emits net48 assemblies; the in-box csc.exe stops at C# 5.
$roslynVersion = '5.9.0'
$roslyn = Join-Path $root 'tools\roslyn'
$compiler = Join-Path $roslyn 'tasks\net472\csc.exe'
if (-not (Test-Path $compiler)) {
    $archive = Join-Path $root 'tools\roslyn.zip'
    Invoke-WebRequest -Uri "https://api.nuget.org/v3-flatcontainer/microsoft.net.compilers.toolset/$roslynVersion/microsoft.net.compilers.toolset.$roslynVersion.nupkg" -OutFile $archive
    if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne 'B0227910320C5AF14D80EC32B5E1A759C1E3CC2EC12E7D9CC8862CF826BD9551') { throw 'Roslyn compiler checksum mismatch.' }
    Expand-Archive -LiteralPath $archive -DestinationPath $roslyn -Force
}
$out = Join-Path $root 'dist\kok'
New-Item -ItemType Directory -Force $out | Out-Null
$core = Join-Path $sdk 'lib\net462\Microsoft.Web.WebView2.Core.dll'
$forms = Join-Path $sdk 'lib\net462\Microsoft.Web.WebView2.WinForms.dll'
& (Join-Path $root 'tools\prepare-youtube.ps1')
& (Join-Path $root 'tools\prepare-media.ps1')
& (Join-Path $root 'tools\prepare-agent.ps1')
& (Join-Path $root 'tools\prepare-pdfium.ps1')
$tagLibrary=Join-Path $root 'tools\media-source\TagLibSharp.dll'
$agentLibraries=@((Get-Content -LiteralPath (Join-Path $root 'tools\agent-dependencies.json') -Raw | ConvertFrom-Json) | ForEach-Object {Join-Path $root ('tools\agent-source\lib\'+$_.id+'.dll')})
$siteLibraries=@((Get-Content -LiteralPath (Join-Path $root 'tools\youtube-dependencies.json') -Raw | ConvertFrom-Json) | ForEach-Object {Join-Path $root ('tools\youtube-source\lib\'+$_.id+'.dll')})
$references = @('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Web.Extensions.dll','System.Xml.dll','System.Xml.Linq.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','System.Net.Http.dll','System.Security.dll',$core,$forms)
$facade=Join-Path $env:WINDIR 'Microsoft.NET\assembly\GAC_MSIL\netstandard\v4.0_2.0.0.0__cc7b13ffcd2ddd51\netstandard.dll'
if(-not(Test-Path -LiteralPath $facade)){throw '.NET Framework 4.8 netstandard support is required.'}
$references += $facade
$references += $agentLibraries
$references += $tagLibrary
$references += @($siteLibraries | Where-Object { [IO.Path]::GetFileName($_) -in @('YoutubeExplode.dll','Microsoft.Bcl.AsyncInterfaces.dll','System.Threading.Tasks.Extensions.dll','System.Memory.dll','System.Buffers.dll','System.Runtime.CompilerServices.Unsafe.dll') })
$arguments = @('/nologo','/langversion:latest','/nullable:disable','/target:winexe','/platform:x64','/optimize+','/utf8output',('/out:' + (Join-Path $out 'kok.exe')))
if (Test-Path (Join-Path $root 'ui\kok.ico')) { $arguments += '/win32icon:' + (Join-Path $root 'ui\kok.ico') }
foreach ($reference in $references) { $arguments += '/reference:' + $reference }
$arguments += Get-ChildItem (Join-Path $root 'src\*.cs') | ForEach-Object { $_.FullName }
& $compiler $arguments
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
Copy-Item $core,$forms -Destination $out -Force
Copy-Item $tagLibrary -Destination $out -Force
Copy-Item -LiteralPath (Join-Path $root 'tools\sqlite\sqlite3.dll') -Destination $out -Force
Copy-Item -LiteralPath (Join-Path $root 'tools\pdfium-source\pdfium.dll') -Destination $out -Force
Copy-Item $siteLibraries -Destination $out -Force
Copy-Item $agentLibraries -Destination $out -Force
Copy-Item (Join-Path $sdk 'runtimes\win-x64\native\WebView2Loader.dll') -Destination $out -Force
& (Join-Path $root 'tools\copy-runtime-ui.ps1') -Destination (Join-Path $out 'ui')
# Remove only known, byte-identical original TTFs left by older local builds.
foreach($font in (Get-Content -LiteralPath (Join-Path $fontRoot 'manifest.json') -Raw | ConvertFrom-Json).files){
    if($font.sourceFile -and [IO.Path]::GetFileName($font.sourceFile) -eq $font.sourceFile){
        $obsolete=Join-Path $out ('ui\fonts\'+$font.sourceFile)
        if((Test-Path -LiteralPath $obsolete) -and (Get-FileHash -LiteralPath $obsolete -Algorithm SHA256).Hash.ToLowerInvariant() -eq $font.sourceSha256){Remove-Item -LiteralPath $obsolete -Force}
    }
}
if (Test-Path (Join-Path $root 'samples')) { Copy-Item (Join-Path $root 'samples') -Destination $out -Recurse -Force }
if (Test-Path (Join-Path $root 'docs')) { Copy-Item (Join-Path $root 'docs') -Destination $out -Recurse -Force }
Copy-Item (Join-Path $root 'src\kok.exe.config') -Destination (Join-Path $out 'kok.exe.config') -Force
foreach ($name in @('README.md','README.en.md','ROADMAP.md','ROADMAP.en.md','CHANGELOG.md','CHANGELOG.en.md','SECURITY.md','SECURITY.en.md','LICENSE','THIRD_PARTY_NOTICES.md','SUPPORT.md','CODE_OF_CONDUCT.md','CONTRIBUTING.md')) { if (Test-Path (Join-Path $root $name)) { Copy-Item (Join-Path $root $name) -Destination $out -Force } }
if (Test-Path (Join-Path $sdk 'LICENSE.txt')) { Copy-Item (Join-Path $sdk 'LICENSE.txt') -Destination (Join-Path $out 'WEBVIEW2-LICENSE.txt') -Force }
if (Test-Path (Join-Path $sdk 'NOTICE.txt')) { Copy-Item (Join-Path $sdk 'NOTICE.txt') -Destination (Join-Path $out 'WEBVIEW2-NOTICE.txt') -Force }
if ($Test) {
    $testExe = Join-Path $out 'kok.Tests.exe'
    $testArgs = @('/nologo','/target:exe','/platform:x64','/utf8output',('/out:'+$testExe),('/reference:'+(Join-Path $out 'kok.exe')))
    foreach ($reference in $references) { $testArgs += '/reference:' + $reference }
    $testArgs += Join-Path $root 'tests\NativeTests.cs'
    $testArgs += Join-Path $root 'tests\TranslationTests.cs'
    $testArgs += Join-Path $root 'tests\OrganizationTests.cs'
    $testArgs += Join-Path $root 'tests\PrivacyProviderTests.cs'
    $testArgs += Join-Path $root 'tests\Preview041Tests.cs'
    $testArgs += Join-Path $root 'tests\DocumentTests.cs'
    $testArgs += Join-Path $root 'tests\KnowledgeTests.cs'
    $testArgs += Join-Path $root 'tests\KnowledgeWorkspaceTests.cs'
    $testArgs += Join-Path $root 'tests\PresentationRulesTests.cs'
    $testArgs += Join-Path $root 'tests\AgentTests.cs'
    $testArgs += Join-Path $root 'tests\AgentTaskTests.cs'
    $testArgs += Join-Path $root 'tests\AgentVisionTests.cs'
    $testArgs += Join-Path $root 'tests\AgentWebTests.cs'
    $testArgs += Join-Path $root 'tests\AgentExecutionTests.cs'
    $testArgs += Join-Path $root 'tests\AgentScheduleTests.cs'
    $testArgs += Join-Path $root 'tests\AgentMemoryTests.cs'
    $testArgs += Join-Path $root 'tests\AgentOrchestrationTests.cs'
    $testArgs += Join-Path $root 'tests\AgentKernelTests.cs'
    $testArgs += Join-Path $root 'tests\AgentPlatformTests.cs'
    $testArgs += Join-Path $root 'tests\AgentReliability090Tests.cs'
    $testArgs += Join-Path $root 'tests\McpTests.cs'
    $testArgs += Join-Path $root 'tests\McpRemoteTests.cs'
    $testArgs += Join-Path $root 'tests\McpOAuthTests.cs'
    $testArgs += Join-Path $root 'tests\ModuleTests.cs'
    $testArgs += Join-Path $root 'tests\WarehouseTests.cs'
    $testArgs += Join-Path $root 'tests\ContentServicesTests.cs'
    $testArgs += Join-Path $root 'tests\LibraryConnectionTests.cs'
    $testArgs += Join-Path $root 'tests\SessionPersistenceTests.cs'
    & $compiler $testArgs
    if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
    Copy-Item (Join-Path $root 'src\kok.exe.config') -Destination ($testExe+'.config') -Force
    & $testExe
    if ($LASTEXITCODE -ne 0) { throw 'Native tests failed.' }
}
Write-Output "Built: $out\kok.exe"
