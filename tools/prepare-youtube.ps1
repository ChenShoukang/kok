$ErrorActionPreference='Stop'
[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::SystemDefault
$project=Split-Path $PSScriptRoot -Parent
$cache=Join-Path $PSScriptRoot 'youtube-source'
$output=Join-Path $cache 'lib'
$licenses=Join-Path $project 'docs\licenses\youtube'
New-Item -ItemType Directory -Force -Path $cache,$output,$licenses | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
foreach($dependency in (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'youtube-dependencies.json') -Raw | ConvertFrom-Json)){
    if($dependency.url -notmatch '^https://api\.nuget\.org/v3-flatcontainer/' -or $dependency.entry -notmatch '^lib/(net462|netstandard2\.0)/[A-Za-z0-9.]+\.dll$' -or $dependency.sha256 -notmatch '^[a-f0-9]{64}$'){throw 'Invalid locked YouTube dependency.'}
    $archiveFile=Join-Path $cache ($dependency.id.ToLowerInvariant()+'.'+$dependency.version+'.nupkg')
    if(-not (Test-Path -LiteralPath $archiveFile)){
        Invoke-WebRequest -UseBasicParsing -Uri $dependency.url -OutFile $archiveFile
    }
    if((Get-FileHash -LiteralPath $archiveFile -Algorithm SHA256).Hash.ToLowerInvariant() -ne $dependency.sha256){throw 'YouTube package checksum mismatch.'}
    $zip=[IO.Compression.ZipFile]::OpenRead($archiveFile)
    try{
        $entry=$zip.GetEntry($dependency.entry);if(-not $entry -or $entry.Length -gt 8MB){throw 'Invalid YouTube library package.'}
        $dll=Join-Path $output ($dependency.id+'.dll')
        [IO.Compression.ZipFileExtensions]::ExtractToFile($entry,$dll,$true)
        if((Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash.ToLowerInvariant() -ne $dependency.dllSha256){throw 'YouTube library checksum mismatch.'}
        $license=$zip.Entries | Where-Object {$_.FullName -match '(^|/)(LICENSE|License|LICENSE\.TXT|License\.txt|LICENSE\.md|LICENSE\.txt)$'} | Select-Object -First 1
        if($license){[IO.Compression.ZipFileExtensions]::ExtractToFile($license,(Join-Path $licenses ($dependency.id+'.txt')),$true)}
        $notice=$zip.Entries | Where-Object {$_.FullName -match '(^|/)THIRD[-_]?PARTY[-_]?NOTICES(?:\.TXT|\.txt)?$'} | Select-Object -First 1
        if($notice){[IO.Compression.ZipFileExtensions]::ExtractToFile($notice,(Join-Path $licenses ($dependency.id+'-notices.txt')),$true)}
    }finally{$zip.Dispose()}
}
