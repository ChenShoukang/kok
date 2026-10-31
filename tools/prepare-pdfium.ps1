$ErrorActionPreference='Stop'
[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::SystemDefault
$cache=Join-Path $PSScriptRoot 'pdfium-source'
$output=Join-Path $cache 'pdfium.dll'
New-Item -ItemType Directory -Force -Path $cache | Out-Null
foreach($dependency in (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'pdfium-dependencies.json') -Raw | ConvertFrom-Json)){
    if($dependency.url -notmatch '^https://github\.com/bblanchon/pdfium-binaries/releases/download/[A-Za-z0-9./_-]+$' -or $dependency.entry -ne 'bin/pdfium.dll' -or $dependency.sha256 -notmatch '^[a-f0-9]{64}$' -or $dependency.dllSha256 -notmatch '^[a-f0-9]{64}$'){throw 'Invalid locked PDFium dependency.'}
    $archiveFile=Join-Path $cache ('pdfium-'+($dependency.tag -replace '/','-')+'.tgz')
    if(-not (Test-Path -LiteralPath $archiveFile)){
        Invoke-WebRequest -UseBasicParsing -Uri $dependency.url -OutFile $archiveFile
    }
    if((Get-FileHash -LiteralPath $archiveFile -Algorithm SHA256).Hash.ToLowerInvariant() -ne $dependency.sha256){throw 'PDFium package checksum mismatch.'}
    $tar=Get-Command tar.exe -ErrorAction Stop
    $extract=Join-Path $cache 'extract'
    Remove-Item -LiteralPath $extract -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force -Path $extract | Out-Null
    & $tar.Source -xzf $archiveFile -C $extract
    if($LASTEXITCODE -ne 0){throw 'PDFium package extraction failed.'}
    $dll=Join-Path $extract ($dependency.entry -replace '/','\')
    if(-not (Test-Path -LiteralPath $dll)){throw 'PDFium package does not contain the locked DLL.'}
    if((Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash.ToLowerInvariant() -ne $dependency.dllSha256){throw 'PDFium library checksum mismatch.'}
    Copy-Item -LiteralPath $dll -Destination $output -Force
    Remove-Item -LiteralPath $extract -Recurse -Force
    # License texts are versioned manually under docs/licenses; builds never rewrite tracked files.
}
