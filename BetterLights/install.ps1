param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Inferno Protocol'
)

$ErrorActionPreference = 'Stop'
$source = Join-Path $PSScriptRoot 'bin\Release\net6.0\InfernoProtocol.BetterLights.dll'
$sourcePdb = Join-Path $PSScriptRoot 'bin\Release\net6.0\InfernoProtocol.BetterLights.pdb'
$coreSource = Join-Path $PSScriptRoot 'bin\Release\net6.0\FrankMods.Core.dll'
$pluginDir = Join-Path $GameDir 'BepInEx\plugins\BetterLights'
$coreDir = Join-Path $GameDir 'BepInEx\plugins\FrankModsCore'

if (-not (Test-Path -LiteralPath $source)) {
    throw "Build output not found at $source. Run BetterLights\build.ps1 first."
}

New-Item -ItemType Directory -Force -Path $pluginDir | Out-Null
Copy-Item -LiteralPath $source -Destination (Join-Path $pluginDir 'InfernoProtocol.BetterLights.dll') -Force
if (-not (Test-Path -LiteralPath $coreSource)) {
    throw "FrankMods Core build output not found at $coreSource."
}
New-Item -ItemType Directory -Force -Path $coreDir | Out-Null
Copy-Item -LiteralPath $coreSource -Destination (Join-Path $coreDir 'FrankMods.Core.dll') -Force
if (Test-Path -LiteralPath $sourcePdb) {
    Copy-Item -LiteralPath $sourcePdb -Destination (Join-Path $pluginDir 'InfernoProtocol.BetterLights.pdb') -Force
}

Write-Output "Installed BetterLights to $pluginDir"
