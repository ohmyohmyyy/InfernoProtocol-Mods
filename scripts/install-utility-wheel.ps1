param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Inferno Protocol'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$source = Join-Path $repoRoot 'UtilityWheel\bin\Release\net6.0\InfernoProtocol.UtilityWheel.dll'
$sourcePdb = Join-Path $repoRoot 'UtilityWheel\bin\Release\net6.0\InfernoProtocol.UtilityWheel.pdb'
$coreSource = Join-Path $repoRoot 'UtilityWheel\bin\Release\net6.0\FrankMods.Core.dll'
$pluginDir = Join-Path $GameDir 'BepInEx\plugins\UtilityWheel'
$coreDir = Join-Path $GameDir 'BepInEx\plugins\FrankModsCore'

if (-not (Test-Path -LiteralPath $source)) {
    throw "Build output not found at $source. Run scripts\build-utility-wheel.ps1 first."
}

New-Item -ItemType Directory -Force -Path $pluginDir | Out-Null
if (-not (Test-Path -LiteralPath $coreSource)) { throw "FrankMods Core build output not found at $coreSource." }
New-Item -ItemType Directory -Force -Path $coreDir | Out-Null
Copy-Item -LiteralPath $coreSource -Destination (Join-Path $coreDir 'FrankMods.Core.dll') -Force
Copy-Item -LiteralPath $source -Destination (Join-Path $pluginDir 'InfernoProtocol.UtilityWheel.dll') -Force
if (Test-Path -LiteralPath $sourcePdb) {
    Copy-Item -LiteralPath $sourcePdb -Destination (Join-Path $pluginDir 'InfernoProtocol.UtilityWheel.pdb') -Force
}
Write-Output "Installed Utility Wheel to $pluginDir"
