<#
PureDS4 app icon ("Brand Pad", owner pick 2026-11-20): an original, simplified gamepad
(not a traced DualShock; no PlayStation face-button symbols) on a PureDS4-blue tile.
Authored geometry, written once as SVG-compatible data and rendered by WPF; no
external assets, fonts or tools. Each ICO frame is drawn from the vector at its own
size (details that cannot survive below a size are skipped, see "min").

Run:  pwsh -NoProfile -STA -File branding/brand-pad/generate.ps1 [-Install] [-Out <folder>]
  -Out      previews: SVG, PNG sizes and a contact sheet (default: <script dir>/out)
  -Install  also writes PureDS4/DS4W.ico, PureDS4/Resources/DS4W.ico and the
            1024 px PureDS4/Resources/AppIcon.png, and refreshes brand-pad.svg here.
#>
param(
    [string]$Out = (Join-Path $PSScriptRoot 'out'),
    [switch]$Install
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase
$inv = [Globalization.CultureInfo]::InvariantCulture

# ------------------------------------------------------------ geometry helpers
function F([double]$v) { $v.ToString('0.###', $inv) }
function Ellipse([double]$cx, [double]$cy, [double]$rx, [double]$ry) {
    "M $(F ($cx-$rx)),$(F $cy) A $(F $rx),$(F $ry) 0 1 0 $(F ($cx+$rx)),$(F $cy) A $(F $rx),$(F $ry) 0 1 0 $(F ($cx-$rx)),$(F $cy) Z"
}
function Circle([double]$cx, [double]$cy, [double]$r) { Ellipse $cx $cy $r $r }
function RoundRect([double]$x, [double]$y, [double]$w, [double]$h, [double]$r) {
    $x2 = $x + $w; $y2 = $y + $h
    "M $(F ($x+$r)),$(F $y) H $(F ($x2-$r)) A $(F $r),$(F $r) 0 0 1 $(F $x2),$(F ($y+$r)) V $(F ($y2-$r)) " +
    "A $(F $r),$(F $r) 0 0 1 $(F ($x2-$r)),$(F $y2) H $(F ($x+$r)) A $(F $r),$(F $r) 0 0 1 $(F $x),$(F ($y2-$r)) " +
    "V $(F ($y+$r)) A $(F $r),$(F $r) 0 0 1 $(F ($x+$r)),$(F $y) Z"
}
<# A shape: d (path), fill ('none', '#hex' or 'url(#id)'), opacity, min (smallest
   pixel size that draws it), rule, stroke, width, transform (SVG list of
   translate(x y) / rotate(a cx cy)). #>
function S([hashtable]$p) {
    $defaults = @{ fill = 'none'; opacity = 1.0; min = 0; rule = 'nonzero'; stroke = $null; width = 0; transform = '' }
    foreach ($k in $defaults.Keys) { if (-not $p.ContainsKey($k)) { $p[$k] = $defaults[$k] } }
    [pscustomobject]$p
}
function Lin($x1, $y1, $x2, $y2, $stops) { @{ type = 'linear'; x1 = $x1; y1 = $y1; x2 = $x2; y2 = $y2; stops = $stops } }
function Rad($cx, $cy, $r, $stops) { @{ type = 'radial'; cx = $cx; cy = $cy; r = $r; stops = $stops } }
# A stop: offset, colour, opacity.
function Glow($color, [double]$alpha = 1) { @(@(0, $color, $alpha), @(1, $color, 0)) }

# Squircle-like tile and its finish: a soft top sheen and a hairline edge.
$tile = RoundRect 16 16 224 224 54
$tileFinish = @(
    (S @{ d = (RoundRect 16 16 224 112 54); fill = 'url(#sheen)'; min = 32 })
    (S @{ d = (RoundRect 17 17 222 222 53); stroke = '#FFFFFF'; width = 2; opacity = 0.12; min = 48 })
)
$sheen = Lin 0 0 0 1 @(@(0, '#FFFFFF', 0.14), @(1, '#FFFFFF', 0))

# ------------------------------------------------------------------ concepts
$concepts = [ordered]@{}

$pad = 'M 78,78 H 178 C 204,78 214,94 218,116 L 230,176 C 234,202 210,214 194,200 L 170,178 H 86 L 62,200 C 46,214 22,202 26,176 L 38,116 C 42,94 52,78 78,78 Z'

# Gamepad layout: d-pad left, four plain buttons right (no PlayStation symbols), two
# sticks low in the middle, light bar on top with clear space below it. Centred in the
# tile, PureDS4 blues (accent #285F8C / #94BFDF). $angle tilts it (0 = upright, chosen).
function Brand-Pad([double]$angle, [string]$title, [string]$idea) {
    # The body spans x 22..234, y 78..214: lift it 18 so its mass sits on the tile centre.
    $place = "translate($(F ($angle / 2)) -18) rotate($(F $angle) 128 146)"
    $navy = '#0E2640'
    $shapes = @(
        (S @{ d = $tile; fill = 'url(#tile)' })
        (S @{ d = $tile; fill = 'url(#lift)' })
        (S @{ d = $tile; fill = 'url(#deep)' })
        $tileFinish
        (S @{ d = $pad; fill = '#04101E'; opacity = 0.18; transform = "translate(0 12) $place"; min = 32 })
        (S @{ d = $pad; fill = '#04101E'; opacity = 0.26; transform = "translate(0 6) $place" })
        (S @{ d = $pad; fill = 'url(#body)'; transform = $place })
        (S @{ d = (RoundRect 90 76 76 30 15); fill = '#94BFDF'; opacity = 0.22; transform = $place; min = 32 })
        (S @{ d = (RoundRect 96 80 64 22 11); fill = '#94BFDF'; opacity = 0.4; transform = $place; min = 24 })
        (S @{ d = (RoundRect 103 86 50 10 5); fill = 'url(#bar)'; transform = $place })
        (S @{ d = ((RoundRect 71 112 14 40 3) + ' ' + (RoundRect 58 125 40 14 3)); fill = $navy; transform = $place })
        (S @{ d = ((Circle 178 115 7) + ' ' + (Circle 178 149 7) + ' ' + (Circle 161 132 7) + ' ' + (Circle 195 132 7)); fill = $navy; transform = $place; min = 32 })
        (S @{ d = ((Circle 104 160 16) + ' ' + (Circle 152 160 16)); fill = $navy; transform = $place })
        (S @{ d = ((Circle 104 160 8) + ' ' + (Circle 152 160 8)); fill = '#4A9BDB'; transform = $place; min = 24 })
    )
    [pscustomobject]@{
        title = $title
        idea = $idea
        gradients = @{
            tile = Lin 0 0 1 1 @(@(0, '#3577B2', 1), @(1, '#0E2640', 1))
            lift = Rad 0.12 0.08 0.65 (Glow '#94BFDF' 0.5)
            deep = Rad 0.9 0.95 0.6 (Glow '#071526' 0.6)
            sheen = $sheen
            body = Lin 0 0 0 1 @(@(0, '#FFFFFF', 1), @(1, '#CFDDEA', 1))
            bar = Lin 0 0 1 0 @(@(0, '#D9EEFF', 1), @(1, '#6FB4EA', 1))
        }
        shapes = $shapes
    }
}
$concepts['brand-pad'] = Brand-Pad 0 'Brand Pad' 'Original gamepad (d-pad, plain buttons, two sticks) on a PureDS4-blue tile.'

# ------------------------------------------------------------------- output
function Write-Svg($concept, $path) {
    $defs = foreach ($name in $concept.gradients.Keys) {
        $g = $concept.gradients[$name]
        $stops = ($g.stops | ForEach-Object { "<stop offset=`"$(F $_[0])`" stop-color=`"$($_[1])`" stop-opacity=`"$(F $_[2])`"/>" }) -join ''
        if ($g.type -eq 'radial') { "<radialGradient id=`"$name`" cx=`"$(F $g.cx)`" cy=`"$(F $g.cy)`" r=`"$(F $g.r)`">$stops</radialGradient>" }
        else { "<linearGradient id=`"$name`" x1=`"$(F $g.x1)`" y1=`"$(F $g.y1)`" x2=`"$(F $g.x2)`" y2=`"$(F $g.y2)`">$stops</linearGradient>" }
    }
    $paths = foreach ($s in $concept.shapes) {
        $a = "d=`"$($s.d)`" fill=`"$($s.fill)`""
        if ($s.opacity -lt 1) { $a += " opacity=`"$(F $s.opacity)`"" }
        if ($s.rule -eq 'evenodd') { $a += ' fill-rule="evenodd"' }
        if ($s.stroke) { $a += " stroke=`"$($s.stroke)`" stroke-width=`"$(F $s.width)`" stroke-linecap=`"round`" stroke-linejoin=`"round`"" }
        if ($s.transform) { $a += " transform=`"$($s.transform)`"" }
        "  <path $a/>"
    }
    $svg = @(
        '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 256 256" width="256" height="256">'
        "  <title>PureDS4 - $($concept.title)</title>"
        "  <defs>$($defs -join '')</defs>"
        $paths
        '</svg>'
    ) -join "`n"
    [IO.File]::WriteAllText($path, $svg + "`n")
}

function Color($hex, [double]$alpha) {
    $c = [Windows.Media.ColorConverter]::ConvertFromString($hex)
    $c.A = [byte][math]::Round(255 * $alpha); $c
}

function Brush-For($concept, $fill) {
    if (-not $fill -or $fill -eq 'none') { return $null }
    if ($fill -match '^url\(#(.+)\)$') {
        $g = $concept.gradients[$Matches[1]]
        if ($g.type -eq 'radial') {
            $brush = [Windows.Media.RadialGradientBrush]::new()
            $brush.Center = [Windows.Point]::new($g.cx, $g.cy); $brush.GradientOrigin = $brush.Center
            $brush.RadiusX = $g.r; $brush.RadiusY = $g.r
        } else {
            $brush = [Windows.Media.LinearGradientBrush]::new()
            $brush.StartPoint = [Windows.Point]::new($g.x1, $g.y1)
            $brush.EndPoint = [Windows.Point]::new($g.x2, $g.y2)
        }
        foreach ($s in $g.stops) { $brush.GradientStops.Add([Windows.Media.GradientStop]::new((Color $s[1] $s[2]), $s[0])) }
        return $brush
    }
    [Windows.Media.SolidColorBrush]::new((Color $fill 1))
}

# SVG applies a transform list right to left; a WPF TransformGroup applies children in order.
function Transform-For([string]$list) {
    if (-not $list) { return $null }
    $group = [Windows.Media.TransformGroup]::new()
    $items = [regex]::Matches($list, '(\w+)\(([^)]*)\)') | ForEach-Object { $_ }
    [array]::Reverse($items)
    foreach ($m in $items) {
        $n = $m.Groups[2].Value -split '[ ,]+' | Where-Object { $_ } | ForEach-Object { [double]::Parse($_, $inv) }
        switch ($m.Groups[1].Value) {
            'translate' { $group.Children.Add([Windows.Media.TranslateTransform]::new($n[0], $n[1])) }
            'rotate' { $group.Children.Add([Windows.Media.RotateTransform]::new($n[0], $n[1], $n[2])) }
        }
    }
    $group
}

function Render-Icon($concept, [int]$size) {
    $visual = [Windows.Media.DrawingVisual]::new()
    $dc = $visual.RenderOpen()
    $dc.PushTransform([Windows.Media.ScaleTransform]::new($size / 256.0, $size / 256.0))
    # Everything stays inside the tile.
    $dc.PushClip([Windows.Media.Geometry]::Parse($tile))
    foreach ($s in $concept.shapes) {
        if ($size -lt $s.min) { continue }
        $prefix = if ($s.rule -eq 'evenodd') { 'F0 ' } else { 'F1 ' }
        $geometry = [Windows.Media.Geometry]::Parse($prefix + $s.d)
        $pen = $null
        if ($s.stroke) {
            $pen = [Windows.Media.Pen]::new((Brush-For $concept $s.stroke), $s.width)
            $pen.StartLineCap = 'Round'; $pen.EndLineCap = 'Round'; $pen.LineJoin = 'Round'
        }
        $t = Transform-For $s.transform
        if ($t) { $dc.PushTransform($t) }
        if ($s.opacity -lt 1) { $dc.PushOpacity($s.opacity) }
        $dc.DrawGeometry((Brush-For $concept $s.fill), $pen, $geometry)
        if ($s.opacity -lt 1) { $dc.Pop() }
        if ($t) { $dc.Pop() }
    }
    $dc.Pop(); $dc.Pop(); $dc.Close()
    $bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new($size, $size, 96, 96, [Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $bitmap.Freeze()
    $bitmap
}

function Save-Png($bitmap, $path) {
    $encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = [IO.File]::Create($path)
    try { $encoder.Save($stream) } finally { $stream.Dispose() }
}

function Text($value, [double]$size, $color, [string]$weight = 'Normal') {
    $t = [Windows.Controls.TextBlock]::new()
    $t.Text = $value; $t.FontSize = $size; $t.FontFamily = [Windows.Media.FontFamily]::new('Segoe UI')
    $t.FontWeight = [Windows.FontWeights]::$weight
    $t.Foreground = [Windows.Media.SolidColorBrush]::new([Windows.Media.ColorConverter]::ConvertFromString($color))
    $t
}
function Img($bitmap, [double]$display) {
    $i = [Windows.Controls.Image]::new()
    $i.Source = $bitmap; $i.Width = $display; $i.Height = $display
    [Windows.Media.RenderOptions]::SetBitmapScalingMode($i, [Windows.Media.BitmapScalingMode]::NearestNeighbor)
    $i
}
function Put($canvas, $element, [double]$x, [double]$y) {
    [Windows.Controls.Canvas]::SetLeft($element, $x); [Windows.Controls.Canvas]::SetTop($element, $y)
    [void]$canvas.Children.Add($element)
}
function Box($color, [double]$w, [double]$h, [double]$radius = 0) {
    $b = [Windows.Controls.Border]::new()
    $b.Width = $w; $b.Height = $h; $b.CornerRadius = [Windows.CornerRadius]::new($radius)
    $b.Background = [Windows.Media.SolidColorBrush]::new([Windows.Media.ColorConverter]::ConvertFromString($color))
    $b
}

function Contact-Sheet($concept, $icons, $path) {
    $W = 1200; $H = 820
    $c = [Windows.Controls.Canvas]::new(); $c.Width = $W; $c.Height = $H
    $c.Background = [Windows.Media.SolidColorBrush]::new([Windows.Media.ColorConverter]::ConvertFromString('#FAFAFA'))
    Put $c (Text "PureDS4 icon concept: $($concept.title)" 26 '#1B1F24' 'SemiBold') 32 24
    Put $c (Text $concept.idea 15 '#4A5560') 32 64

    Put $c (Text 'Actual pixels: 256 / 48 / 32 / 24 / 16' 13 '#4A5560' 'SemiBold') 32 104
    Put $c (Img $icons[256] 256) 32 128
    $x = 316
    foreach ($s in 48, 32, 24, 16) { Put $c (Img $icons[$s] $s) $x (128 + 256 - $s); Put $c (Text "$s" 12 '#4A5560') $x 392; $x += $s + 28 }
    Put $c (Text 'Small sizes enlarged 4x (pixel check)' 13 '#4A5560' 'SemiBold') 560 104
    Put $c (Img $icons[24] 96) 560 128; Put $c (Text '24 px' 12 '#4A5560') 560 230
    Put $c (Img $icons[16] 64) 680 160; Put $c (Text '16 px' 12 '#4A5560') 680 230

    Put $c (Text 'Taskbar (dark and light, 100 % scaling)' 13 '#4A5560' 'SemiBold') 32 430
    foreach ($row in @(@('#1C1C1C', '#2B2B2B', 456), @('#EEEEEE', '#DADADA', 520))) {
        Put $c (Box $row[0] 520 48) 32 $row[2]
        $tx = 180
        foreach ($k in 0..3) {
            if ($k -eq 1) { Put $c (Box $row[1] 40 40 4) ($tx - 8) ($row[2] + 4); Put $c (Img $icons[24] 24) $tx ($row[2] + 12) }
            else { Put $c (Box $row[1] 24 24 6) $tx ($row[2] + 12) }
            $tx += 48
        }
    }

    Put $c (Text 'Title bar (16 px)' 13 '#4A5560' 'SemiBold') 600 430
    foreach ($row in @(@('#F3F3F3', '#1B1F24', 456), @('#202020', '#F3F3F3', 520))) {
        Put $c (Box $row[0] 560 40) 600 $row[2]
        Put $c (Img $icons[16] 16) 614 ($row[2] + 12)
        Put $c (Text 'PureDS4' 12 $row[1]) 640 ($row[2] + 11)
        Put $c (Text ([string][char]0x2014 + '   ' + [char]0x25A1 + '   ' + [char]0x2715) 12 $row[1]) 1080 ($row[2] + 11)
    }

    Put $c (Text 'Installer and Start menu' 13 '#4A5560' 'SemiBold') 32 600
    Put $c (Box '#FFFFFF' 520 170 8) 32 624
    Put $c (Img $icons[48] 40) 56 648
    Put $c (Text 'PureDS4 Setup' 20 '#1B1F24' 'SemiBold') 108 648
    Put $c (Text 'Version 5.2.0' 13 '#4A5560') 108 676
    Put $c (Box '#285F8C' 120 30 4) 408 748; Put $c (Text 'Install' 13 '#FFFFFF' 'SemiBold') 446 754
    Put $c (Box '#202020' 560 170 8) 600 624
    foreach ($k in 0..5) {
        $sx = 632 + $k * 86
        if ($k -eq 2) { Put $c (Img $icons[48] 48) ($sx + 8) 656; Put $c (Text 'PureDS4' 12 '#F3F3F3') ($sx + 8) 712 }
        else { Put $c (Box '#3A3A3A' 48 48 10) ($sx + 8) 656 }
    }

    $c.Measure([Windows.Size]::new($W, $H)); $c.Arrange([Windows.Rect]::new(0, 0, $W, $H)); $c.UpdateLayout()
    $bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new($W, $H, 96, 96, [Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($c)
    Save-Png $bitmap $path
}

function Write-Ico($concept, [int[]]$sizes, [string]$path) {
    $frames = foreach ($size in $sizes) {
        $encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
        $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create((Render-Icon $concept $size)))
        $ms = [IO.MemoryStream]::new(); $encoder.Save($ms)
        [pscustomobject]@{ size = $size; bytes = $ms.ToArray() }
    }
    $out = [IO.MemoryStream]::new(); $w = [IO.BinaryWriter]::new($out)
    $w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$frames.Count)
    $offset = 6 + 16 * $frames.Count
    foreach ($f in $frames) {
        $dim = [byte]($f.size % 256)
        $w.Write($dim); $w.Write($dim); $w.Write([byte]0); $w.Write([byte]0)
        $w.Write([uint16]1); $w.Write([uint16]32); $w.Write([uint32]$f.bytes.Length); $w.Write([uint32]$offset)
        $offset += $f.bytes.Length
    }
    foreach ($f in $frames) { $w.Write($f.bytes) }
    $w.Flush(); [IO.File]::WriteAllBytes($path, $out.ToArray())
}

$concept = $concepts['brand-pad']
New-Item -ItemType Directory -Force $Out | Out-Null
Write-Svg $concept (Join-Path $Out 'brand-pad.svg')
$icons = @{}
foreach ($size in 256, 128, 96, 64, 48, 40, 32, 24, 20, 16) {
    $icons[$size] = Render-Icon $concept $size
    Save-Png $icons[$size] (Join-Path $Out "brand-pad-$size.png")
}
Contact-Sheet $concept $icons (Join-Path $Out 'contact-sheet.png')

if ($Install) {
    $repo = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
    $sizes = 16, 20, 24, 32, 40, 48, 64, 96, 128, 256
    Write-Ico $concept $sizes (Join-Path $repo 'PureDS4\DS4W.ico')
    Write-Ico $concept $sizes (Join-Path $repo 'PureDS4\Resources\DS4W.ico')
    Save-Png (Render-Icon $concept 1024) (Join-Path $repo 'PureDS4\Resources\AppIcon.png')
    Write-Svg $concept (Join-Path $PSScriptRoot 'brand-pad.svg')
    Write-Output 'Installed DS4W.ico (x2) and AppIcon.png'
}
