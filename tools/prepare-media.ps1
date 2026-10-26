$ErrorActionPreference='Stop'
[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::SystemDefault
$cache=Join-Path $PSScriptRoot 'media-source'
New-Item -ItemType Directory -Force -Path $cache | Out-Null
$archive=Join-Path $cache 'taglibsharp.2.3.0.nupkg'
if(-not(Test-Path -LiteralPath $archive)){Invoke-WebRequest -UseBasicParsing -Uri 'https://api.nuget.org/v3-flatcontainer/taglibsharp/2.3.0/taglibsharp.2.3.0.nupkg' -OutFile $archive}
if((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne '3C3F5B55988F69E0BC84DC760FEB8351FEF4D5C09A322D88462E683D334DFBFC'){throw 'Audio metadata dependency checksum mismatch.'}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip=[IO.Compression.ZipFile]::OpenRead($archive)
try{[IO.Compression.ZipFileExtensions]::ExtractToFile($zip.GetEntry('lib/net462/TagLibSharp.dll'),(Join-Path $cache 'TagLibSharp.dll'),$true)}finally{$zip.Dispose()}
if((Get-FileHash -LiteralPath (Join-Path $cache 'TagLibSharp.dll') -Algorithm SHA256).Hash -ne 'B1833A41AB1E933F7B006E5DB15300B7223BFCCC2C3B6689D49A9171DD27DE1D'){throw 'Audio metadata library checksum mismatch.'}
