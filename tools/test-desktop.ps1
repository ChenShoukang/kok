param([string[]]$Suites=@('ui','features','translation','i18n','minimal','drop','workspaces','video-layout','refinements','menus-covers','compatibility','organizer','companion','private-space','app-modes','recovery','agent','agent-interactions','agent-reliable','agent-runtime','agent-kernel','mcp'),[string]$LargeMediaFile,[string]$RealFilesFolder,[string]$Executable,[string]$AuditDirectory)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$executable=if($Executable){(Resolve-Path -LiteralPath $Executable).Path}else{Join-Path $projectRoot 'dist\kok\kok.exe'}
$testBoundary=[IO.Path]::GetFullPath((Join-Path $projectRoot 'test-output')).TrimEnd([IO.Path]::DirectorySeparatorChar)+[IO.Path]::DirectorySeparatorChar
$auditRoot=if($AuditDirectory){[IO.Path]::GetFullPath($AuditDirectory)}else{Join-Path $projectRoot ('test-output\full-'+[DateTime]::Now.ToString('yyyyMMdd-HHmmss')+'-'+[guid]::NewGuid().ToString('N').Substring(0,8))}
if(-not $auditRoot.StartsWith($testBoundary,[StringComparison]::OrdinalIgnoreCase)){throw 'Desktop evidence must stay inside the project test-output directory.'}
New-Item -ItemType Directory -Path $auditRoot -Force | Out-Null
$fingerprintJson=& node (Join-Path $projectRoot 'tools\source-fingerprint.cjs') --runtime (Split-Path -Parent $executable)
if($LASTEXITCODE -ne 0){throw 'Unable to fingerprint the test source and runtime.'}
$runRecord=@{started=[DateTime]::UtcNow.ToString('o');version=[Diagnostics.FileVersionInfo]::GetVersionInfo($executable).ProductVersion;executable=$executable;fingerprint=($fingerprintJson|ConvertFrom-Json);requestedSuites=$Suites;passed=$false}
$runRecord|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $auditRoot 'run.json') -Encoding utf8
$results=@()
foreach($suite in $Suites){
    if($suite -notmatch '^[a-z0-9-]+$' -or -not (Test-Path -LiteralPath (Join-Path $projectRoot ('tests\'+$suite+'.cjs')))){throw 'Unknown desktop test suite'}
    $profile=Join-Path $auditRoot $suite
    New-Item -ItemType Directory -Path $profile -Force | Out-Null
    $preferences=@{appMode='smart';smartModeNoticeSeen=$true;theme='light';uiLanguage='zh';startPage='overview';skin='graphite'}
    if($suite -eq 'app-modes'){$preferences=@{}}
    $sources=@()
    if($suite -eq 'organizer'){
        $files=Join-Path $projectRoot 'test-output\organizer-ui\我的资料';New-Item -ItemType Directory -Path $files -Force | Out-Null
        [IO.File]::WriteAllText((Join-Path $files '旅行计划.md'),'# 旅行计划'+[Environment]::NewLine+'周末去海边旅行、拍照与阅读。',[Text.UTF8Encoding]::new($false))
        [IO.File]::WriteAllText((Join-Path $files '学习笔记.txt'),'今日阅读：设计与生活。',[Text.UTF8Encoding]::new($false))
        $sources=@(@{id='test-source';path=$files;enabled=$true;scanned=''})
    }
    if($suite -eq 'companion'){
        $files=Join-Path $projectRoot 'test-output\companion-ui\files';New-Item -ItemType Directory -Path $files -Force | Out-Null
        Add-Type -AssemblyName System.Drawing
        $bitmap=[Drawing.Bitmap]::new(360,640);$graphics=[Drawing.Graphics]::FromImage($bitmap);$graphics.Clear([Drawing.Color]::LightSteelBlue);$bitmap.Save((Join-Path $files 'portrait.png'),[Drawing.Imaging.ImageFormat]::Png);$graphics.Dispose();$bitmap.Dispose()
        $sources=@(@{id='companion-source';path=$files;enabled=$true;scanned=''})
    }
    $database=@{version=1;items=@();settings=$preferences;collections=@();fonts=@();watchFolders=$sources;ignoredAutoPaths=@()}
    [IO.File]::WriteAllText((Join-Path $profile 'library.json'),($database|ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($false))
    if($suite -eq 'preview-041'){& node (Join-Path $projectRoot 'tests\preview-041.cjs') --prepare $profile;if($LASTEXITCODE -ne 0){throw 'Preview fixture preparation failed.'}}
    if($suite -eq 'photo-tools'){& node (Join-Path $projectRoot 'tests\photo-tools.cjs') --prepare $profile;if($LASTEXITCODE -ne 0){throw 'Photo fixture preparation failed.'}}
    if($suite -eq 'warehouse-interactions'){& node (Join-Path $projectRoot 'tests\photo-tools.cjs') --prepare $profile;if($LASTEXITCODE -ne 0){throw 'Warehouse interaction fixture preparation failed.'}}
    if($suite -eq 'native-downloads'){& node (Join-Path $projectRoot 'tests\native-downloads.cjs') --prepare $profile;if($LASTEXITCODE -ne 0){throw 'Native download fixture preparation failed.'}}
    if($suite -eq 'media-downloads'){& node (Join-Path $projectRoot 'tests\media-downloads.cjs') --prepare $profile;if($LASTEXITCODE -ne 0){throw 'Media download fixture preparation failed.'}}
    if($suite -eq 'kok-downloads'){& node (Join-Path $projectRoot 'tests\kok-downloads.cjs') --prepare $profile;if($LASTEXITCODE -ne 0){throw 'kok download fixture preparation failed.'}}
    if($suite -eq 'site-adapters'){& node (Join-Path $projectRoot 'tests\site-adapters.cjs') --prepare $profile;if($LASTEXITCODE -ne 0){throw 'Site adapter fixture preparation failed.'}}
    if($suite -eq 'flat-chrome'){& node (Join-Path $projectRoot 'tests\flat-chrome.cjs') --prepare $profile;if($LASTEXITCODE -ne 0){throw 'Window chrome fixture preparation failed.'}}
    if($suite -eq 'study-tools'){& node (Join-Path $projectRoot 'tests\study-tools.cjs') --prepare $profile;if($LASTEXITCODE -ne 0){throw 'Study fixture preparation failed.'}}
    if($suite -eq 'creative-music'){& node (Join-Path $projectRoot 'tests\creative-music.cjs') --prepare $profile;if($LASTEXITCODE -ne 0){throw 'Creative/music fixture preparation failed.'}}
    if($suite -eq 'translation-workspace'){& node (Join-Path $projectRoot 'tests\translation-workspace.cjs') --prepare $profile;if($LASTEXITCODE -ne 0){throw 'Translation workspace fixture preparation failed.'}}
    if($suite -eq 'selection-motion'){& node (Join-Path $projectRoot 'tests\selection-motion.cjs') --prepare $profile;if($LASTEXITCODE -ne 0){throw 'Selection motion fixture preparation failed.'}}
    if($suite -eq 'reading-renewal'){& node (Join-Path $projectRoot 'tests\reading-renewal.cjs') --prepare $profile;if($LASTEXITCODE -ne 0){throw 'Reading renewal fixture preparation failed.'}}
    if($suite -eq 'reading-flow'){& node (Join-Path $projectRoot 'tests\reading-flow.cjs') --prepare $profile;if($LASTEXITCODE -ne 0){throw 'Reading flow fixture preparation failed.'}}
    if($suite -eq 'window-drag'){& node (Join-Path $projectRoot 'tests\window-drag.cjs') --prepare $profile;if($LASTEXITCODE -ne 0){throw 'Window drag fixture preparation failed.'}}
    if($suite -eq 'calm-motion'){& node (Join-Path $projectRoot 'tests\calm-motion.cjs') --prepare $profile;if($LASTEXITCODE -ne 0){throw 'Calm motion fixture preparation failed.'}}
    if($suite -eq 'workspace-clarity'){& node (Join-Path $projectRoot 'tests\workspace-clarity.cjs') --prepare $profile;if($LASTEXITCODE -ne 0){throw 'Workspace clarity fixture preparation failed.'}}
    if($suite -eq 'documents'){& node (Join-Path $projectRoot 'tests\documents.cjs') --prepare $profile;if($LASTEXITCODE -ne 0){throw 'Document fixture preparation failed.'}}
    if($suite -eq 'knowledge'){& node (Join-Path $projectRoot 'tests\knowledge.cjs') --prepare $profile;if($LASTEXITCODE -ne 0){throw 'Knowledge fixture preparation failed.'}}
    if($suite -eq 'graphite'){& node (Join-Path $projectRoot 'tests\graphite.cjs') --prepare $profile;if($LASTEXITCODE -ne 0){throw 'Graphite fixture preparation failed.'}}
    if($suite -eq 'presentations-agent'){& node (Join-Path $projectRoot 'tests\presentations-agent.cjs') --prepare $profile;if($LASTEXITCODE -ne 0){throw 'Presentation fixture preparation failed.'}}
    $process=Start-Process -FilePath $executable -ArgumentList @('--debug','--test-silent','--data-dir',('"'+$profile+'"')) -PassThru -WindowStyle Hidden
    $env:KOK_TEST_NATIVE_PID=[string]$process.Id
    $env:KOK_TEST_EXECUTABLE=$executable
    $env:KOK_TEST_PROFILE=$profile
    try{
        $ready=$false
        for($attempt=0;$attempt -lt 80;$attempt++){try{Invoke-RestMethod -Uri 'http://127.0.0.1:9225/json/version' -TimeoutSec 1 | Out-Null;$ready=$true;break}catch{Start-Sleep -Milliseconds 250}}
        if(-not $ready){throw 'Silent test window did not start'}
        Push-Location $projectRoot
        try{if($suite -eq 'large-media'){if(-not $LargeMediaFile){throw 'Large-media checks require -LargeMediaFile.'};& node ('tests/'+$suite+'.cjs') $LargeMediaFile}elseif($suite -eq 'real-files'){if(-not $RealFilesFolder){throw 'Real-file checks require -RealFilesFolder.'};& node ('tests/'+$suite+'.cjs') $RealFilesFolder}else{& node ('tests/'+$suite+'.cjs')};$code=$LASTEXITCODE}finally{Pop-Location}
        $results+=@{suite=$suite;passed=($code -eq 0)}
        $results|ConvertTo-Json | Set-Content -LiteralPath (Join-Path $auditRoot 'results.json') -Encoding utf8
        if($code -ne 0){throw ('Desktop test failed: '+$suite)}
        Write-Output ('SUITE PASS: '+$suite)
    }finally{
        Remove-Item Env:KOK_TEST_NATIVE_PID -ErrorAction SilentlyContinue
        Remove-Item Env:KOK_TEST_EXECUTABLE -ErrorAction SilentlyContinue
        Remove-Item Env:KOK_TEST_PROFILE -ErrorAction SilentlyContinue
        $owned=Get-Process -Id $process.Id -ErrorAction SilentlyContinue
        if($owned -and $owned.Path -eq $executable){$null=$owned.CloseMainWindow();if(-not $owned.WaitForExit(4000)){Stop-Process -Id $owned.Id}}
        for($attempt=0;$attempt -lt 20;$attempt++){try{Invoke-RestMethod -Uri 'http://127.0.0.1:9225/json/version' -TimeoutSec 1|Out-Null;Start-Sleep -Milliseconds 150}catch{break}}
    }
}
$finalFingerprintJson=& node (Join-Path $projectRoot 'tools\source-fingerprint.cjs') --runtime (Split-Path -Parent $executable)
if($LASTEXITCODE -ne 0){throw 'Unable to verify the test source and runtime after the run.'}
$finalFingerprint=$finalFingerprintJson|ConvertFrom-Json
if($runRecord.fingerprint.source.sha256 -ne $finalFingerprint.source.sha256 -or $runRecord.fingerprint.runtime.sha256 -ne $finalFingerprint.runtime.sha256){throw 'Source or runtime changed during desktop testing; this run cannot validate the final tree.'}
$runRecord.passed=$true
$runRecord.finished=[DateTime]::UtcNow.ToString('o')
$runRecord|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $auditRoot 'run.json') -Encoding utf8
Write-Output ('Desktop audit completed: '+$auditRoot)
