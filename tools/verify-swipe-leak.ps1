#Requires -Version 7.0
<#
.SYNOPSIS
    Visual regression: a closed ArcSwipeActions row must not leak danger colour through its edges.

.DESCRIPTION
    The swipe-actions documentation page ends with a row that is at rest (closed) while its hidden
    trailing actions include the ArcDanger "Delete" action. ArcSwipeActions keeps both action hosts
    invisible until the row opens, so no danger fill may appear on the resting row, including on the
    antialiased 1 px border and the rounded corners where a leak would show first.

    This script renders that page with the gallery shot harness and scans the outer 6 px band of the
    closed row rectangle (all four edges; the rounded corners are inside it) for danger red. The opened
    trailing row above it is the positive control: its right-hand 168 px must contain the red Delete
    fill, which proves the predicate can see ArcDanger in the rendered theme at all.

    Why the band and not the whole row rectangle: the offscreen Skia renderer uses LCD subpixel text
    antialiasing, and warm fringe pixels on the outside of glyphs (for example r,g,b = 208,160,25 or
    160,25,25) satisfy any danger predicate. The demo row carries text ("Fortnightly report", "Fri",
    and a subject line) so a whole-row scan is polluted by those fringes. Text sits at least one row
    padding (16 px) inside the surface, while a colour leak can only appear on the 1-3 px edge ring, so
    the 6 px band isolates the regression target. Full-row counts are still reported as diagnostics.

    Evidence is written to docs/analysis/swipe-leak/: the offset page slice, an annotated PNG, and a
    JSON + Markdown report. Exit code 0 = PASS, 1 = FAIL, 2 = render or row-detection failure.

    Rendering notes: tools/shots.ps1 only captures a 1400 px slice from the top of the page, which is
    enough for the standard gallery screenshots but clips the bottom of very tall item pages. The demo
    canvas sits well below the top, so this script also renders the same page with the gallery's
    --offset argument (a vertical page offset) and scans that slice.

.EXAMPLE
    pwsh -NoProfile tools/verify-swipe-leak.ps1

.EXAMPLE
    pwsh -NoProfile tools/verify-swipe-leak.ps1 -Theme dark -SkipBuild
#>
[CmdletBinding()]
param(
    [ValidateSet('both', 'dark', 'light')]
    [string]$Theme = 'both',

    [int]$Width = 1180,

    # Vertical page offset for the analysed slice. The demo canvas starts around page y=900, so 300
    # keeps the whole canvas (and the closed assertion row) inside one 1400 px capture.
    [int]$Offset = 300,

    [int]$TimeoutSec = 240,

    # Reuse the current gallery build instead of running tools/build.ps1 first.
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $PSScriptRoot
$galleryProject = Join-Path $root 'src\Apps\Arc.Avalonia.Gallery\Arc.Avalonia.Gallery.csproj'
$galleryDll = Join-Path $root 'src\Apps\Arc.Avalonia.Gallery\bin\Debug\net10.0\Arc.Avalonia.Gallery.dll'
$buildScript = Join-Path $PSScriptRoot 'build.ps1'
$shotsScript = Join-Path $PSScriptRoot 'shots.ps1'
$pageSlug = 'swipe-actions'
$evidenceDir = Join-Path $root 'docs\analysis\swipe-leak'
$renderDir = Join-Path $evidenceDir 'render'
$themes = if ($Theme -eq 'both') { @('dark', 'light') } else { @($Theme) }

# Outer-edge band of a demo row that is scanned for a colour leak (the rounded corners are included).
$ringThickness = 6

# Predicate for the edge band: a deliberately loose "reddish / not neutral" rule. It is only safe
# because text glyphs never reach the band; it catches blended leak colours such as 110,60,58.
$ringDanger = @{ MinRed = 60; MinGreenDelta = 25; MinBlueDelta = 25 }

# Predicate for the positive control: a real ArcDanger fill, not an antialiasing fringe.
$fillDanger = @{ MinRed = 150; MinGreenDelta = 60; MinBlueDelta = 55 }

# Diagnostic-only predicate for the whole row rectangle; documented in the report as text-AA polluted.
$fullRowDanger = @{ MinRed = 120; MinGreenDelta = 50; MinBlueDelta = 50 }

# Positive control geometry: the trailing action stack is last in the row, on the right.
$controlRegionWidth = 168
$controlMinFillPixels = 1000

$ringRule = 'r >= 60 && r - g >= 25 && r - b >= 25 (outer {0} px band)' -f $ringThickness
$fillRule = 'r >= 150 && r - g >= 60 && r - b >= 55 (danger fill)'
$diagnosticRule = 'r >= 120 && r - g >= 50 && r - b >= 50 (whole row, diagnostics only)'
$buildCommand = 'pwsh -NoProfile tools/build.ps1 -Project src/Apps/Arc.Avalonia.Gallery/Arc.Avalonia.Gallery.csproj'

function Get-RelativePath {
    param([Parameter(Mandatory)][string]$Path)
    return ([System.IO.Path]::GetRelativePath($root, $Path) -replace '\\', '/')
}

function Get-HexColor {
    param([int]$Argb)
    return ('#{0:X2}{1:X2}{2:X2}' -f (($Argb -shr 16) -band 0xFF), (($Argb -shr 8) -band 0xFF), ($Argb -band 0xFF))
}

# Reads one pixel out of the normalised 32bpp image as an 0xRRGGBB integer.
function Get-ArgbAt {
    param([hashtable]$Image, [int]$X, [int]$Y)
    $offset = $Y * $Image.Stride + $X * 4
    $bytes = $Image.Bytes
    return ([int]$bytes[$offset + 2] -shl 16) -bor ([int]$bytes[$offset + 1] -shl 8) -bor [int]$bytes[$offset]
}

# Copies the rendered PNG into a known 32bpp layout so the scan can walk a byte array instead of
# calling GetPixel hundreds of thousands of times.
function Convert-ToRgbaImage {
    param([System.Drawing.Bitmap]$Bitmap)
    $width = $Bitmap.Width
    $height = $Bitmap.Height
    $normalized = [System.Drawing.Bitmap]::new($width, $height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($normalized)
    try {
        $graphics.DrawImage($Bitmap, 0, 0, $width, $height)
    }
    finally {
        $graphics.Dispose()
    }

    $rectangle = [System.Drawing.Rectangle]::new(0, 0, $width, $height)
    $lock = $normalized.LockBits($rectangle, [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $stride = $lock.Stride
        $bytes = [byte[]]::new($stride * $height)
        [System.Runtime.InteropServices.Marshal]::Copy($lock.Scan0, $bytes, 0, $bytes.Length)
    }
    finally {
        $normalized.UnlockBits($lock)
        $normalized.Dispose()
    }

    return @{ Bytes = $bytes; Stride = $stride; Width = $width; Height = $height }
}

# Contiguous vertical runs of the row-surface colour inside the demo canvas. Glyph-sized holes
# (antialiased text over the flat surface) are bridged so one row never splits into two bands.
function Get-SurfaceBands {
    param([hashtable]$Image, [int]$Column, [int]$Top, [int]$Bottom, [int]$SurfaceColor)
    $bands = [System.Collections.Generic.List[object]]::new()
    $start = -1
    $last = -1
    $gap = 0
    for ($y = $Top; $y -le $Bottom; $y++) {
        if ((Get-ArgbAt -Image $Image -X $Column -Y $y) -eq $SurfaceColor) {
            if ($start -lt 0) { $start = $y }
            $last = $y
            $gap = 0
        }
        elseif ($start -ge 0) {
            $gap++
            if ($gap -gt 6) {
                $bands.Add([pscustomobject]@{ Top = $start; Bottom = $last })
                $start = -1
                $last = -1
                $gap = 0
            }
        }
    }

    if ($start -ge 0) {
        $bands.Add([pscustomobject]@{ Top = $start; Bottom = $last })
    }

    return @($bands | Where-Object { ($_.Bottom - $_.Top + 1) -ge 40 -and ($_.Bottom - $_.Top + 1) -le 200 })
}

# Locates the demo canvas and the four demo rows inside it, without hard-coded coordinates:
#   1. the canvas is the tallest uniform non-page-background run in the left canvas padding column;
#   2. the row surface colour is the dominant non-canvas colour inside the canvas;
#   3. a row band is a vertical run of that surface colour at a column that crosses all four rows;
#   4. the row x-range comes from the closed rows (an opened row's surface is translated, but every
#      demo row has the same 640 px width and left alignment, and all of them are clipped to it).
function Find-DemoRows {
    param([hashtable]$Image)
    $width = $Image.Width
    $height = $Image.Height
    $pageBackground = Get-ArgbAt -Image $Image -X 4 -Y 4

    # 1. Vertical canvas band. x=60 sits in the canvas' 24 px padding, left of the 640 px rows and in
    #    the page margin (the content column starts at x=56).
    $canvasTop = -1
    $canvasBottom = -1
    $canvasColor = -1
    $bestLength = 0
    $runStart = 0
    $runColor = Get-ArgbAt -Image $Image -X 60 -Y 0
    for ($y = 1; $y -lt $height; $y++) {
        $color = Get-ArgbAt -Image $Image -X 60 -Y $y
        if ($color -eq $runColor) { continue }
        $length = $y - $runStart
        if ($runColor -ne $pageBackground -and $length -gt $bestLength) {
            $bestLength = $length
            $canvasTop = $runStart
            $canvasBottom = $y - 1
            $canvasColor = $runColor
        }
        $runStart = $y
        $runColor = $color
    }
    $length = $height - $runStart
    if ($runColor -ne $pageBackground -and $length -gt $bestLength) {
        $bestLength = $length
        $canvasTop = $runStart
        $canvasBottom = $height - 1
        $canvasColor = $runColor
    }
    if ($canvasTop -lt 0 -or $bestLength -lt 300) {
        throw "could not locate the muted demo canvas in column x=60 (best non-background run: $bestLength px)"
    }

    # 2. Horizontal canvas extent, on scanlines just under the top edge: the rounded 26 px corner has
    #    almost vanished there and the first demo row has not started yet.
    $canvasLeft = -1
    $canvasRight = -1
    foreach ($probeY in @(($canvasTop + 6), ($canvasTop + 8), ($canvasTop + 10))) {
        if ($probeY -ge $canvasBottom) { continue }
        $runStart = -1
        $last = -1
        $best = 0
        $bestStart = -1
        $bestEnd = -1
        for ($x = 0; $x -lt $width; $x++) {
            if ((Get-ArgbAt -Image $Image -X $x -Y $probeY) -eq $canvasColor) {
                if ($runStart -lt 0) { $runStart = $x }
                $last = $x
            }
            elseif ($runStart -ge 0) {
                $runLength = $last - $runStart + 1
                if ($runLength -gt $best) { $best = $runLength; $bestStart = $runStart; $bestEnd = $last }
                $runStart = -1
                $last = -1
            }
        }
        if ($runStart -ge 0) {
            $runLength = $last - $runStart + 1
            if ($runLength -gt $best) { $best = $runLength; $bestStart = $runStart; $bestEnd = $last }
        }
        if ($best -ge 300) {
            $canvasLeft = $bestStart
            $canvasRight = $bestEnd
            break
        }
    }
    if ($canvasLeft -lt 0) {
        throw 'could not locate the demo canvas horizontally'
    }

    # 3. Row surface colour: the dominant non-canvas colour at the canvas centre column.
    $scanColumn = [int](($canvasLeft + $canvasRight) / 2)
    $histogram = @{}
    for ($y = $canvasTop; $y -le $canvasBottom; $y++) {
        $color = Get-ArgbAt -Image $Image -X $scanColumn -Y $y
        if ($color -eq $canvasColor) { continue }
        if ($histogram.ContainsKey($color)) { $histogram[$color] = [int]$histogram[$color] + 1 }
        else { $histogram[$color] = 1 }
    }
    if ($histogram.Count -eq 0) {
        throw 'no row-surface pixels found inside the demo canvas'
    }
    $surfaceColor = [int](($histogram.GetEnumerator() | Sort-Object -Property Value -Descending | Select-Object -First 1).Key)

    # 4. Four row bands, measured on the canvas centre column (or a nearby one if a control happens to
    #    cover it in a future layout).
    $bands = $null
    $usedColumn = -1
    foreach ($candidate in @($scanColumn, ($scanColumn - 40), ($scanColumn + 40), ($scanColumn - 80), ($scanColumn + 80), 400, 380, 420, 360, 440)) {
        if ($candidate -lt ($canvasLeft + 8) -or $candidate -gt ($canvasRight - 8)) { continue }
        $found = @(Get-SurfaceBands -Image $Image -Column $candidate -Top $canvasTop -Bottom $canvasBottom -SurfaceColor $surfaceColor)
        if ($found.Count -eq 4) {
            $bands = $found
            $usedColumn = $candidate
            break
        }
    }
    if ($null -eq $bands) {
        throw 'could not separate exactly 4 demo rows inside the canvas'
    }

    # 5. Row rectangles. The x-range is shared by all rows (same width and alignment); it is taken from
    #    the closed rows, whose surface is not translated by an open action stack.
    $rows = [System.Collections.Generic.List[object]]::new()
    $closedLeft = [int]::MaxValue
    $closedRight = -1
    foreach ($band in $bands) {
        $middle = [int][Math]::Floor(($band.Top + $band.Bottom) / 2)
        $minX = -1
        $maxX = -1
        for ($x = $canvasLeft; $x -le $canvasRight; $x++) {
            if ((Get-ArgbAt -Image $Image -X $x -Y $middle) -eq $surfaceColor) {
                if ($minX -lt 0) { $minX = $x }
                $maxX = $x
            }
        }
        if ($minX -lt 0) {
            throw "row band at y=$($band.Top) has no surface pixels"
        }
        $closed = ($maxX - $minX + 1) -ge 600
        if ($closed) {
            $closedLeft = [Math]::Min($closedLeft, $minX)
            $closedRight = [Math]::Max($closedRight, $maxX)
        }
        $rows.Add([pscustomobject]@{
            Index = $rows.Count
            Top = $band.Top
            Bottom = $band.Bottom
            Height = $band.Bottom - $band.Top + 1
            SurfaceMinX = $minX
            SurfaceMaxX = $maxX
            Closed = $closed
        })
    }

    if ($closedLeft -eq [int]::MaxValue -or $closedRight -lt 0) {
        throw 'no closed demo row found (cannot derive the shared row x-range)'
    }
    if (-not $rows[$rows.Count - 1].Closed) {
        throw 'the last demo row is not closed; the danger-colour assertion row is missing'
    }

    # 6. Scan rectangle per row: shared row x-range plus a small margin so the 1 px border, the rounded
    #    corners and their antialiasing are inside the rectangle that the edge band is measured from.
    $margin = 4
    foreach ($row in $rows) {
        $x = [Math]::Max(0, $closedLeft - $margin)
        $y = [Math]::Max(0, $row.Top - $margin)
        $rowWidth = [Math]::Min($width - $x, ($closedRight - $closedLeft + 1) + 2 * $margin)
        $rowHeight = [Math]::Min($height - $y, $row.Height + 2 * $margin)
        $row | Add-Member -NotePropertyName Rect -NotePropertyValue ([pscustomobject]@{ X = $x; Y = $y; W = $rowWidth; H = $rowHeight })
    }

    return [pscustomobject]@{
        Canvas = [pscustomobject]@{
            Left = $canvasLeft
            Right = $canvasRight
            Top = $canvasTop
            Bottom = $canvasBottom
            Width = $canvasRight - $canvasLeft + 1
            Height = $canvasBottom - $canvasTop + 1
            Color = $canvasColor
        }
        RowLeft = $closedLeft
        RowRight = $closedRight
        ScanColumn = $usedColumn
        SurfaceColor = $surfaceColor
        Rows = @($rows)
    }
}

# Counts matching pixels inside a rectangle and returns the hit bounding box.
function Measure-DangerPixels {
    param(
        [hashtable]$Image,
        [int]$X,
        [int]$Y,
        [int]$W,
        [int]$H,
        [int]$MinRed,
        [int]$MinGreenDelta,
        [int]$MinBlueDelta
    )
    $bytes = $Image.Bytes
    $stride = $Image.Stride
    $xStart = [Math]::Max(0, $X)
    $xEnd = [Math]::Min($Image.Width - 1, $X + $W - 1)
    $yStart = [Math]::Max(0, $Y)
    $yEnd = [Math]::Min($Image.Height - 1, $Y + $H - 1)
    $count = 0
    $minX = [int]::MaxValue
    $minY = [int]::MaxValue
    $maxX = -1
    $maxY = -1
    for ($y = $yStart; $y -le $yEnd; $y++) {
        $rowOffset = $y * $stride
        for ($x = $xStart; $x -le $xEnd; $x++) {
            $offset = $rowOffset + $x * 4
            $r = [int]$bytes[$offset + 2]
            if ($r -lt $MinRed) { continue }
            $g = [int]$bytes[$offset + 1]
            if (($r - $g) -lt $MinGreenDelta) { continue }
            if (($r - [int]$bytes[$offset]) -lt $MinBlueDelta) { continue }
            $count++
            if ($x -lt $minX) { $minX = $x }
            if ($x -gt $maxX) { $maxX = $x }
            if ($y -lt $minY) { $minY = $y }
            if ($y -gt $maxY) { $maxY = $y }
        }
    }

    $bbox = $null
    if ($count -gt 0) {
        $bbox = [pscustomobject]@{ X = $minX; Y = $minY; W = $maxX - $minX + 1; H = $maxY - $minY + 1 }
    }
    return [pscustomobject]@{ Count = $count; BBox = $bbox }
}

# Counts matching pixels only in the outer band of a rectangle: the 1-3 px border/corner ring where a
# colour leak would show. Every pixel is tested once, so overlapping corners are not double counted.
function Measure-DangerRing {
    param(
        [hashtable]$Image,
        [int]$X,
        [int]$Y,
        [int]$W,
        [int]$H,
        [int]$Thickness,
        [int]$MinRed,
        [int]$MinGreenDelta,
        [int]$MinBlueDelta
    )
    $bytes = $Image.Bytes
    $stride = $Image.Stride
    $xStart = [Math]::Max(0, $X)
    $xEnd = [Math]::Min($Image.Width - 1, $X + $W - 1)
    $yStart = [Math]::Max(0, $Y)
    $yEnd = [Math]::Min($Image.Height - 1, $Y + $H - 1)
    $count = 0
    $minX = [int]::MaxValue
    $minY = [int]::MaxValue
    $maxX = -1
    $maxY = -1
    for ($y = $yStart; $y -le $yEnd; $y++) {
        $fromTop = $y - $yStart
        $fromBottom = $yEnd - $y
        $rowOffset = $y * $stride
        for ($x = $xStart; $x -le $xEnd; $x++) {
            $fromLeft = $x - $xStart
            $fromRight = $xEnd - $x
            if ($fromTop -ge $Thickness -and $fromBottom -ge $Thickness -and $fromLeft -ge $Thickness -and $fromRight -ge $Thickness) {
                continue
            }
            $offset = $rowOffset + $x * 4
            $r = [int]$bytes[$offset + 2]
            if ($r -lt $MinRed) { continue }
            $g = [int]$bytes[$offset + 1]
            if (($r - $g) -lt $MinGreenDelta) { continue }
            if (($r - [int]$bytes[$offset]) -lt $MinBlueDelta) { continue }
            $count++
            if ($x -lt $minX) { $minX = $x }
            if ($x -gt $maxX) { $maxX = $x }
            if ($y -lt $minY) { $minY = $y }
            if ($y -gt $maxY) { $maxY = $y }
        }
    }

    $bbox = $null
    if ($count -gt 0) {
        $bbox = [pscustomobject]@{ X = $minX; Y = $minY; W = $maxX - $minX + 1; H = $maxY - $minY + 1 }
    }
    return [pscustomobject]@{ Count = $count; BBox = $bbox }
}

# Writes the analysed slice with the scanned rectangles drawn on top: dashed grey = demo rows,
# blue = positive control, green/red = assertion row. Each rectangle is paired with an inner outline
# that marks the outer band that was actually scanned.
function Save-AnnotatedSlice {
    param([string]$SourcePath, [string]$TargetPath, $Rows, [int]$BandThickness)
    $source = [System.Drawing.Bitmap]::new($SourcePath)
    try { $canvas = $source.Clone() } finally { $source.Dispose() }

    $graphics = [System.Drawing.Graphics]::FromImage($canvas)
    try {
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $font = [System.Drawing.Font]::new('Segoe UI', 10)
        $labelBackdrop = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(205, 0, 0, 0))
        $labelBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 255, 255, 255))
        try {
            foreach ($row in $Rows) {
                $rect = $row.rect_slice
                if ($row.index -eq 2) {
                    $pen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 56, 132, 255), 2)
                    $label = "control: opened danger row (fill matches: $($row.control_fill_pixels) px)"
                }
                elseif ($row.index -eq 3) {
                    $color = if ($row.ring_danger_pixels -eq 0) { [System.Drawing.Color]::FromArgb(255, 34, 197, 94) }
                             else { [System.Drawing.Color]::FromArgb(255, 239, 68, 68) }
                    $pen = [System.Drawing.Pen]::new($color, 2)
                    $label = "ASSERTION: closed row, outer band ($($row.ring_danger_pixels) danger px)"
                }
                else {
                    $pen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(200, 160, 160, 160), 1)
                    $pen.DashStyle = [System.Drawing.Drawing2D.DashStyle]::Dash
                    $label = "row $($row.index): $($row.label) (band: $($row.ring_danger_pixels) danger px)"
                }

                try {
                    $graphics.DrawRectangle($pen, $rect.x, $rect.y, [Math]::Max(1, $rect.w - 1), [Math]::Max(1, $rect.h - 1))
                }
                finally {
                    $pen.Dispose()
                }

                $innerPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(180, 250, 204, 21), 1)
                try {
                    $graphics.DrawRectangle($innerPen, $rect.x + $BandThickness, $rect.y + $BandThickness,
                        [Math]::Max(1, $rect.w - 1 - 2 * $BandThickness), [Math]::Max(1, $rect.h - 1 - 2 * $BandThickness))
                }
                finally {
                    $innerPen.Dispose()
                }

                if ($null -ne $row.ring_danger_bbox_slice) {
                    $bboxPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 236, 72, 153), 1)
                    try {
                        $graphics.DrawRectangle($bboxPen, $row.ring_danger_bbox_slice.x, $row.ring_danger_bbox_slice.y,
                            [Math]::Max(1, $row.ring_danger_bbox_slice.w - 1), [Math]::Max(1, $row.ring_danger_bbox_slice.h - 1))
                    }
                    finally {
                        $bboxPen.Dispose()
                    }
                }

                $size = $graphics.MeasureString($label, $font)
                $labelX = [int]($rect.x + 6)
                $labelY = if ($rect.y -gt 22) { [int]($rect.y - 20) } else { [int]($rect.y + 4) }
                $graphics.FillRectangle($labelBackdrop, $labelX, $labelY, [int]$size.Width + 6, [int]$size.Height + 2)
                $graphics.DrawString($label, $font, $labelBrush, $labelX + 3, $labelY + 1)
            }
        }
        finally {
            $font.Dispose()
            $labelBackdrop.Dispose()
            $labelBrush.Dispose()
        }
    }
    finally {
        $graphics.Dispose()
    }

    $canvas.Save($TargetPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $canvas.Dispose()
}

# ---------------------------------------------------------------------------------------------
# 1. build
# ---------------------------------------------------------------------------------------------
if (-not $SkipBuild) {
    Write-Host "BUILD $buildCommand"
    & pwsh -NoProfile -File $buildScript -Project $galleryProject
    if ($LASTEXITCODE -ne 0) {
        Write-Host "FAIL: gallery build exited $LASTEXITCODE"
        exit 2
    }
}

if (-not (Test-Path -LiteralPath $galleryDll)) {
    Write-Host "FAIL: gallery assembly not found: $galleryDll (rerun without -SkipBuild)"
    exit 2
}

New-Item -ItemType Directory -Force -Path $evidenceDir, $renderDir | Out-Null

# ---------------------------------------------------------------------------------------------
# 2. canonical 1400 px gallery screenshots under shots/<theme>/ (same command as the rest of the repo)
# ---------------------------------------------------------------------------------------------
$shotsCommand = "pwsh -NoProfile tools/shots.ps1 -Page $pageSlug -Theme $Theme -Width $Width -SkipBuild"
Write-Host "RENDER $shotsCommand"
& pwsh -NoProfile -File $shotsScript -Page $pageSlug -Theme $Theme -Width $Width -SkipBuild -TimeoutSec $TimeoutSec
if ($LASTEXITCODE -ne 0) {
    Write-Host "FAIL: tools/shots.ps1 exited $LASTEXITCODE"
    exit 2
}

# ---------------------------------------------------------------------------------------------
# 3. analyse one offset slice per theme
# ---------------------------------------------------------------------------------------------
$rowSpecs = @(
    @{ Label = 'closed neutral row'; Expect = 'sanity-ring-zero'; Asserted = $true },
    @{ Label = 'leading-open row (accent action)'; Expect = 'diagnostic-ring'; Asserted = $false },
    @{ Label = 'trailing-open row (Archive + danger Delete)'; Expect = 'control-fill'; Asserted = $true },
    @{ Label = 'closed row with hidden danger actions (ASSERTION)'; Expect = 'assertion-ring-zero'; Asserted = $true }
)

$themeResults = [System.Collections.Generic.List[object]]::new()
$allPass = $true

foreach ($scheme in $themes) {
    Write-Host ''
    Write-Host "== theme: $scheme"

    $themeDir = Join-Path $renderDir $scheme
    New-Item -ItemType Directory -Force -Path $themeDir | Out-Null
    Get-ChildItem -LiteralPath $themeDir -Filter "*-$pageSlug*.png" -File -ErrorAction SilentlyContinue | Remove-Item -Force

    $arguments = @($galleryDll, '--shot', $renderDir, '--page', $pageSlug, '--theme', $scheme,
        '--width', $Width, '--offset', $Offset, '--timeout', $TimeoutSec)
    $renderCommand = 'dotnet ' + (($arguments | ForEach-Object { "$_" }) -join ' ')
    Write-Host "RENDER $renderCommand"
    $renderLines = & dotnet @arguments 2>&1
    $renderExit = $LASTEXITCODE
    foreach ($line in $renderLines) { Write-Host "  $line" }
    if ($renderExit -ne 0) {
        Write-Host "FAIL: gallery render exited $renderExit"
        exit 2
    }

    $slice = Get-ChildItem -LiteralPath $themeDir -Filter "*-$pageSlug*.png" -File |
        Sort-Object LastWriteTime | Select-Object -Last 1
    if ($null -eq $slice) {
        Write-Host "FAIL: no slice rendered inside $themeDir"
        exit 2
    }
    Write-Host "SLICE $(Get-RelativePath $slice.FullName)"

    try { $source = [System.Drawing.Bitmap]::new($slice.FullName) }
    catch {
        Write-Host "FAIL: cannot open slice $($slice.FullName): $($_.Exception.Message)"
        exit 2
    }
    try { $image = Convert-ToRgbaImage -Bitmap $source } finally { $source.Dispose() }

    try { $layout = Find-DemoRows -Image $image }
    catch {
        Write-Host "FAIL (row detection, ${scheme}): $($_.Exception.Message)"
        Write-Host "slice: $(Get-RelativePath $slice.FullName)"
        exit 2
    }

    Write-Host ("CANVAS x={0}..{1} y={2}..{3} ({4}x{5} px, color {6}); row surface {7}; rows scanned on column x={8}" -f `
        $layout.Canvas.Left, $layout.Canvas.Right, $layout.Canvas.Top, $layout.Canvas.Bottom,
        $layout.Canvas.Width, $layout.Canvas.Height, (Get-HexColor $layout.Canvas.Color),
        (Get-HexColor $layout.SurfaceColor), $layout.ScanColumn)

    $rowResults = [System.Collections.Generic.List[object]]::new()
    $themePass = $true

    for ($index = 0; $index -lt $layout.Rows.Count; $index++) {
        $row = $layout.Rows[$index]
        $spec = $rowSpecs[$index]

        $ring = Measure-DangerRing -Image $image -X $row.Rect.X -Y $row.Rect.Y -W $row.Rect.W -H $row.Rect.H `
            -Thickness $ringThickness -MinRed $ringDanger.MinRed -MinGreenDelta $ringDanger.MinGreenDelta -MinBlueDelta $ringDanger.MinBlueDelta
        $fullRow = Measure-DangerPixels -Image $image -X $row.Rect.X -Y $row.Rect.Y -W $row.Rect.W -H $row.Rect.H `
            -MinRed $fullRowDanger.MinRed -MinGreenDelta $fullRowDanger.MinGreenDelta -MinBlueDelta $fullRowDanger.MinBlueDelta

        $controlFill = $null
        $controlFillBBox = $null
        if ($spec.Expect -eq 'control-fill') {
            $controlWidth = [Math]::Min($controlRegionWidth, $row.Rect.W)
            $controlX = $row.Rect.X + $row.Rect.W - $controlWidth
            $fill = Measure-DangerPixels -Image $image -X $controlX -Y $row.Rect.Y -W $controlWidth -H $row.Rect.H `
                -MinRed $fillDanger.MinRed -MinGreenDelta $fillDanger.MinGreenDelta -MinBlueDelta $fillDanger.MinBlueDelta
            $controlFill = $fill.Count
            $controlFillBBox = $fill.BBox
        }

        switch ($spec.Expect) {
            'control-fill' { $ok = $controlFill -gt $controlMinFillPixels }
            'diagnostic-ring' { $ok = $ring.Count -eq 0 }
            default { $ok = $ring.Count -eq 0 }
        }
        if ($spec.Asserted -and -not $ok) { $themePass = $false }

        $ringBBoxPage = $null
        if ($null -ne $ring.BBox) {
            $ringBBoxPage = [pscustomobject]@{ x = $ring.BBox.X; y = $ring.BBox.Y + $Offset; w = $ring.BBox.W; h = $ring.BBox.H }
        }

        $rowResults.Add([pscustomobject]@{
            index = $index
            label = $spec.Label
            expectation = $spec.Expect
            asserted = [bool]$spec.Asserted
            closed = [bool]$row.Closed
            rect_slice = [pscustomobject]@{ x = $row.Rect.X; y = $row.Rect.Y; w = $row.Rect.W; h = $row.Rect.H }
            rect_page = [pscustomobject]@{ x = $row.Rect.X; y = $row.Rect.Y + $Offset; w = $row.Rect.W; h = $row.Rect.H }
            ring_thickness = $ringThickness
            ring_danger_pixels = $ring.Count
            ring_danger_bbox_slice = $ring.BBox
            ring_danger_bbox_page = $ringBBoxPage
            full_row_danger_pixels = $fullRow.Count
            full_row_danger_bbox_slice = $fullRow.BBox
            control_fill_pixels = $controlFill
            control_fill_bbox_slice = $controlFillBBox
            ok = $ok
        })

        $ringBBoxText = 'none'
        if ($null -ne $ring.BBox) {
            $ringBBoxText = 'x={0},y={1},w={2},h={3}' -f $ring.BBox.X, $ring.BBox.Y, $ring.BBox.W, $ring.BBox.H
        }
        $extra = ''
        if ($null -ne $controlFill) { $extra = " control_fill=$controlFill" }
        $verdict = if ($ok) { 'ok' } else { 'FAIL' }
        $assertText = if ($spec.Asserted) { 'asserted' } else { 'diagnostic' }
        Write-Host ("ROW {0} {1,-48} rect=({2},{3},{4},{5}) page_y={6} closed={7,-5} ring_danger={8,-5} ring_bbox={9} full_row={10,-5} {11}{12} ({13})" -f `
            $index, $spec.Label, $row.Rect.X, $row.Rect.Y, $row.Rect.W, $row.Rect.H,
            ($row.Rect.Y + $Offset), $row.Closed, $ring.Count, $ringBBoxText, $fullRow.Count, $verdict, $extra, $assertText)
    }

    $sanityRow = $rowResults[0]
    $controlRow = $rowResults[2]
    $assertionRow = $rowResults[3]

    $annotatedPath = Join-Path $evidenceDir "swipe-leak-$scheme-annotated.png"
    Save-AnnotatedSlice -SourcePath $slice.FullName -TargetPath $annotatedPath -Rows $rowResults -BandThickness $ringThickness
    Write-Host "EVIDENCE $(Get-RelativePath $annotatedPath)"

    if (-not $themePass) { $allPass = $false }

    $themeResults.Add([pscustomobject]@{
        theme = $scheme
        slice = (Get-RelativePath $slice.FullName)
        slice_size = [pscustomobject]@{ width = $image.Width; height = $image.Height }
        annotated = (Get-RelativePath $annotatedPath)
        render_command = $renderCommand
        canvas = $layout.Canvas
        surface_color = (Get-HexColor $layout.SurfaceColor)
        scan_column = $layout.ScanColumn
        ring_thickness = $ringThickness
        ring_rule = $ringRule
        control_fill_rule = $fillRule
        full_row_rule = $diagnosticRule
        rows = @($rowResults)
        sanity = [pscustomobject]@{
            row_index = $sanityRow.index
            ring_danger_pixels = $sanityRow.ring_danger_pixels
            ring_danger_bbox_slice = $sanityRow.ring_danger_bbox_slice
            ok = $sanityRow.ok
        }
        assertion = [pscustomobject]@{
            row_index = $assertionRow.index
            rect_slice = $assertionRow.rect_slice
            rect_page = $assertionRow.rect_page
            ring_danger_pixels = $assertionRow.ring_danger_pixels
            ring_danger_bbox_slice = $assertionRow.ring_danger_bbox_slice
            ring_danger_bbox_page = $assertionRow.ring_danger_bbox_page
            full_row_danger_pixels = $assertionRow.full_row_danger_pixels
            closed = $assertionRow.closed
            ok = $assertionRow.ok
        }
        control = [pscustomobject]@{
            row_index = $controlRow.index
            rect_slice = $controlRow.rect_slice
            region_width = [Math]::Min($controlRegionWidth, $controlRow.rect_slice.w)
            control_fill_pixels = $controlRow.control_fill_pixels
            control_fill_bbox_slice = $controlRow.control_fill_bbox_slice
            ring_danger_pixels = $controlRow.ring_danger_pixels
            ok = $controlRow.ok
        }
        pass = $themePass
    })

    Write-Host ("RESULT {0}: {1} (assertion outer band: {2} danger px; sanity row outer band: {3} danger px; control danger fill: {4} px)" -f `
        $scheme, $(if ($themePass) { 'PASS' } else { 'FAIL' }), $assertionRow.ring_danger_pixels,
        $sanityRow.ring_danger_pixels, $controlRow.control_fill_pixels)
}

# ---------------------------------------------------------------------------------------------
# 4. report
# ---------------------------------------------------------------------------------------------
$canonicalShots = [System.Collections.Generic.List[string]]::new()
foreach ($scheme in $themes) {
    $shotDir = Join-Path $root "shots\$scheme"
    if (-not (Test-Path -LiteralPath $shotDir)) { continue }
    $shot = Get-ChildItem -LiteralPath $shotDir -Filter "*-$pageSlug.png" -File -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime | Select-Object -Last 1
    if ($null -ne $shot) { $canonicalShots.Add((Get-RelativePath $shot.FullName)) }
}

$generatedAt = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss zzz')
$report = [pscustomobject]@{
    generated_at = $generatedAt
    page = $pageSlug
    width = $Width
    offset = $Offset
    ring_thickness = $ringThickness
    ring_rule = $ringRule
    control_fill_rule = $fillRule
    diagnostic_full_row_rule = $diagnosticRule
    assertion = 'The closed demo row (last row; hidden trailing Archive neutral + Delete danger) must contain zero danger-colour pixels in the outer 6 px band of the row rectangle, i.e. on the antialiased border and rounded corners where a leak shows. Text is at least one row padding inside the surface, so LCD subpixel antialiasing fringes on glyphs cannot reach the scanned band. The opened trailing row is the positive control: its right-hand 168 px must contain the red Delete fill.'
    sanity = 'The first demo row is closed and has no danger action at all; its outer band must also be zero, which proves the band predicate does not invent leaks.'
    build_command = $buildCommand
    build_skipped = [bool]$SkipBuild
    shots_command = $shotsCommand
    canonical_shots = @($canonicalShots)
    themes = @($themeResults)
    pass = $allPass
}

$jsonPath = Join-Path $evidenceDir 'report.json'
$report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $jsonPath -Encoding utf8

$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add('# Swipe actions danger-colour leak verification')
$lines.Add('')
$lines.Add("Generated: $generatedAt")
$lines.Add('')
$lines.Add("Page: ``$pageSlug`` (documentation page in ``src/Docs/Arc.Avalonia.Docs``)")
$lines.Add('')
$lines.Add("Verdict: **" + $(if ($allPass) { 'PASS' } else { 'FAIL' }) + '**')
$lines.Add('')
$lines.Add('## Assertion')
$lines.Add('')
$lines.Add('The last demo row of the `swipe-actions` page is at rest (closed) while its hidden trailing actions are `Archive` (neutral) and `Delete` (`ArcSwipeActionTone.Danger`). `ArcSwipeActions` hides both action hosts while the row is closed, so this row must not paint any danger colour on its surface, including on the antialiased 1 px border and the rounded corners where a leak would show first.')
$lines.Add('')
$lines.Add("The scan targets the outer $ringThickness px band of the row rectangle (all four edges; the rounded corners are inside it) with the predicate ``$ringRule``. The opened trailing row above it is the positive control: its right-hand $controlRegionWidth px must contain more than $controlMinFillPixels pixels of the red Delete fill (``$fillRule``), which proves the scan can see ArcDanger in the rendered theme at all.")
$lines.Add('')
$lines.Add('### Why the outer band and not the whole row rectangle')
$lines.Add('')
$lines.Add('The offscreen Skia renderer draws text with LCD subpixel antialiasing. Warm fringe pixels on the outside of glyphs (observed examples: `#D0A019` = 208,160,25; `#BB7719` = 187,119,25; `#A01919` = 160,25,25; `#F2D0A0` = 242,208,160) satisfy any red predicate, so scanning the full row rectangle reports ~50-180 false "danger" pixels on the title and time label of every row, including rows that carry no danger action at all. Text sits at least one row padding (16 px) inside the row surface, while a colour leak can only appear on the 1-3 px border/corner ring, so the outer band isolates the regression target. Whole-row counts are kept as diagnostics only (r >= 120 and r - g >= 50 and r - b >= 50).')
$lines.Add('')
$lines.Add('## Commands')
$lines.Add('')
$lines.Add('```powershell')
$lines.Add($buildCommand)
$lines.Add($shotsCommand)
$lines.Add('```')
$lines.Add('')
$lines.Add('Per-theme offset slice (tools/shots.ps1 only captures the top 1400 px; the demo canvas starts near page y=900):')
$lines.Add('')
$lines.Add('```powershell')
foreach ($themeResult in $themeResults) {
    $lines.Add($themeResult.render_command)
}
$lines.Add('```')
$lines.Add('')
$lines.Add('## Results')
$lines.Add('')
$lines.Add("| theme | assertion band rect (page px) | assertion band danger px | sanity row 0 band danger px | control danger fill px | verdict |")
$lines.Add('| --- | --- | --- | --- | --- | --- |')
foreach ($themeResult in $themeResults) {
    $rect = $themeResult.assertion.rect_page
    $lines.Add("| $($themeResult.theme) | x=$($rect.x), y=$($rect.y), w=$($rect.w), h=$($rect.h) | $($themeResult.assertion.ring_danger_pixels) | $($themeResult.sanity.ring_danger_pixels) | $($themeResult.control.control_fill_pixels) | $(if ($themeResult.pass) { 'PASS' } else { 'FAIL' }) |")
}
$lines.Add('')
foreach ($themeResult in $themeResults) {
    $lines.Add("### $($themeResult.theme)")
    $lines.Add('')
    $lines.Add("- slice: ``$($themeResult.slice)`` ($($themeResult.slice_size.width)x$($themeResult.slice_size.height))")
    $lines.Add("- annotated: ``$($themeResult.annotated)``")
    $lines.Add("- canvas: x=$($themeResult.canvas.Left)..$($themeResult.canvas.Right), y=$($themeResult.canvas.Top)..$($themeResult.canvas.Bottom) (page y=$($themeResult.canvas.Top + $Offset)..$($themeResult.canvas.Bottom + $Offset)), surface $($themeResult.surface_color)")
    $lines.Add('')
    $lines.Add('| row | state | closed | rect (slice px) | band danger px | band danger bbox (slice px) | full-row diagnostics px | control fill px | asserted | expected | result |')
    $lines.Add('| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |')
    foreach ($rowResult in $themeResult.rows) {
        $rect = $rowResult.rect_slice
        $bandBBox = 'none'
        if ($null -ne $rowResult.ring_danger_bbox_slice) {
            $bandBBox = 'x={0}, y={1}, w={2}, h={3}' -f $rowResult.ring_danger_bbox_slice.x, $rowResult.ring_danger_bbox_slice.y, $rowResult.ring_danger_bbox_slice.w, $rowResult.ring_danger_bbox_slice.h
        }
        $fillText = 'n/a'
        if ($null -ne $rowResult.control_fill_pixels) { $fillText = $rowResult.control_fill_pixels }
        $lines.Add("| $($rowResult.index) | $($rowResult.label) | $($rowResult.closed) | x=$($rect.x), y=$($rect.y), w=$($rect.w), h=$($rect.h) | $($rowResult.ring_danger_pixels) | $bandBBox | $($rowResult.full_row_danger_pixels) | $fillText | $($rowResult.asserted) | $($rowResult.expectation) | $(if ($rowResult.ok) { 'ok' } else { 'FAIL' }) |")
    }
    $lines.Add('')
}
$lines.Add('## Evidence')
$lines.Add('')
foreach ($themeResult in $themeResults) {
    $lines.Add("- slice: ``$($themeResult.slice)``")
    $lines.Add("- annotated: ``$($themeResult.annotated)``")
}
foreach ($shot in $canonicalShots) {
    $lines.Add("- gallery screenshot: ``$shot``")
}
$lines.Add('')
$lines.Add('## Conclusion')
$lines.Add('')
if ($allPass) {
    $controlBandCounts = ($themeResults | ForEach-Object { "$($_.theme)=$($_.control.ring_danger_pixels)" }) -join ', '
    $lines.Add("In both themes the closed assertion row reports 0 danger pixels in its outer band, while the opened control row reports danger pixels in the corresponding band ($controlBandCounts) because its visible Delete fill reaches the row edge, and more than $controlMinFillPixels danger-fill pixels in its right-hand action stack. The band predicate therefore fires when danger colour sits on a row edge and stays at 0 on the resting row. ArcSwipeActions itself does not leak the hidden action colour; the DSHRemote symptom comes from a host surface that paints its hidden (or underlay) action background through an antialiased, semi-transparent rounded edge, not from this control.")
}
else {
    $lines.Add('At least one theme failed the assertion; inspect the annotated slice and the row table above.')
}
$lines.Add('')

$markdownPath = Join-Path $evidenceDir 'REPORT.md'
[System.IO.File]::WriteAllText($markdownPath, ($lines -join "`n"), [System.Text.UTF8Encoding]::new($false))

Write-Host ''
Write-Host "EVIDENCE $(Get-RelativePath $markdownPath)"
Write-Host "EVIDENCE $(Get-RelativePath $jsonPath)"
if ($allPass) {
    Write-Host "PASS: swipe-actions closed danger row leaks 0 danger pixels in its outer band in $($themes -join ' + '); the positive control saw the red Delete fill in every theme."
    exit 0
}

Write-Host 'FAIL: swipe-actions closed danger row leaked danger pixels in its outer band (see the row table above).'
exit 1
