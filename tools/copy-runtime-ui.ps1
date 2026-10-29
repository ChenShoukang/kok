param([Parameter(Mandatory=$true)][string]$Destination)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$uiRoot=(Join-Path $projectRoot 'ui').TrimEnd('\')+'\'
$runtimeRoot=[IO.Path]::GetFullPath($Destination).TrimEnd('\')+'\'
$allowedRoot=[IO.Path]::GetFullPath((Join-Path $projectRoot 'dist')).TrimEnd('\')+'\'
if(-not $runtimeRoot.StartsWith($allowedRoot,[StringComparison]::OrdinalIgnoreCase)){throw 'Runtime UI destination must be inside the project dist directory.'}
$sourceStyles=@{}
$manifest=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'ui-styles.json') -Raw | ConvertFrom-Json
foreach($bundle in $manifest.PSObject.Properties){foreach($file in $bundle.Value){$sourceStyles[$file.Replace('\','/')]=$true}}
foreach($file in Get-ChildItem -LiteralPath $uiRoot -File -Recurse){
    $relative=$file.FullName.Substring($uiRoot.Length).Replace('\','/')
    if($sourceStyles.ContainsKey($relative)){continue}
    $target=[IO.Path]::GetFullPath((Join-Path $runtimeRoot $relative))
    if(-not $target.StartsWith($runtimeRoot,[StringComparison]::OrdinalIgnoreCase)){throw 'UI asset escapes its destination.'}
    New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($target)) -Force | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $target -Force
}
