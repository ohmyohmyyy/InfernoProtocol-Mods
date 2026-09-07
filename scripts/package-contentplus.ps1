param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Inferno Protocol',
    [switch]$SkipBuild
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
if (-not $SkipBuild) {
    & (Join-Path $repo 'FieldQuests\build.ps1') -GameDir $GameDir
    if ($LASTEXITCODE -ne 0) { throw 'ContentPlus build failed.' }
}
$meta = Join-Path $repo 'thunderstore\ContentPlus'
$manifest = Get-Content -LiteralPath (Join-Path $meta 'manifest.json') -Raw | ConvertFrom-Json
if ($manifest.name -ne 'ContentPlus' -or $manifest.version_number -notmatch '^\d+\.\d+\.\d+$' -or
    $manifest.description.Length -gt 250 -or [string]::IsNullOrWhiteSpace($manifest.description) -or
    @($manifest.dependencies).Count -ne 1 -or $manifest.dependencies[0] -ne 'FrankMods-InfernoProtocolInteropFix-1.0.0') {
    throw 'Invalid ContentPlus manifest.'
}
$dll = Join-Path $repo 'FieldQuests\bin\Release\net6.0\InfernoProtocol.FieldQuests.dll'
if ([version](Get-Item -LiteralPath $dll).VersionInfo.FileVersion -ne [version]($manifest.version_number + '.0')) {
    throw 'DLL and manifest versions differ.'
}
Add-Type -AssemblyName System.Drawing
$icon = Join-Path $meta 'icon.png'
$img = [System.Drawing.Image]::FromFile($icon)
try {
    if ($img.Width -ne 256 -or $img.Height -ne 256 -or $img.RawFormat.Guid -ne [System.Drawing.Imaging.ImageFormat]::Png.Guid) {
        throw 'Icon must be a 256x256 PNG.'
    }
} finally { $img.Dispose() }
$bytes = [IO.File]::ReadAllBytes($icon)
$position = 8
while ($position -lt $bytes.Length) {
    if ($position + 12 -gt $bytes.Length) { throw 'Truncated PNG.' }
    $lengthBytes = [byte[]]$bytes[$position..($position + 3)]
    [Array]::Reverse($lengthBytes)
    $length = [BitConverter]::ToUInt32($lengthBytes, 0)
    $kind = [Text.Encoding]::ASCII.GetString($bytes, $position + 4, 4)
    if ($kind -notin @('IHDR', 'PLTE', 'IDAT', 'IEND')) { throw "Unexpected icon metadata: $kind" }
    $position += 12 + [int64]$length
    if ($position -gt $bytes.Length) { throw 'Invalid PNG chunk.' }
}
$output = Join-Path $repo 'dist\contentplus'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$archive = Join-Path $output ('ContentPlus-' + $manifest.version_number + '.zip')
if (Test-Path -LiteralPath $archive) { throw "Archive already exists; preserve or move it before rebuilding: $archive" }
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.IO.Compression
$zip = [IO.Compression.ZipFile]::Open($archive, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($name in @('manifest.json', 'README.md', 'CHANGELOG.md', 'icon.png')) {
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, (Join-Path $meta $name), $name) | Out-Null
    }
    [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $dll, 'BepInEx/plugins/FieldQuests/InfernoProtocol.FieldQuests.dll') | Out-Null
} finally { $zip.Dispose() }
$zip = [IO.Compression.ZipFile]::OpenRead($archive)
try {
    if ($zip.Entries.Count -ne 5) { throw 'Unexpected package entries.' }
    $zip.Entries | Select-Object FullName, Length
} finally { $zip.Dispose() }
Get-FileHash -LiteralPath $archive -Algorithm SHA256
Write-Output "Validated ContentPlus package: $archive"
