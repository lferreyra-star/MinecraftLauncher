Add-Type -AssemblyName System.Drawing

$outDir = Join-Path $PSScriptRoot '..\src\XDLauncher\Assets\backgrounds'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$W = 1920
$H = 1080

function Get-Color([string]$hex, [int]$alpha = 255) {
    $hex = $hex.TrimStart('#')
    $r = [Convert]::ToInt32($hex.Substring(0,2),16)
    $g = [Convert]::ToInt32($hex.Substring(2,2),16)
    $b = [Convert]::ToInt32($hex.Substring(4,2),16)
    return [System.Drawing.Color]::FromArgb($alpha,$r,$g,$b)
}

function Fill-Sky($g, [string]$top, [string]$mid, [string]$bottom) {
    $rect = New-Object System.Drawing.Rectangle(0,0,$W,$H)
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $rect, (Get-Color $top), (Get-Color $bottom), 90)
    $blend = New-Object System.Drawing.Drawing2D.ColorBlend
    $blend.Colors = [System.Drawing.Color[]]@((Get-Color $top), (Get-Color $mid), (Get-Color $bottom))
    $blend.Positions = [float[]]@(0.0, 0.52, 1.0)
    $brush.InterpolationColors = $blend
    $g.FillRectangle($brush, $rect)
    $brush.Dispose()
}

function Add-Glow($g, [int]$x, [int]$y, [int]$radius, [string]$color, [int]$alpha) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddEllipse(($x-$radius), ($y-$radius), ($radius*2), ($radius*2))
    $brush = New-Object System.Drawing.Drawing2D.PathGradientBrush($path)
    $center = Get-Color $color $alpha
    $edge = Get-Color $color 0
    $brush.CenterColor = $center
    $brush.SurroundColors = @($edge)
    $g.FillPath($brush, $path)
    $brush.Dispose(); $path.Dispose()
}

function Add-Stars($g, $rnd, [int]$count, [string]$color, [int]$maxY) {
    for ($i=0; $i -lt $count; $i++) {
        $x = $rnd.Next(0,$W); $y = $rnd.Next(0,$maxY)
        $s = $rnd.NextDouble()*2.2 + 0.5
        $a = $rnd.Next(70,220)
        $brush = New-Object System.Drawing.SolidBrush((Get-Color $color $a))
        $g.FillEllipse($brush, [float]$x, [float]$y, [float]$s, [float]$s)
        $brush.Dispose()
    }
}

function Add-Mountains($g, $rnd, [int]$baseY, [string]$color, [int]$alpha, [int]$peaks, [int]$height) {
    $points = New-Object System.Collections.Generic.List[System.Drawing.Point]
    $points.Add((New-Object System.Drawing.Point(0,$H)))
    $points.Add((New-Object System.Drawing.Point(0,$baseY)))
    for ($i=0; $i -le $peaks; $i++) {
        $x = [int]($i * $W / $peaks)
        $y = $baseY - $rnd.Next(0,$height)
        $points.Add((New-Object System.Drawing.Point($x,$y)))
    }
    $points.Add((New-Object System.Drawing.Point($W,$H)))
    $brush = New-Object System.Drawing.SolidBrush((Get-Color $color $alpha))
    $g.FillPolygon($brush, [System.Drawing.Point[]]$points.ToArray())
    $brush.Dispose()
}

function Add-Trees($g, $rnd, [string]$trunk, [string]$leaf, [int]$count, [int]$minY) {
    for ($i=0; $i -lt $count; $i++) {
        $x = [int]$rnd.Next(-40, $W+40)
        $h = [int]$rnd.Next(180, 520)
        $low = [Math]::Min([int]$minY, $H)
        $high = [int]($H + 120)
        $baseY = [int]$rnd.Next($low, $high)
        $tw = [int]($h*0.08)
        $tb = New-Object System.Drawing.SolidBrush((Get-Color $trunk 235))
        $g.FillRectangle($tb, ($x-[int]($tw/2)), ($baseY-$h), $tw, $h)
        $tb.Dispose()
        $lb = New-Object System.Drawing.SolidBrush((Get-Color $leaf 230))
        for ($j=0; $j -lt 3; $j++) {
            $ty = [int]($baseY - $h + ($j*$h*0.22))
            $tw2 = [int]($h*(0.36 - ($j*0.07)))
            $topY = [int]($ty - ($h*0.20))
            $botY = [int]($ty + ($h*0.12))
            $tri = [System.Drawing.Point[]]@(
                (New-Object System.Drawing.Point($x,$topY)),
                (New-Object System.Drawing.Point([int]($x-($tw2/2)),$botY)),
                (New-Object System.Drawing.Point([int]($x+($tw2/2)),$botY))
            )
            $g.FillPolygon($lb, $tri)
        }
        $lb.Dispose()
    }
}

function Add-Vignette($g) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddRectangle((New-Object System.Drawing.Rectangle(0,0,$W,$H)))
    $brush = New-Object System.Drawing.Drawing2D.PathGradientBrush($path)
    $brush.CenterPoint = New-Object System.Drawing.PointF(($W/2),($H/2))
    $brush.CenterColor = [System.Drawing.Color]::FromArgb(0,0,0,0)
    $brush.SurroundColors = @([System.Drawing.Color]::FromArgb(190,0,0,0))
    $g.FillPath($brush, $path)
    $brush.Dispose(); $path.Dispose()
}

function Add-Ribbons($g, $rnd, [string]$c1, [string]$c2) {
    for ($i=0; $i -lt 7; $i++) {
        $y = 120 + $i*70 + $rnd.Next(-20,20)
        $pen = New-Object System.Drawing.Pen((Get-Color $(if($i%2 -eq 0){$c1}else{$c2}) 55), 38)
        $g.DrawBezier($pen, -100,$y, 500,$y-160, 1300,$y+180, $W+100,$y-80)
        $pen.Dispose()
    }
}

function Add-Portal($g, $x, $y, [string]$outer, [string]$inner) {
    Add-Glow $g $x $y 520 $outer 150
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddEllipse(($x-260),($y-360),520,720)
    $brush = New-Object System.Drawing.Drawing2D.PathGradientBrush($path)
    $brush.CenterColor = Get-Color $inner 235
    $brush.SurroundColors = @((Get-Color $outer 90))
    $g.FillPath($brush,$path)
    $brush.Dispose(); $path.Dispose()
}

function Add-Crystals($g, $rnd, [string]$color, [int]$count) {
    for ($i=0; $i -lt $count; $i++) {
        $x = $rnd.Next(0,$W); $y = $H - $rnd.Next(0,420)
        $h = $rnd.Next(80,260); $w = [int]($h*0.32)
        $tri = @(
            (New-Object System.Drawing.Point($x,($y-$h))),
            (New-Object System.Drawing.Point(($x-$w),$y)),
            (New-Object System.Drawing.Point(($x+$w),$y))
        )
        Add-Glow $g $x ($y-[int]($h/2)) 120 $color 70
        $b = New-Object System.Drawing.SolidBrush((Get-Color $color 165))
        $g.FillPolygon($b,[System.Drawing.Point[]]$tri); $b.Dispose()
    }
}

function Add-City($g, $rnd, [string]$building, [string]$window) {
    $x = 0
    while ($x -lt $W) {
        $bw = $rnd.Next(90,210); $bh = $rnd.Next(220,720)
        $y = $H - $bh
        $b = New-Object System.Drawing.SolidBrush((Get-Color $building 245))
        $g.FillRectangle($b,$x,$y,$bw,$bh); $b.Dispose()
        $wb = New-Object System.Drawing.SolidBrush((Get-Color $window 190))
        for ($wy=$y+20; $wy -lt $H-20; $wy+=38) {
            for ($wx=$x+16; $wx -lt $x+$bw-16; $wx+=30) {
                if ($rnd.NextDouble() -gt 0.45) { $g.FillRectangle($wb,$wx,$wy,10,16) }
            }
        }
        $wb.Dispose()
        $x += $bw + $rnd.Next(6,26)
    }
}

function Add-Islands($g, $rnd, [string]$rock, [string]$grass, [int]$count) {
    for ($i=0; $i -lt $count; $i++) {
        $x = $rnd.Next(120,$W-120); $y = $rnd.Next(240,760)
        $iw = $rnd.Next(180,420); $h = [int]($iw*0.42)
        $pts = @(
            (New-Object System.Drawing.Point([int]($x-($iw/2)),$y)),
            (New-Object System.Drawing.Point([int]($x+($iw/2)),$y)),
            (New-Object System.Drawing.Point([int]($x+($iw/3)),($y+$h))),
            (New-Object System.Drawing.Point($x,[int]($y+($h*1.5)))),
            (New-Object System.Drawing.Point([int]($x-($iw/3)),($y+$h)))
        )
        $b = New-Object System.Drawing.SolidBrush((Get-Color $rock 245))
        $g.FillPolygon($b,[System.Drawing.Point[]]$pts); $b.Dispose()
        $gb = New-Object System.Drawing.SolidBrush((Get-Color $grass 220))
        $g.FillEllipse($gb,[int]($x-($iw/2)),($y-18),$iw,36); $gb.Dispose()
    }
}

function Add-Petals($g, $rnd, [string]$color, [int]$count) {
    for ($i=0; $i -lt $count; $i++) {
        $x=$rnd.Next(0,$W); $y=$rnd.Next(0,$H)
        $b=New-Object System.Drawing.SolidBrush((Get-Color $color $rnd.Next(80,200)))
        $g.FillEllipse($b,$x,$y,$rnd.Next(3,8),$rnd.Next(2,5)); $b.Dispose()
    }
}

function Save-Scene {
    param([string]$Name,[string]$Top,[string]$Mid,[string]$Bottom,[string]$Glow,[string]$Accent,
          [string]$Trunk,[string]$Leaf,[int]$Seed,[string]$Kind)

    $bmp = New-Object System.Drawing.Bitmap($W,$H)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $rnd = New-Object System.Random($Seed)

    Fill-Sky $g $Top $Mid $Bottom
    Add-Glow $g ([int]($W*0.62)) ([int]($H*0.34)) 430 $Glow 150

    switch ($Kind) {
        'aurora' { Add-Stars $g $rnd 260 '#CFF7FF' 620; Add-Ribbons $g $rnd $Glow $Accent }
        'nether' { Add-Stars $g $rnd 90 '#FFB27A' 520; Add-Portal $g ([int]($W*0.55)) ([int]($H*0.55)) '#FF4B2B' '#FFD36A'; Add-Crystals $g $rnd '#FF5A36' 14 }
        'end' { Add-Stars $g $rnd 340 '#E7D8FF' 700; Add-Islands $g $rnd '#221A3C' '#9D7BE8' 7 }
        'cave' { Add-Crystals $g $rnd $Accent 26 }
        'city' { Add-Stars $g $rnd 160 '#B9D7FF' 460; Add-City $g $rnd '#0A1326' '#FFD88A' }
        'snow' { Add-Stars $g $rnd 220 '#FFFFFF' 500; Add-Mountains $g $rnd 720 '#1B2A44' 250 9 380; Add-Mountains $g $rnd 840 '#E8F3FF' 220 11 300 }
        'sunset' { Add-Mountains $g $rnd 760 '#2A2140' 245 10 330; Add-Glow $g ([int]($W*0.68)) ([int]($H*0.62)) 210 '#FFB36B' 220 }
        'magico' { Add-Trees $g $rnd '#0B1E17' '#123D2E' 26 640; Add-Petals $g $rnd $Accent 120 }
        'fantastico' { Add-Islands $g $rnd '#18243F' '#49C5A0' 9; Add-Glow $g ([int]($W*0.3)) ([int]($H*0.5)) 320 $Accent 110 }
        'cherry' { Add-Trees $g $rnd '#2A1626' '#7A3B62' 28 640; Add-Petals $g $rnd '#FFB7D5' 180 }
        default { Add-Mountains $g $rnd 760 '#0E2233' 235 10 320; Add-Trees $g $rnd $Trunk $Leaf 30 700 }
    }

    Add-Vignette $g
    $g.Dispose()
    $path = Join-Path $outDir $Name
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Output "OK $Name"
}

Save-Scene 'bg-bosque.png'      '#06131B' '#0C2A2E' '#040A0C' '#36D39A' '#8DE5AB' '#0A1A18' '#123B2C' 11 'bosque'
Save-Scene 'bg-aurora.png'      '#040A1F' '#0B1A3C' '#050914' '#37E0C8' '#9A7BFF' '#0A1526' '#12324A' 22 'aurora'
Save-Scene 'bg-amanecer.png'    '#241433' '#B35A6E' '#F2A65A' '#FF9E6B' '#FFD9A0' '#221A2C' '#3A2A44' 33 'sunset'
Save-Scene 'bg-cueva.png'       '#04080F' '#0A1424' '#02070C' '#45B8FF' '#7A6CFF' '#0A101A' '#102033' 44 'cave'
Save-Scene 'bg-nether.png'      '#1B0406' '#571014' '#120305' '#FF5A2B' '#FFB24A' '#2A0808' '#5A1512' 55 'nether'
Save-Scene 'bg-end.png'         '#080417' '#1A0F3A' '#050313' '#B08CFF' '#6A55E8' '#140B2A' '#241544' 66 'end'
Save-Scene 'bg-cerezo.png'      '#1B0B22' '#5A2447' '#160A18' '#FF9AC7' '#FFD0E2' '#2A1626' '#7A3B62' 77 'cherry'
Save-Scene 'bg-nevado.png'      '#0C1C33' '#486A8D' '#DCEBFA' '#A9D8FF' '#FFFFFF' '#16243B' '#E8F3FF' 88 'snow'
Save-Scene 'bg-ciudad.png'      '#050A16' '#0C1830' '#02050B' '#4B8DFF' '#FFD88A' '#0A1326' '#12203A' 99 'city'
Save-Scene 'bg-nocturno.png'    '#020610' '#071B32' '#01040A' '#5FC8FF' '#B9D7FF' '#08131F' '#0E2A3E' 101 'bosque'
Save-Scene 'bg-magico.png'      '#04150F' '#0B3A2A' '#020C09' '#45E0A8' '#B6FFD9' '#0B1E17' '#123D2E' 112 'magico'
Save-Scene 'bg-fantastico.png'  '#071426' '#15405A' '#030A14' '#4ED8C0' '#7CE0FF' '#18243F' '#49C5A0' 123 'fantastico'
