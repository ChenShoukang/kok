$ErrorActionPreference='Stop'
[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::SystemDefault
$project=Split-Path $PSScriptRoot -Parent
$cache=Join-Path $PSScriptRoot 'agent-source'
$output=Join-Path $cache 'lib'
New-Item -ItemType Directory -Force -Path $cache,$output | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
foreach($dependency in (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'agent-dependencies.json') -Raw | ConvertFrom-Json)){
    if($dependency.url -notmatch '^https://api\.nuget\.org/v3-flatcontainer/' -or $dependency.entry -notmatch '^lib/(net462|netstandard2\.0)/[A-Za-z0-9.]+\.dll$' -or $dependency.sha256 -notmatch '^[a-f0-9]{64}$'){throw 'Invalid locked Agent dependency.'}
    $archiveFile=Join-Path $cache ($dependency.id.ToLowerInvariant()+'.'+$dependency.version+'.nupkg')
    if(-not (Test-Path -LiteralPath $archiveFile)){
        Invoke-WebRequest -UseBasicParsing -Uri $dependency.url -OutFile $archiveFile
    }
    if((Get-FileHash -LiteralPath $archiveFile -Algorithm SHA256).Hash.ToLowerInvariant() -ne $dependency.sha256){throw 'Agent package checksum mismatch.'}
    $zip=[IO.Compression.ZipFile]::OpenRead($archiveFile)
    try{
        $entry=$zip.GetEntry($dependency.entry);if(-not $entry -or $entry.Length -gt 16MB){throw 'Invalid Agent library package.'}
        $dll=Join-Path $output ($dependency.id+'.dll')
        [IO.Compression.ZipFileExtensions]::ExtractToFile($entry,$dll,$true)
        if((Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash.ToLowerInvariant() -ne $dependency.dllSha256){throw 'Agent library checksum mismatch.'}
        # License texts are versioned manually under docs/licenses/agent; builds never rewrite tracked files.
    }finally{$zip.Dispose()}
}
