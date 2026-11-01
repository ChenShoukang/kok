param([switch]$Test)
# Serialize C# builds between parallel agents: build.ps1 rewrites dist\kok, so only one build may run at a time.
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$mutex=New-Object System.Threading.Mutex($false,'Local\kok-build-serializer')
try{
    if(-not $mutex.WaitOne([TimeSpan]::FromMinutes(15))){throw 'Timed out waiting for the C# build lock.'}
    try{
        $arguments=@('-NoProfile','-ExecutionPolicy','Bypass','-File',(Join-Path $root 'build.ps1'))
        if($Test){$arguments+='-Test'}
        & powershell @arguments
        exit $LASTEXITCODE
    }finally{$mutex.ReleaseMutex()}
}finally{$mutex.Dispose()}
