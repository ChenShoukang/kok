param()
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$researchRoot=Join-Path $projectRoot 'runtime\research'
$node=(Get-Command node.exe -CommandType Application -ErrorAction Stop | Select-Object -First 1).Source
$nodeVersion=(& $node -p 'process.versions.node').Trim()
if([int]($nodeVersion.Split('.')[0]) -ne 24){throw 'Building the research runtime requires Node.js 24 LTS.'}
$licenseDirectory=Join-Path $PSScriptRoot 'research-node'
New-Item -ItemType Directory -Path $licenseDirectory -Force | Out-Null
$licenseFile=Join-Path $licenseDirectory 'LICENSE.txt'
$licenseVersion=Join-Path $licenseDirectory 'version.txt'
if(-not(Test-Path -LiteralPath $licenseFile) -or -not(Test-Path -LiteralPath $licenseVersion) -or (Get-Content -LiteralPath $licenseVersion -Raw).Trim() -ne $nodeVersion){
    Invoke-WebRequest -UseBasicParsing -Uri ('https://raw.githubusercontent.com/nodejs/node/v'+$nodeVersion+'/LICENSE') -OutFile $licenseFile
    Set-Content -LiteralPath $licenseVersion -Value $nodeVersion -Encoding Ascii
}
$lockHash=(Get-FileHash -LiteralPath (Join-Path $researchRoot 'package-lock.json') -Algorithm SHA256).Hash
$installedHash=Join-Path $researchRoot 'node_modules\.kok-lock.sha256'
if(-not(Test-Path -LiteralPath $installedHash) -or (Get-Content -LiteralPath $installedHash -Raw).Trim() -ne $lockHash){
    Push-Location $researchRoot
    try{
        $npmArguments=@('ci','--include=dev','--ignore-scripts','--no-audit','--no-fund','--fetch-retries=0','--fetch-timeout=30000')
        if($env:KOK_NPM_CACHE){$npmArguments+=@('--cache',$env:KOK_NPM_CACHE)}
        & npm.cmd @npmArguments
        if($LASTEXITCODE -ne 0){throw 'Research runtime dependency restore failed.'}
    }finally{Pop-Location}
    Set-Content -LiteralPath $installedHash -Value $lockHash -Encoding Ascii
}
& $node (Join-Path $researchRoot 'build.mjs')
if($LASTEXITCODE -ne 0){throw 'Research runtime bundle failed.'}
