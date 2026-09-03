param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Inferno Protocol',
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$dist = Join-Path $repoRoot 'dist'
$staging = Join-Path $dist 'staging'

if (-not $SkipBuild) {
    & (Join-Path $PSScriptRoot 'build-all.ps1') -GameDir $GameDir
}

$betterUiDll = Join-Path $repoRoot 'BetterUI\bin\Release\net6.0\InfernoProtocol.BetterUI.dll'
$utilityWheelDll = Join-Path $repoRoot 'UtilityWheel\bin\Release\net6.0\InfernoProtocol.UtilityWheel.dll'
$betterLightsDll = Join-Path $repoRoot 'BetterLights\bin\Release\net6.0\InfernoProtocol.BetterLights.dll'
foreach ($dll in @($betterUiDll, $utilityWheelDll, $betterLightsDll)) {
    if (-not (Test-Path -LiteralPath $dll)) {
        throw "Release DLL not found: $dll"
    }
}

if (Test-Path -LiteralPath $dist) {
    $resolvedDist = (Resolve-Path -LiteralPath $dist).Path
    $expectedDist = Join-Path (Resolve-Path -LiteralPath $repoRoot).Path 'dist'
    if (-not $resolvedDist.Equals($expectedDist, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Unexpected distribution directory: $resolvedDist"
    }
    Remove-Item -LiteralPath $resolvedDist -Recurse -Force
}

$betterUiStage = Join-Path $staging 'BetterUI'
$utilityWheelStage = Join-Path $staging 'UtilityWheel'
$betterLightsStage = Join-Path $staging 'BetterLights'
New-Item -ItemType Directory -Force -Path (Join-Path $betterUiStage 'BepInEx\plugins\BetterUI') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $utilityWheelStage 'BepInEx\plugins\UtilityWheel') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $betterLightsStage 'BepInEx\plugins\BetterLights') | Out-Null

Copy-Item -LiteralPath $betterUiDll -Destination (Join-Path $betterUiStage 'BepInEx\plugins\BetterUI\InfernoProtocol.BetterUI.dll')
Copy-Item -LiteralPath (Join-Path $repoRoot 'release\INSTALL-BetterUI.txt') -Destination (Join-Path $betterUiStage 'INSTALL.txt')
Copy-Item -LiteralPath $utilityWheelDll -Destination (Join-Path $utilityWheelStage 'BepInEx\plugins\UtilityWheel\InfernoProtocol.UtilityWheel.dll')
Copy-Item -LiteralPath (Join-Path $repoRoot 'release\INSTALL-UtilityWheel.txt') -Destination (Join-Path $utilityWheelStage 'INSTALL.txt')
Copy-Item -LiteralPath $betterLightsDll -Destination (Join-Path $betterLightsStage 'BepInEx\plugins\BetterLights\InfernoProtocol.BetterLights.dll')
Copy-Item -LiteralPath (Join-Path $repoRoot 'release\INSTALL-BetterLights.txt') -Destination (Join-Path $betterLightsStage 'INSTALL.txt')

Compress-Archive -Path (Join-Path $betterUiStage '*') -DestinationPath (Join-Path $dist 'BetterUI-v0.9.4.zip')
Compress-Archive -Path (Join-Path $utilityWheelStage '*') -DestinationPath (Join-Path $dist 'UtilityWheel-v0.2.0.zip')
Compress-Archive -Path (Join-Path $betterLightsStage '*') -DestinationPath (Join-Path $dist 'BetterLights-v0.3.1.zip')
Copy-Item -LiteralPath $betterUiDll -Destination (Join-Path $dist 'InfernoProtocol.BetterUI-v0.9.4.dll')
Copy-Item -LiteralPath $utilityWheelDll -Destination (Join-Path $dist 'InfernoProtocol.UtilityWheel-v0.2.0.dll')
Copy-Item -LiteralPath $betterLightsDll -Destination (Join-Path $dist 'InfernoProtocol.BetterLights-v0.3.1.dll')

Remove-Item -LiteralPath $staging -Recurse -Force
Get-ChildItem -LiteralPath $dist | Select-Object Name, Length
