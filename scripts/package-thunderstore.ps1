param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Inferno Protocol',
    [switch]$SkipBuild,
    [ValidateSet('BetterUI','UtilityWheel','BetterLights','Renovator','InfernoProtocolInteropFix')]
    [string[]]$Only
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$metadataRoot = Join-Path $repoRoot 'thunderstore'
$outputRoot = Join-Path $repoRoot 'dist\thunderstore'
$stagingRoot = Join-Path $outputRoot 'staging'
$bepInExDependency = 'BepInEx-BepInExPack_IL2CPP-6.0.755'
$interopFixDependency = 'FrankMods-InfernoProtocolInteropFix-1.0.0'

$packages = @(
    [pscustomobject]@{
        Folder = 'BetterUI'
        DllName = 'InfernoProtocol.BetterUI.dll'
        DllPath = Join-Path $repoRoot 'BetterUI\bin\Release\net6.0\InfernoProtocol.BetterUI.dll'
        InstallPath = 'BepInEx\plugins\BetterUI'
        Dependency = $interopFixDependency
        ExtraFiles = @()
    },
    [pscustomobject]@{
        Folder = 'UtilityWheel'
        DllName = 'InfernoProtocol.UtilityWheel.dll'
        DllPath = Join-Path $repoRoot 'UtilityWheel\bin\Release\net6.0\InfernoProtocol.UtilityWheel.dll'
        InstallPath = 'BepInEx\plugins\UtilityWheel'
        Dependency = $interopFixDependency
        ExtraFiles = @()
    },
    [pscustomobject]@{
        Folder = 'BetterLights'
        DllName = 'InfernoProtocol.BetterLights.dll'
        DllPath = Join-Path $repoRoot 'BetterLights\bin\Release\net6.0\InfernoProtocol.BetterLights.dll'
        InstallPath = 'BepInEx\plugins\BetterLights'
        Dependency = $interopFixDependency
        ExtraFiles = @()
    },
    [pscustomobject]@{
        Folder = 'Renovator'
        DllName = 'InfernoProtocol.Renovator.dll'
        DllPath = Join-Path $repoRoot 'Renovator\bin\Release\net6.0\InfernoProtocol.Renovator.dll'
        InstallPath = 'BepInEx\plugins\Renovator'
        Dependency = $interopFixDependency
        ExtraFiles = @()
    },
    [pscustomobject]@{
        Folder = 'InfernoProtocolInteropFix'
        DllName = 'InfernoProtocol.InteropFix.dll'
        DllPath = Join-Path $repoRoot 'InteropFix\bin\Release\net6.0\InfernoProtocol.InteropFix.dll'
        InstallPath = 'BepInEx\patchers\InfernoProtocolInteropFix'
        Dependency = $bepInExDependency
        ExtraFiles = @(
            Join-Path $repoRoot 'InteropFix\bin\Release\net6.0\dnlib.dll'
        )
    }
)

if ($Only.Count -gt 0) {
    $packages = @($packages | Where-Object { $_.Folder -in $Only })
}

function Assert-Manifest {
    param(
        [Parameter(Mandatory = $true)]$Manifest,
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$ExpectedDependency
    )

    $requiredKeys = @('name', 'version_number', 'website_url', 'description', 'dependencies')
    foreach ($key in $requiredKeys) {
        if ($Manifest.PSObject.Properties.Name -notcontains $key) {
            throw "$Path is missing required manifest key '$key'."
        }
    }

    if ($Manifest.name.Length -gt 128 -or $Manifest.name -notmatch '^[A-Za-z0-9_]+$') {
        throw "$Path has an invalid package name '$($Manifest.name)'."
    }

    if ($Manifest.version_number -notmatch '^[0-9]+\.[0-9]+\.[0-9]+$') {
        throw "$Path has an invalid semantic version '$($Manifest.version_number)'."
    }

    if ([string]::IsNullOrWhiteSpace($Manifest.description) -or $Manifest.description.Length -gt 250) {
        throw "$Path must have a non-empty description no longer than 250 characters."
    }

    if (-not [string]::IsNullOrWhiteSpace($Manifest.website_url)) {
        $website = $null
        if (-not [System.Uri]::TryCreate($Manifest.website_url, [System.UriKind]::Absolute, [ref]$website)) {
            throw "$Path has an invalid website_url."
        }
    }

    $dependencies = @($Manifest.dependencies)
    if ($dependencies.Count -ne 1 -or $dependencies[0] -ne $ExpectedDependency) {
        throw "$Path must depend on exactly $ExpectedDependency."
    }
}

function Assert-Icon {
    param([Parameter(Mandatory = $true)][string]$Path)

    Add-Type -AssemblyName System.Drawing
    $image = [System.Drawing.Image]::FromFile($Path)
    try {
        if ($image.RawFormat.Guid -ne [System.Drawing.Imaging.ImageFormat]::Png.Guid) {
            throw "$Path is not a PNG image."
        }

        if ($image.Width -ne 256 -or $image.Height -ne 256) {
            throw "$Path must be exactly 256x256; found $($image.Width)x$($image.Height)."
        }

    }
    finally {
        $image.Dispose()
    }

    $stream = [System.IO.File]::OpenRead($Path)
    $reader = [System.IO.BinaryReader]::new($stream)
    try {
        $signature = $reader.ReadBytes(8)
        $expectedSignature = [byte[]](137, 80, 78, 71, 13, 10, 26, 10)
        if ([System.BitConverter]::ToString($signature) -ne [System.BitConverter]::ToString($expectedSignature)) {
            throw "$Path has an invalid PNG signature."
        }

        while ($stream.Position -lt $stream.Length) {
            $lengthBytes = $reader.ReadBytes(4)
            if ($lengthBytes.Length -ne 4) {
                throw "$Path has a truncated PNG chunk length."
            }
            [Array]::Reverse($lengthBytes)
            $length = [System.BitConverter]::ToUInt32($lengthBytes, 0)
            $chunkTypeBytes = $reader.ReadBytes(4)
            if ($chunkTypeBytes.Length -ne 4) {
                throw "$Path has a truncated PNG chunk type."
            }
            $chunkType = [System.Text.Encoding]::ASCII.GetString($chunkTypeBytes)
            if ($chunkType -notin @('IHDR', 'PLTE', 'IDAT', 'IEND')) {
                throw "$Path contains ancillary PNG metadata chunk $chunkType."
            }

            if ($length -gt [int]::MaxValue -or $stream.Position + [int64]$length + 4 -gt $stream.Length) {
                throw "$Path has an invalid PNG chunk length."
            }
            $stream.Seek([int64]$length + 4, [System.IO.SeekOrigin]::Current) | Out-Null
            if ($chunkType -eq 'IEND') {
                break
            }
        }
    }
    finally {
        $reader.Dispose()
        $stream.Dispose()
    }
}

function Assert-DllVersion {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$ExpectedVersion
    )

    $versionText = (Get-Item -LiteralPath $Path).VersionInfo.FileVersion
    $dllVersion = [System.Version]$versionText
    $manifestVersion = [System.Version]$ExpectedVersion
    if ($dllVersion.Major -ne $manifestVersion.Major -or
        $dllVersion.Minor -ne $manifestVersion.Minor -or
        $dllVersion.Build -ne $manifestVersion.Build) {
        throw "DLL version $versionText does not match manifest version $ExpectedVersion for $Path."
    }
}

if (-not $SkipBuild) {
    & (Join-Path $PSScriptRoot 'build-all.ps1') -GameDir $GameDir
    if ($LASTEXITCODE -ne 0) {
        throw 'One or more mod builds failed.'
    }
}

$expectedOutputRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot 'dist\thunderstore')).TrimEnd('\')
if (Test-Path -LiteralPath $outputRoot) {
    $actualOutputRoot = [System.IO.Path]::GetFullPath((Resolve-Path -LiteralPath $outputRoot).Path).TrimEnd('\')
    if (-not $actualOutputRoot.Equals($expectedOutputRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean unexpected output directory: $actualOutputRoot"
    }

    Remove-Item -LiteralPath $actualOutputRoot -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $stagingRoot | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem

$results = @()
foreach ($package in $packages) {
    $metadataDir = Join-Path $metadataRoot $package.Folder
    $manifestPath = Join-Path $metadataDir 'manifest.json'
    $readmePath = Join-Path $metadataDir 'README.md'
    $changelogPath = Join-Path $metadataDir 'CHANGELOG.md'
    $iconPath = Join-Path $metadataDir 'icon.png'

    foreach ($requiredFile in @($manifestPath, $readmePath, $changelogPath, $iconPath, $package.DllPath) + @($package.ExtraFiles)) {
        if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
            throw "Required package file not found: $requiredFile"
        }
    }

    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    Assert-Manifest -Manifest $manifest -Path $manifestPath -ExpectedDependency $package.Dependency
    Assert-Icon -Path $iconPath
    Assert-DllVersion -Path $package.DllPath -ExpectedVersion $manifest.version_number

    $stageDir = Join-Path $stagingRoot $package.Folder
    $installDir = Join-Path $stageDir $package.InstallPath
    New-Item -ItemType Directory -Force -Path $installDir | Out-Null

    Copy-Item -LiteralPath $manifestPath -Destination (Join-Path $stageDir 'manifest.json')
    Copy-Item -LiteralPath $readmePath -Destination (Join-Path $stageDir 'README.md')
    Copy-Item -LiteralPath $changelogPath -Destination (Join-Path $stageDir 'CHANGELOG.md')
    Copy-Item -LiteralPath $iconPath -Destination (Join-Path $stageDir 'icon.png')
    Copy-Item -LiteralPath $package.DllPath -Destination (Join-Path $installDir $package.DllName)
    foreach ($extraFile in @($package.ExtraFiles)) {
        Copy-Item -LiteralPath $extraFile -Destination (Join-Path $installDir ([System.IO.Path]::GetFileName($extraFile)))
    }

    $archivePath = Join-Path $outputRoot "$($manifest.name)-$($manifest.version_number).zip"
    Compress-Archive -Path (Join-Path $stageDir '*') -DestinationPath $archivePath -CompressionLevel Optimal

    $zip = [System.IO.Compression.ZipFile]::OpenRead($archivePath)
    try {
        $entries = @($zip.Entries | ForEach-Object { $_.FullName.Replace('\', '/') })
        foreach ($requiredRootEntry in @('manifest.json', 'README.md', 'CHANGELOG.md', 'icon.png')) {
            if ($entries -notcontains $requiredRootEntry) {
                throw "$archivePath is missing root entry $requiredRootEntry."
            }
        }

        $normalizedInstallPath = $package.InstallPath.Replace('\', '/')
        $expectedDllEntries = @("$normalizedInstallPath/$($package.DllName)") + @(
            $package.ExtraFiles | ForEach-Object { "$normalizedInstallPath/$([System.IO.Path]::GetFileName($_))" }
        )
        $dllEntries = @($entries | Where-Object { $_ -match '(?i)\.dll$' })
        if ($dllEntries.Count -ne $expectedDllEntries.Count -or @($expectedDllEntries | Where-Object { $dllEntries -notcontains $_ }).Count -gt 0) {
            throw "$archivePath has unexpected DLL entries. Expected: $($expectedDllEntries -join ', '); found: $($dllEntries -join ', ')"
        }

        $forbiddenEntries = @($entries | Where-Object {
            $_ -match '(?i)\.pdb$' -or
            $_ -match '(?i)(^|/)Assembly-CSharp\.dll$' -or
            $_ -match '(?i)(^|/)UnityEngine[^/]*\.dll$' -or
            $_ -match '(?i)(^|/)BepInEx[^/]*\.dll$'
        })
        if ($forbiddenEntries.Count -gt 0) {
            throw "$archivePath contains forbidden development or game files: $($forbiddenEntries -join ', ')"
        }
    }
    finally {
        $zip.Dispose()
    }

    $archive = Get-Item -LiteralPath $archivePath
    if ($archive.Length -gt 500MB) {
        throw "$archivePath exceeds Thunderstore's 500 MB package limit."
    }

    $hash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
    $results += [pscustomobject]@{
        Package = $manifest.name
        Version = $manifest.version_number
        Archive = $archive.FullName
        Bytes = $archive.Length
        SHA256 = $hash
    }
}

Remove-Item -LiteralPath $stagingRoot -Recurse -Force

$hashLines = $results | ForEach-Object { "$($_.SHA256)  $([System.IO.Path]::GetFileName($_.Archive))" }
[System.IO.File]::WriteAllLines((Join-Path $outputRoot 'SHA256SUMS.txt'), $hashLines, [System.Text.UTF8Encoding]::new($false))

$results | Format-Table Package, Version, Bytes, Archive -AutoSize
Write-Output "SHA-256 checksums: $(Join-Path $outputRoot 'SHA256SUMS.txt')"
