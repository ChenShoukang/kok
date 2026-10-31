param([switch]$Test,[switch]$CheckOnly)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$nodeCommand=Get-Command node.exe -CommandType Application -ErrorAction Stop
$npmCommand=Get-Command npm.cmd -CommandType Application -ErrorAction Stop
$nodeMajor=[int]((& $nodeCommand.Source -p 'parseInt(process.versions.node)').Trim())
if($nodeMajor -lt 22){throw 'Node.js 22 or newer is required for development. The packaged app does not need Node.js.'}
$probe=Join-Path $projectRoot ('.kok-write-probe-'+[guid]::NewGuid().ToString('N'))
try{[IO.File]::WriteAllText($probe,'write access')}catch{throw 'The checkout is not writable. Use a fresh directory owned by the current account.'}finally{if(Test-Path -LiteralPath $probe){Remove-Item -LiteralPath $probe -Force}}
Push-Location $projectRoot
try{
    & $npmCommand.Source ci --ignore-scripts --omit=optional
    if($LASTEXITCODE -ne 0){throw 'npm.cmd ci failed. Check the error above; use a fresh writable checkout for stale or inaccessible dependencies.'}
    & $nodeCommand.Source (Join-Path $PSScriptRoot 'vendor-sandbox.cjs')
    if($LASTEXITCODE -ne 0){throw 'QuickJS asset verification failed.'}
    & $npmCommand.Source run check
    if($LASTEXITCODE -ne 0){throw 'Static checks failed.'}
    if(-not $CheckOnly){& (Join-Path $projectRoot 'build.ps1') -Test:$Test;if($LASTEXITCODE -ne 0){throw 'Build or native tests failed.'}}
}finally{Pop-Location}
