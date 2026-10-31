# CI/test setup only. The shipped app never installs a runtime silently.
# https://learn.microsoft.com/microsoft-edge/webview2/concepts/distribution
param([string]$CoreAssembly)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
if(-not $CoreAssembly){$CoreAssembly=Join-Path $projectRoot 'dist\kok\Microsoft.Web.WebView2.Core.dll'}
$core=(Resolve-Path -LiteralPath $CoreAssembly).Path
Add-Type -Path $core
function Get-TestRuntimeVersion {
    try { return [Microsoft.Web.WebView2.Core.CoreWebView2Environment]::GetAvailableBrowserVersionString() } catch { return '' }
}
$version=Get-TestRuntimeVersion
if(-not $version){
    if(-not $env:RUNNER_TEMP){throw 'Automatic runtime setup is restricted to an explicit CI runner.'}
    $installer=Join-Path $env:RUNNER_TEMP 'kok-webview2-setup.exe'
    Invoke-WebRequest -Uri 'https://go.microsoft.com/fwlink/p/?LinkId=2124703' -OutFile $installer
    $signature=Get-AuthenticodeSignature -LiteralPath $installer
    if($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation,'){throw 'WebView2 installer signature is not valid.'}
    $setup=Start-Process -FilePath $installer -ArgumentList @('/silent','/install') -PassThru -WindowStyle Hidden
    if(-not $setup.WaitForExit(120000)){throw 'WebView2 setup timed out.'}
    if($setup.ExitCode -notin @(0,3010)){throw ('WebView2 setup failed: '+$setup.ExitCode)}
    $version=Get-TestRuntimeVersion
    if(-not $version){throw 'No WebView2 runtime is available after setup.'}
}
Write-Output ('WebView2 test runtime: '+$version)
