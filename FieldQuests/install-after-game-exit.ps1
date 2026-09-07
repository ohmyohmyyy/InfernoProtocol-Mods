param(
    [Parameter(Mandatory = $true)]
    [int]$GameProcessId,
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Inferno Protocol'
)
$ErrorActionPreference = 'Stop'
$logPath = Join-Path $PSScriptRoot 'install-after-game-exit.log'
try {
    "Waiting for game process $GameProcessId to close normally." | Out-File -LiteralPath $logPath
    Wait-Process -Id $GameProcessId -ErrorAction SilentlyContinue
    & (Join-Path $PSScriptRoot 'install.ps1') -GameDir $GameDir | Out-File -LiteralPath $logPath -Append
}
catch {
    "Install failed: $_" | Out-File -LiteralPath $logPath -Append
    exit 1
}
