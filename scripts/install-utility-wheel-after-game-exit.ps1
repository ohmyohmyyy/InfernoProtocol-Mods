param(
    [Parameter(Mandatory = $true)]
    [int]$GameProcessId,
    [Parameter(Mandatory = $true)]
    [string]$SourceDll,
    [Parameter(Mandatory = $true)]
    [string]$SourcePdb,
    [Parameter(Mandatory = $true)]
    [string]$PluginDirectory
)

$ErrorActionPreference = 'Stop'
try {
    Wait-Process -Id $GameProcessId -ErrorAction Stop
}
catch [Microsoft.PowerShell.Commands.ProcessCommandException] {
    # The game closed before the watcher began waiting.
}

$expectedDirectory = 'C:\Program Files (x86)\Steam\steamapps\common\Inferno Protocol\BepInEx\plugins\UtilityWheel'
$parent = Split-Path -Parent $PluginDirectory
$expectedParent = Split-Path -Parent $expectedDirectory
if (-not (Test-Path -LiteralPath $parent)) {
    throw "UtilityWheel plugin parent directory does not exist: $parent"
}

$resolvedParent = (Resolve-Path -LiteralPath $parent).Path
if (-not $resolvedParent.Equals($expectedParent, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Unexpected UtilityWheel plugin parent directory: $resolvedParent"
}

New-Item -ItemType Directory -Force -Path $PluginDirectory | Out-Null
Copy-Item -LiteralPath $SourceDll -Destination (Join-Path $PluginDirectory 'InfernoProtocol.UtilityWheel.dll') -Force
Copy-Item -LiteralPath $SourcePdb -Destination (Join-Path $PluginDirectory 'InfernoProtocol.UtilityWheel.pdb') -Force
