param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Inferno Protocol'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$source = Join-Path $repoRoot 'BetterUI\bin\Release\net6.0\InfernoProtocol.BetterUI.dll'
$sourcePdb = Join-Path $repoRoot 'BetterUI\bin\Release\net6.0\InfernoProtocol.BetterUI.pdb'
$pluginDir = Join-Path $GameDir 'BepInEx\plugins\BetterUI'
$localDotnet = Join-Path $repoRoot '.tools\dotnet-sdk\dotnet.exe'
$dotnet = if (Test-Path -LiteralPath $localDotnet) {
    $localDotnet
}
else {
    (Get-Command dotnet -ErrorAction Stop).Source
}
$fixer = Join-Path $repoRoot 'tools\Unity6InteropFixer\bin\Release\net6.0\BetterUI.Unity6InteropFixer.dll'
$interopDir = Join-Path $GameDir 'BepInEx\interop'

if (-not (Test-Path -LiteralPath $source)) {
    throw "Build output not found at $source. Run scripts\build.ps1 first."
}

if (Test-Path -LiteralPath $fixer) {
    & $dotnet $fixer $interopDir
    if ($LASTEXITCODE -ne 0) {
        throw 'Unity 6 interop repair failed.'
    }
}

New-Item -ItemType Directory -Force -Path $pluginDir | Out-Null
Copy-Item -LiteralPath $source -Destination (Join-Path $pluginDir 'InfernoProtocol.BetterUI.dll') -Force
if (Test-Path -LiteralPath $sourcePdb) {
    Copy-Item -LiteralPath $sourcePdb -Destination (Join-Path $pluginDir 'InfernoProtocol.BetterUI.pdb') -Force
}
Write-Output "Installed BetterUI to $pluginDir"
