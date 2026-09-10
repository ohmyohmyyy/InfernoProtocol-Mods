param([int]$GameProcessId=0)
$ErrorActionPreference='Stop'
if($GameProcessId -gt 0) { Wait-Process -Id $GameProcessId -ErrorAction SilentlyContinue }
if(Get-Process -Name 'Inferno Protocol' -ErrorAction SilentlyContinue) { throw 'Close the game before installing Renovator.' }
$game='C:\Program Files (x86)\Steam\steamapps\common\Inferno Protocol'
if(-not(Test-Path -LiteralPath (Join-Path $game 'BepInEx\core\BepInEx.Unity.IL2CPP.dll'))) { throw 'BepInEx IL2CPP is missing.' }
$source=Join-Path $PSScriptRoot 'bin\Release\net6.0\InfernoProtocol.Renovator.dll'
$folder=Join-Path $game 'BepInEx\plugins\Renovator'
New-Item -ItemType Directory -Path $folder -Force | Out-Null
$target=Join-Path $folder 'InfernoProtocol.Renovator.dll'
if(Test-Path -LiteralPath $target) {
    $backup=Join-Path $PSScriptRoot 'backups'
    New-Item -ItemType Directory -Path $backup -Force | Out-Null
    Copy-Item -LiteralPath $target -Destination (Join-Path $backup ((Get-Date -Format 'yyyyMMdd-HHmmss')+'.dll'))
}
Copy-Item -LiteralPath $source -Destination $target -Force
if((Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath $target).Hash) { throw 'Installed hash mismatch.' }
foreach($legacy in @(
    @('BepInEx\plugins\Rennovator','InfernoProtocol.Rennovator.dll'),
    @('BepInEx\plugins\Wallpaper','InfernoProtocol.Wallpaper.dll')
)) {
    $legacyFolder=Join-Path $game $legacy[0]
    $legacyDll=Join-Path $legacyFolder $legacy[1]
    if(Test-Path -LiteralPath $legacyDll) { Remove-Item -LiteralPath $legacyDll -Force }
    if((Test-Path -LiteralPath $legacyFolder) -and -not (Get-ChildItem -LiteralPath $legacyFolder -Force | Select-Object -First 1)) { Remove-Item -LiteralPath $legacyFolder }
}
Write-Output 'Renovator installed and hash verified.'
