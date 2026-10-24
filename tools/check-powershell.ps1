$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$files = @(Get-ChildItem -LiteralPath $root -Filter '*.ps1' -File) + @(Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.ps1' -File -Recurse)
$failures = @()
foreach ($file in $files) {
    $tokens = $null
    $parseErrors = $null
    [System.Management.Automation.Language.Parser]::ParseFile($file.FullName, [ref]$tokens, [ref]$parseErrors) | Out-Null
    foreach ($parseError in $parseErrors) { $failures += $file.Name + ':' + $parseError.Extent.StartLineNumber + ' [' + $parseError.ErrorId + ']' }
}
if ($failures.Count) { $failures | Write-Output; throw 'PowerShell script parsing failed.' }
Write-Output ('PowerShell ' + $PSVersionTable.PSVersion.Major + ' scripts valid: ' + $files.Count + ' files.')
