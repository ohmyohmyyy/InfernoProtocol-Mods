param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Inferno Protocol'
)

$ErrorActionPreference = 'Stop'

& (Join-Path $PSScriptRoot 'build.ps1') -GameDir $GameDir
if ($LASTEXITCODE -ne 0) {
    throw 'BetterUI build failed.'
}

& (Join-Path $PSScriptRoot 'build-utility-wheel.ps1') -GameDir $GameDir
if ($LASTEXITCODE -ne 0) {
    throw 'Utility Wheel build failed.'
}

& (Join-Path (Split-Path -Parent $PSScriptRoot) 'BetterLights\build.ps1') -GameDir $GameDir
if ($LASTEXITCODE -ne 0) {
    throw 'BetterLights build failed.'
}
