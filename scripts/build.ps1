param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Inferno Protocol'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$localDotnet = Join-Path $repoRoot '.tools\dotnet-sdk\dotnet.exe'
$dotnet = if (Test-Path -LiteralPath $localDotnet) {
    $localDotnet
}
else {
    (Get-Command dotnet -ErrorAction Stop).Source
}

if (-not (Test-Path -LiteralPath (Join-Path $GameDir 'BepInEx\interop\Assembly-CSharp.dll'))) {
    throw 'BepInEx interop assemblies are missing. Run Inferno Protocol once with BepInEx installed.'
}

& $dotnet build (Join-Path $repoRoot 'tools\Unity6InteropFixer\Unity6InteropFixer.csproj') -c Release
& $dotnet build (Join-Path $repoRoot 'BetterUI\BetterUI.csproj') -c Release -p:GameDir="$GameDir"
