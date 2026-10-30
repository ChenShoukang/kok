param([string]$Archive,[string[]]$Suites=@('agent','agent-interactions','agent-reliable','agent-runtime','agent-kernel','mcp','agent-platform'))
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$version=(Get-Content -LiteralPath (Join-Path $projectRoot 'package.json') -Raw|ConvertFrom-Json).version
if(-not $Archive){$Archive=Join-Path $projectRoot ("dist\kok-$version-win-x64.zip")}
$archivePath=(Resolve-Path -LiteralPath $Archive).Path
$hash=(Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
$checksumPath=$archivePath+'.sha256'
if(-not (Test-Path -LiteralPath $checksumPath) -or (Get-Content -LiteralPath $checksumPath -Raw).Trim().Split(' ')[0] -ne $hash){throw 'The package SHA-256 file is missing or does not match.'}
$evidenceRoot=Join-Path $projectRoot ('test-output\package-'+[guid]::NewGuid().ToString('N'))
$unpacked=Join-Path $evidenceRoot 'unpacked'
New-Item -ItemType Directory -Path $unpacked -Force|Out-Null
$boundary=[IO.Path]::GetFullPath($unpacked).TrimEnd([IO.Path]::DirectorySeparatorChar)+[IO.Path]::DirectorySeparatorChar
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip=[IO.Compression.ZipFile]::OpenRead($archivePath)
try{
    foreach($entry in $zip.Entries){
        $destination=[IO.Path]::GetFullPath((Join-Path $unpacked $entry.FullName))
        if(-not $destination.StartsWith($boundary,[StringComparison]::OrdinalIgnoreCase)){throw 'Package entry would escape its isolated extraction directory.'}
    }
}finally{$zip.Dispose()}
[IO.Compression.ZipFile]::ExtractToDirectory($archivePath,$unpacked)
$executable=Join-Path $unpacked 'kok\kok.exe'
if(-not (Test-Path -LiteralPath $executable) -or [Diagnostics.FileVersionInfo]::GetVersionInfo($executable).ProductVersion -ne $version){throw 'The extracted executable version does not match package.json.'}
$fingerprintJson=& node (Join-Path $projectRoot 'tools\source-fingerprint.cjs') --runtime (Split-Path -Parent $executable)
if($LASTEXITCODE -ne 0){throw 'Unable to fingerprint the extracted package.'}
$record=@{format='kok.package.validation';version=$version;archive=[IO.Path]::GetFileName($archivePath);sha256=$hash;started=[DateTime]::UtcNow.ToString('o');fingerprint=($fingerprintJson|ConvertFrom-Json);suites=$Suites;passed=$false}
try{
    & (Join-Path $PSScriptRoot 'test-desktop.ps1') -Executable $executable -Suites $Suites -AuditDirectory (Join-Path $evidenceRoot 'desktop')
    $run=Get-Content -LiteralPath (Join-Path $evidenceRoot 'desktop\run.json') -Raw|ConvertFrom-Json
    if(-not $run.passed -or $run.fingerprint.runtime.sha256 -ne $record.fingerprint.runtime.sha256 -or $run.fingerprint.source.sha256 -ne $record.fingerprint.source.sha256){throw 'Desktop evidence does not validate this exact package and source.'}
    if((Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant() -ne $hash){throw 'Archive changed during package testing.'}
    $record.passed=$true
}finally{
    $record.finished=[DateTime]::UtcNow.ToString('o')
    $record|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $evidenceRoot 'archive.json') -Encoding utf8
}
Write-Output ('Verified package evidence: '+$evidenceRoot)
