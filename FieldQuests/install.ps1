param([string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Inferno Protocol')
$ErrorActionPreference = 'Stop'
if (Get-Process -Name 'Inferno Protocol' -ErrorAction SilentlyContinue) {
    throw 'Close Inferno Protocol before installing FieldQuests.'
}
$source = Join-Path $PSScriptRoot 'bin\Release\net6.0\InfernoProtocol.FieldQuests.dll'
if (-not (Test-Path -LiteralPath $source)) { throw 'Run FieldQuests\build.ps1 first.' }
$pluginDir = Join-Path $GameDir 'BepInEx\plugins\FieldQuests'
if (-not (Test-Path -LiteralPath (Join-Path $GameDir 'BepInEx\core\BepInEx.Unity.IL2CPP.dll'))) { throw 'BepInEx 6 IL2CPP is missing.' }
New-Item -ItemType Directory -Force -Path $pluginDir | Out-Null
$target = Join-Path $pluginDir 'InfernoProtocol.FieldQuests.dll'
if (Test-Path -LiteralPath $target) {
    $backupDir = Join-Path $PSScriptRoot 'backups'
    New-Item -ItemType Directory -Force -Path $backupDir | Out-Null
    Copy-Item -LiteralPath $target -Destination (Join-Path $backupDir ('FieldQuests-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.dll'))
}
Copy-Item -LiteralPath $source -Destination $target -Force
if ((Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath $target).Hash) { throw 'Installed DLL verification failed.' }
Write-Output "Installed and verified ContentPlus 0.5.0 at $target"
