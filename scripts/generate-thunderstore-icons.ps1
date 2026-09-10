param([switch]$ContentPlusOnly,[switch]$RenovatorOnly)
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

$repoRoot = Split-Path -Parent $PSScriptRoot
$size = 256
$dark = [System.Drawing.Color]::FromArgb(255, 4, 14, 16)
$panel = [System.Drawing.Color]::FromArgb(255, 8, 31, 34)
$soft = [System.Drawing.Color]::FromArgb(255, 83, 128, 124)
$bright = [System.Drawing.Color]::FromArgb(255, 115, 255, 186)
$cyan = [System.Drawing.Color]::FromArgb(255, 78, 221, 255)

function New-Canvas {
    $bitmap = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.Clear($dark)

    $background = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
        [System.Drawing.Rectangle]::new(0, 0, $size, $size),
        $panel,
        $dark,
        55.0
    )
    $graphics.FillRectangle($background, 0, 0, $size, $size)
    $background.Dispose()

    $border = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(220, $bright), 4)
    $graphics.DrawRectangle($border, 8, 8, 239, 239)
    $border.Dispose()

    return [pscustomobject]@{ Bitmap = $bitmap; Graphics = $graphics }
}

function Save-Canvas {
    param(
        [Parameter(Mandatory = $true)]$Canvas,
        [Parameter(Mandatory = $true)][string]$Path
    )

    $Canvas.Graphics.Dispose()
    $Canvas.Bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    $Canvas.Bitmap.Dispose()
    Remove-PngMetadata -Path $Path
}

function Remove-PngMetadata {
    param([Parameter(Mandatory = $true)][string]$Path)

    $bytes = [System.IO.File]::ReadAllBytes($Path)
    $output = [System.IO.MemoryStream]::new()
    try {
        $output.Write($bytes, 0, 8)
        $position = 8
        while ($position -lt $bytes.Length) {
            $lengthBytes = [byte[]]$bytes[$position..($position + 3)]
            [Array]::Reverse($lengthBytes)
            $length = [System.BitConverter]::ToUInt32($lengthBytes, 0)
            $chunkType = [System.Text.Encoding]::ASCII.GetString($bytes, $position + 4, 4)
            $chunkLength = 12 + [int]$length

            if ($chunkType -in @('IHDR', 'PLTE', 'IDAT', 'IEND')) {
                $output.Write($bytes, $position, $chunkLength)
            }

            $position += $chunkLength
            if ($chunkType -eq 'IEND') {
                break
            }
        }

        [System.IO.File]::WriteAllBytes($Path, $output.ToArray())
    }
    finally {
        $output.Dispose()
    }
}

function New-BetterUiIcon {
    param([string]$Path)

    $canvas = New-Canvas
    $g = $canvas.Graphics
    $outline = [System.Drawing.Pen]::new($bright, 7)
    $line = [System.Drawing.Pen]::new($soft, 6)
    $selected = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(210, 31, 105, 73))

    $g.DrawRectangle($outline, 39, 48, 178, 151)
    $g.DrawLine($outline, 39, 82, 217, 82)
    $g.DrawLine($line, 91, 83, 91, 199)
    $g.FillRectangle($selected, 101, 96, 100, 29)
    $g.DrawRectangle($outline, 101, 96, 100, 29)
    $g.DrawLine($line, 106, 143, 192, 143)
    $g.DrawLine($line, 106, 165, 176, 165)
    $g.DrawLine($line, 106, 187, 196, 187)
    $g.DrawLine($outline, 51, 105, 78, 105)
    $g.DrawLine($line, 51, 137, 78, 137)
    $g.DrawLine($line, 51, 169, 78, 169)

    $outline.Dispose()
    $line.Dispose()
    $selected.Dispose()
    Save-Canvas -Canvas $canvas -Path $Path
}

function New-UtilityWheelIcon {
    param([string]$Path)

    $canvas = New-Canvas
    $g = $canvas.Graphics
    $ring = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(210, $soft), 32)
    $highlight = [System.Drawing.Pen]::new($cyan, 34)
    $spoke = [System.Drawing.Pen]::new($dark, 7)
    $centerBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 13, 52, 55))
    $centerPen = [System.Drawing.Pen]::new($bright, 5)

    $g.DrawEllipse($ring, 53, 53, 150, 150)
    $g.DrawArc($highlight, 53, 53, 150, 150, -87, 39)
    for ($i = 0; $i -lt 8; $i++) {
        $angle = ($i * 45.0) * [Math]::PI / 180.0
        $x1 = 128 + [Math]::Cos($angle) * 52
        $y1 = 128 + [Math]::Sin($angle) * 52
        $x2 = 128 + [Math]::Cos($angle) * 91
        $y2 = 128 + [Math]::Sin($angle) * 91
        $g.DrawLine($spoke, [single]$x1, [single]$y1, [single]$x2, [single]$y2)
    }
    $g.FillEllipse($centerBrush, 99, 99, 58, 58)
    $g.DrawEllipse($centerPen, 99, 99, 58, 58)
    $pointer = [System.Drawing.Pen]::new($bright, 8)
    $pointer.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pointer.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $g.DrawLine($pointer, 128, 128, 168, 88)

    $ring.Dispose()
    $highlight.Dispose()
    $spoke.Dispose()
    $centerBrush.Dispose()
    $centerPen.Dispose()
    $pointer.Dispose()
    Save-Canvas -Canvas $canvas -Path $Path
}

function New-BetterLightsIcon {
    param([string]$Path)

    $canvas = New-Canvas
    $g = $canvas.Graphics

    $glowColors = @(
        [System.Drawing.Color]::FromArgb(25, 115, 255, 186),
        [System.Drawing.Color]::FromArgb(36, 78, 221, 255),
        [System.Drawing.Color]::FromArgb(46, 190, 103, 255),
        [System.Drawing.Color]::FromArgb(70, 255, 192, 85)
    )
    $diameters = @(154, 124, 96, 70)
    for ($i = 0; $i -lt $diameters.Count; $i++) {
        $diameter = $diameters[$i]
        $brush = [System.Drawing.SolidBrush]::new($glowColors[$i])
        $offset = [int](($size - $diameter) / 2)
        $g.FillEllipse($brush, $offset, 43 + [int](($diameters[0] - $diameter) / 2), $diameter, $diameter)
        $brush.Dispose()
    }

    $bulb = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 255, 230, 122))
    $bulbOutline = [System.Drawing.Pen]::new($bright, 5)
    $baseBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 55, 93, 91))
    $rayColors = @(
        [System.Drawing.Color]::FromArgb(255, 255, 96, 130),
        [System.Drawing.Color]::FromArgb(255, 255, 192, 85),
        [System.Drawing.Color]::FromArgb(255, 115, 255, 186),
        [System.Drawing.Color]::FromArgb(255, 78, 221, 255),
        [System.Drawing.Color]::FromArgb(255, 190, 103, 255)
    )

    for ($i = 0; $i -lt 10; $i++) {
        $angle = ($i * 36.0 - 90.0) * [Math]::PI / 180.0
        $x1 = 128 + [Math]::Cos($angle) * 62
        $y1 = 105 + [Math]::Sin($angle) * 62
        $x2 = 128 + [Math]::Cos($angle) * 86
        $y2 = 105 + [Math]::Sin($angle) * 86
        $ray = [System.Drawing.Pen]::new($rayColors[$i % $rayColors.Count], 7)
        $ray.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $ray.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        $g.DrawLine($ray, [single]$x1, [single]$y1, [single]$x2, [single]$y2)
        $ray.Dispose()
    }

    $g.FillEllipse($bulb, 84, 61, 88, 88)
    $g.DrawEllipse($bulbOutline, 84, 61, 88, 88)
    $g.FillRectangle($baseBrush, 101, 138, 54, 42)
    $g.DrawLine($bulbOutline, 101, 150, 155, 150)
    $g.DrawLine($bulbOutline, 103, 164, 153, 164)
    $g.DrawArc($bulbOutline, 108, 165, 40, 32, 0, 180)

    $bulb.Dispose()
    $bulbOutline.Dispose()
    $baseBrush.Dispose()
    Save-Canvas -Canvas $canvas -Path $Path
}

function New-RenovatorIcon {
    param([string]$Path)

    $canvas = New-Canvas
    $g = $canvas.Graphics
    $outline = [System.Drawing.Pen]::new($bright, 7)
    $line = [System.Drawing.Pen]::new($soft, 5)
    $paper = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 18, 57, 57))
    $accent = [System.Drawing.Pen]::new($cyan, 7)
    $house = [System.Drawing.Point[]]@(
        [System.Drawing.Point]::new(39,112),
        [System.Drawing.Point]::new(128,42),
        [System.Drawing.Point]::new(217,112),
        [System.Drawing.Point]::new(197,112),
        [System.Drawing.Point]::new(197,211),
        [System.Drawing.Point]::new(59,211),
        [System.Drawing.Point]::new(59,112)
    )
    $g.FillPolygon($paper,$house)
    $g.DrawLines($outline,$house)
    $g.DrawLine($outline,39,112,128,42)
    $g.DrawLine($outline,128,42,217,112)
    $g.DrawLine($outline,59,211,197,211)

    # Wallpapered window/panel gives the house a clear surface-finishing cue.
    $g.DrawRectangle($outline,75,116,58,54)
    $g.DrawLine($line,94,117,94,169)
    $g.DrawLine($line,114,117,114,169)
    $g.DrawLine($accent,79,140,94,125)
    $g.DrawLine($accent,99,160,114,145)
    $g.DrawLine($accent,118,137,129,126)

    # Door plus a compact four-way move glyph communicates object placement.
    $g.DrawRectangle($outline,151,135,27,76)
    $g.DrawEllipse($accent,168,174,3,3)
    $g.DrawLine($accent,103,190,139,190)
    $g.DrawLine($accent,121,172,121,208)
    $g.DrawLine($accent,103,190,112,181)
    $g.DrawLine($accent,103,190,112,199)
    $g.DrawLine($accent,139,190,130,181)
    $g.DrawLine($accent,139,190,130,199)
    $g.DrawLine($accent,121,172,112,181)
    $g.DrawLine($accent,121,172,130,181)
    $g.DrawLine($accent,121,208,112,199)
    $g.DrawLine($accent,121,208,130,199)
    $outline.Dispose(); $line.Dispose(); $paper.Dispose(); $accent.Dispose()
    Save-Canvas -Canvas $canvas -Path $Path
}

if ($ContentPlusOnly) {
    $canvas = New-Canvas
    $g = $canvas.Graphics
    $outline = [System.Drawing.Pen]::new($bright, 6)
    $line = [System.Drawing.Pen]::new($soft, 5)
    $paper = [System.Drawing.SolidBrush]::new($panel)
    $badge = [System.Drawing.SolidBrush]::new($dark)
    $plus = [System.Drawing.Pen]::new($cyan, 9)
    $g.DrawRectangle($outline, 42, 43, 162, 144)
    $g.DrawLine($outline, 59, 190, 59, 215)
    $g.DrawLine($outline, 185, 190, 185, 215)
    $g.DrawLine($outline, 43, 75, 203, 75)
    $g.FillRectangle($paper, 61, 94, 68, 72)
    $g.DrawRectangle($line, 61, 94, 68, 72)
    $g.DrawLine($outline, 72, 110, 115, 110)
    $g.DrawLine($line, 72, 129, 115, 129)
    $g.DrawLine($line, 72, 148, 101, 148)
    $g.DrawLine($line, 148, 103, 185, 103)
    $g.DrawLine($line, 148, 124, 180, 124)
    $g.FillEllipse($badge, 146, 145, 74, 74)
    $g.DrawEllipse($outline, 146, 145, 74, 74)
    $g.DrawLine($plus, 164, 182, 202, 182)
    $g.DrawLine($plus, 183, 163, 183, 201)
    $outline.Dispose(); $line.Dispose(); $paper.Dispose(); $badge.Dispose(); $plus.Dispose()
    Save-Canvas -Canvas $canvas -Path (Join-Path $repoRoot 'thunderstore\ContentPlus\icon.png')
    return
}
if ($RenovatorOnly) {
    $directory = Join-Path $repoRoot 'thunderstore\Renovator'
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    New-RenovatorIcon -Path (Join-Path $directory 'icon.png')
    return
}
New-BetterUiIcon -Path (Join-Path $repoRoot 'thunderstore\BetterUI\icon.png')
New-UtilityWheelIcon -Path (Join-Path $repoRoot 'thunderstore\UtilityWheel\icon.png')
New-BetterLightsIcon -Path (Join-Path $repoRoot 'thunderstore\BetterLights\icon.png')

Get-Item -LiteralPath `
    (Join-Path $repoRoot 'thunderstore\BetterUI\icon.png'), `
    (Join-Path $repoRoot 'thunderstore\UtilityWheel\icon.png'), `
    (Join-Path $repoRoot 'thunderstore\BetterLights\icon.png') |
    Select-Object FullName, Length
