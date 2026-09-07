param([string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Inferno Protocol')
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath (Join-Path $GameDir 'BepInEx\interop\Assembly-CSharp.dll'))) {
    throw 'Run the game once with BepInEx 6 IL2CPP to generate its interop assemblies.'
}
& dotnet build (Join-Path $PSScriptRoot 'FieldQuests.csproj') -c Release -p:GameDir="$GameDir"
if ($LASTEXITCODE -ne 0) { throw 'FieldQuests build failed.' }
& dotnet run --project (Join-Path $PSScriptRoot '..\tests\FieldQuests.Tests\FieldQuests.Tests.csproj') -c Release -- "$GameDir"
if ($LASTEXITCODE -ne 0) { throw 'FieldQuests verification failed.' }
