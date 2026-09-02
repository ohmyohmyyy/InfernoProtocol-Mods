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

$resolvedPluginDirectory = (Resolve-Path -LiteralPath $PluginDirectory).Path
$expectedDirectory = 'C:\Program Files (x86)\Steam\steamapps\common\Inferno Protocol\BepInEx\plugins\BetterUI'
if (-not $resolvedPluginDirectory.Equals($expectedDirectory, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Unexpected BetterUI plugin directory: $resolvedPluginDirectory"
}

Copy-Item -LiteralPath $SourceDll -Destination (Join-Path $resolvedPluginDirectory 'InfernoProtocol.BetterUI.dll') -Force
Copy-Item -LiteralPath $SourcePdb -Destination (Join-Path $resolvedPluginDirectory 'InfernoProtocol.BetterUI.pdb') -Force
