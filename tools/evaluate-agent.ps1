param([switch]$Live,[string]$ApiKeyFile,[string]$Model='deepseek-flash',[string]$Round='round-1',[string[]]$Cases=@(),[ValidateSet('reliability','quality','requirements')][string]$Suite='reliability',[switch]$Holdout,[ValidateSet('a','b')][string]$Variant='a',[int]$MaxRequests=150,[string]$OutputDirectory,[string]$AppDirectory)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
if(-not $Live){throw 'This evaluator makes paid DeepSeek requests. Pass -Live with an authorized local API key file.'}
if(-not $ApiKeyFile -or -not(Test-Path -LiteralPath $ApiKeyFile -PathType Leaf)){throw 'A readable local DeepSeek API key file is required.'}
if($Round -notmatch '^[a-zA-Z0-9][a-zA-Z0-9-]{0,60}$' -or $Model -notmatch '^[a-zA-Z0-9][a-zA-Z0-9._-]{0,80}$' -or $MaxRequests -lt 1 -or $MaxRequests -gt 360){throw 'Invalid evaluation settings.'}
if($Cases | Where-Object {$_ -notmatch '^[a-z][a-z0-9-]{0,60}$'}){throw 'Invalid case selector.'}
if($Holdout -and $Suite -ne 'reliability'){throw 'The frozen variants belong to the 18-case reliability suite.'}
$evaluationVersion=(Get-Content -LiteralPath (Join-Path $projectRoot 'package.json') -Raw|ConvertFrom-Json).version.Replace('.','')
$outputRoot=if($OutputDirectory){[IO.Path]::GetFullPath($OutputDirectory)}else{Join-Path $projectRoot ('test-output\evaluation-v'+$evaluationVersion)}
$testBoundary=[IO.Path]::GetFullPath((Join-Path $projectRoot 'test-output')).TrimEnd('\')+'\'
if(-not($outputRoot+'\').StartsWith($testBoundary,[StringComparison]::OrdinalIgnoreCase)){throw 'Evaluation evidence must stay inside the project test-output directory.'}
$roundRoot=Join-Path $outputRoot $Round
if(Test-Path -LiteralPath $roundRoot){throw 'Existing round evidence is preserved. Choose a distinct round name; the cumulative ledger will be retained.'}
$app=if($AppDirectory){[IO.Path]::GetFullPath($AppDirectory)}else{Join-Path $projectRoot 'dist\kok'}
if($AppDirectory -and -not($app+'\').StartsWith($testBoundary,[StringComparison]::OrdinalIgnoreCase)){throw 'Alternative test applications must be staged under test-output.'}
if(-not(Test-Path -LiteralPath (Join-Path $app 'runtime\research\business.mjs'))){throw 'Build the current app before evaluating.'}
New-Item -ItemType Directory -Force -Path $roundRoot | Out-Null
$probe=Join-Path $roundRoot 'probe'
New-Item -ItemType Directory -Force -Path $probe | Out-Null
foreach($file in Get-ChildItem -LiteralPath $app -File){if($file.Name -eq 'kok.exe' -or $file.Extension -eq '.dll' -or $file.Name -eq 'kok.exe.config'){Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $probe $file.Name) -Force}}
$target=Join-Path $probe 'kok.Tests.exe'
$compiler=Join-Path $projectRoot 'tools\roslyn\tasks\net472\csc.exe'
$arguments=@('/nologo','/langversion:latest','/target:exe','/platform:x64','/utf8output',('/out:'+$target),('/reference:'+(Join-Path $probe 'kok.exe')),'/reference:System.dll','/reference:System.Core.dll','/reference:System.Net.Http.dll','/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll','/reference:System.Web.Extensions.dll','/reference:System.IO.Compression.dll','/reference:System.IO.Compression.FileSystem.dll',(Join-Path $projectRoot 'tests\AgentEvaluationRunner.cs'))
& $compiler $arguments
if($LASTEXITCODE -ne 0){throw 'Evaluation runner compilation failed.'}
$previous=@{};foreach($name in @('KOK_LIVE_AI','KOK_LIVE_MAX_REQUESTS','KOK_LIVE_LEDGER')){$previous[$name]=[Environment]::GetEnvironmentVariable($name,'Process')}
$env:KOK_LIVE_AI='1';$env:KOK_LIVE_MAX_REQUESTS=[string]$MaxRequests;$env:KOK_LIVE_LEDGER=Join-Path $outputRoot 'request-ledger.txt'
try{
    $fingerprint=& node (Join-Path $projectRoot 'tools\source-fingerprint.cjs') --runtime $app
    if($LASTEXITCODE -ne 0){throw 'Unable to fingerprint this evaluation candidate.'}
    [IO.File]::WriteAllText((Join-Path $roundRoot 'fingerprint.json'),($fingerprint -join "`n"),(New-Object Text.UTF8Encoding($false)))
    $samples=if($Suite -eq 'requirements'){'tests\fixtures\requirements-v0163.json'}elseif($Suite -eq 'quality'){'tests\fixtures\quality-v0162.json'}else{'tests\fixtures\reliability-v0161.json'}
    $caseSelector=if($Cases.Count){$Cases -join ','}else{'all'}
    $variantFile=if(!$Holdout){'standard'}elseif($Variant -eq 'b'){Join-Path $projectRoot 'tests\fixtures\reliability-holdout-b-v0163.json'}else{Join-Path $projectRoot 'tests\fixtures\reliability-holdout-v0163.json'}
    & $target $roundRoot $app (Join-Path $projectRoot $samples) ([IO.Path]::GetFullPath($ApiKeyFile)) $Model $caseSelector $variantFile
    if($LASTEXITCODE -ne 0){throw 'Some evaluation cases did not meet their checks. All attempts and requests remain in the evidence and cumulative ledger.'}
}finally{foreach($name in $previous.Keys){[Environment]::SetEnvironmentVariable($name,$previous[$name],'Process')}}
