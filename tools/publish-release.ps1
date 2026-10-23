$ErrorActionPreference = 'Stop'
$repo = $env:GITHUB_REPOSITORY
if ($repo -ne 'ChenShoukang/kok') { throw 'Release publishing is configured for ChenShoukang/kok.' }
if (-not $env:GITHUB_TOKEN) { throw 'GITHUB_TOKEN is required inside GitHub Actions.' }
$headers = @{ Authorization = 'Bearer ' + $env:GITHUB_TOKEN; Accept = 'application/vnd.github+json'; 'X-GitHub-Api-Version' = '2022-11-28' }
$version = (Get-Content (Join-Path $PSScriptRoot '..\package.json') -Raw | ConvertFrom-Json).version
$tag = 'v' + $version
$base = 'https://api.github.com/repos/' + $repo
$existing = $null
try { $existing = Invoke-RestMethod -Uri ($base + '/releases/tags/' + $tag) -Headers $headers }
catch { if ([int]$_.Exception.Response.StatusCode -ne 404) { throw } }
if ($existing) { Write-Output ('Release already exists: ' + $existing.html_url); exit 0 }
$notes = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\docs\RELEASE_NOTES.md') -Raw -Encoding UTF8

$body = @{ tag_name = $tag; target_commitish = $env:GITHUB_SHA; name = 'kok ' + $version; body = $notes; draft = $true; prerelease = $false } | ConvertTo-Json
$release = Invoke-RestMethod -Method Post -Uri ($base + '/releases') -Headers $headers -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($body))
$upload = $release.upload_url -replace '\{.*$', ''
foreach ($name in @("kok-$version-win-x64.zip", "kok-$version-win-x64.zip.sha256")) {
    $file = Join-Path $PSScriptRoot ('..\dist\' + $name)
    if (-not (Test-Path -LiteralPath $file)) { throw "Missing release asset: $name" }
    Invoke-RestMethod -Method Post -Uri ($upload + '?name=' + [Uri]::EscapeDataString($name)) -Headers $headers -ContentType 'application/octet-stream' -InFile $file | Out-Null
}
$publish = @{ draft = $false; make_latest = 'true' } | ConvertTo-Json
$release = Invoke-RestMethod -Method Patch -Uri ($base + '/releases/' + $release.id) -Headers $headers -ContentType 'application/json' -Body $publish
Write-Output ('Published: ' + $release.html_url)
