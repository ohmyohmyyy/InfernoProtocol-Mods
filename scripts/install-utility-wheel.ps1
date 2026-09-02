param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Inferno Protocol'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$source = Join-Path $repoRoot 'UtilityWheel\bin\Release\net6.0\InfernoProtocol.UtilityWheel.dll'
$sourcePdb = Join-Path $repoRoot 'UtilityWheel\bin\Release\net6.0\InfernoProtocol.UtilityWheel.pdb'
$pluginDir = Join-Path $GameDir 'BepInEx\plugins\UtilityWheel'

if (-not (Test-Path -LiteralPath $source)) {
    throw "Build output not found at $source. Run scripts\build-utility-wheel.ps1 first."
}

New-Item -ItemType Directory -Force -Path $pluginDir | Out-Null
Copy-Item -LiteralPath $source -Destination (Join-Path $pluginDir 'InfernoProtocol.UtilityWheel.dll') -Force
if (Test-Path -LiteralPath $sourcePdb) {
    Copy-Item -LiteralPath $sourcePdb -Destination (Join-Path $pluginDir 'InfernoProtocol.UtilityWheel.pdb') -Force
}
Write-Output "Installed Utility Wheel to $pluginDir"
