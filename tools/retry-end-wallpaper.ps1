Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.Drawing
$base = 'https://www.minecraft.net/content/dam/minecraftnet/games/minecraft/software/'
$tmp = Join-Path $env:TEMP 'xd-eclipse-wallpapers'
$destFile = 'C:\Users\EVIL\Documents\xD\src\XDLauncher\Assets\backgrounds\bg-end.png'
$candidates = @(
    'wallpapers_minecraft_bedrock_edition.zip',
    'wallpapers_minecraft_mineshaft.zip',
    'wallpapers_minecraft_ocean_monument_.zip',
    'wallpapers_marketplace_frozen_planet.zip',
    'wallpapers_chaos_cubed_drop.zip'
)
foreach ($name in $candidates) {
    $zip = Join-Path $tmp 'end-try.zip'
    $top = Join-Path $tmp 'end-try-top'
    $work = Join-Path $tmp 'end-try-work'
    Write-Host "Probando $name"
    try {
        if (Test-Path $zip) { Remove-Item $zip -Force }
        Invoke-WebRequest -Uri ($base + $name) -OutFile $zip -UseBasicParsing -ErrorAction Stop
        if (Test-Path $top) { Remove-Item $top -Recurse -Force }
        [IO.Compression.ZipFile]::ExtractToDirectory($zip, $top)
        if (Test-Path $work) { Remove-Item $work -Recurse -Force }
        New-Item -ItemType Directory -Force -Path $work | Out-Null

        $nested = Get-ChildItem $top -Filter *.zip -Recurse | Where-Object { $_.FullName -notmatch '__MACOSX' }
        if ($nested) {
            foreach ($n in $nested) {
                $d = Join-Path $work ([IO.Path]::GetFileNameWithoutExtension($n.Name))
                [IO.Compression.ZipFile]::ExtractToDirectory($n.FullName, $d)
            }
        } else {
            Copy-Item (Join-Path $top '*') $work -Recurse -Force
        }

        $c = Get-ChildItem $work -Recurse -Include *.png,*.jpg,*.jpeg |
            Where-Object { $_.FullName -notmatch '__MACOSX' } |
            Sort-Object Length -Descending | Select-Object -First 1
        if (-not $c) { throw 'Sin imágenes' }

        $src = [Drawing.Image]::FromFile($c.FullName)
        $bmp = New-Object Drawing.Bitmap 1920,1080
        $g = [Drawing.Graphics]::FromImage($bmp)
        $g.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.DrawImage($src, (New-Object Drawing.Rectangle 0,0,1920,1080))
        $g.Dispose()
        $bmp.Save($destFile, [Drawing.Imaging.ImageFormat]::Png)
        $bmp.Dispose(); $src.Dispose()
        Write-Host "OK $name -> $($c.Name)"
        break
    } catch {
        Write-Host "Fallo $name : $($_.Exception.Message)"
    }
}
