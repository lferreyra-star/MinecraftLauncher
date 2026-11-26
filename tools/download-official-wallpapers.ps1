Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.Drawing

$base = 'https://www.minecraft.net/content/dam/minecraftnet/games/minecraft/software/'
$tmp = Join-Path $env:TEMP 'xd-eclipse-wallpapers'
$outDir = 'C:\Users\EVIL\Documents\xD\src\XDLauncher\Assets\backgrounds'
New-Item -ItemType Directory -Force -Path $tmp | Out-Null
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$jobs = @(
    @{ Id='bosque';      Zip='wallpaper_minecraft_wild_update.zip';                 Out='bg-bosque.png' },
    @{ Id='aurora';      Zip='wallpapers_chase_the_skies_update.zip';               Out='bg-aurora.png' },
    @{ Id='amanecer';    Zip='wallpapers_spring_to_life_update.zip';                Out='bg-amanecer.png' },
    @{ Id='cueva';       Zip='wallpapers_minecraft_caves_cliffs(part2).zip';        Out='bg-cueva.png' },
    @{ Id='nether';      Zip='wallpapers_minecraft_nether_update.zip';              Out='bg-nether.png' },
    @{ Id='end';         Zip='wallpapers_minecraft_15th-anniversary.zip';           Out='bg-end.png' },
    @{ Id='cerezo';      Zip='wallpapers_the_garden_awakens_update.zip';            Out='bg-cerezo.png' },
    @{ Id='nevado';      Zip='wallpapers_minecraft_caves_cliffs(part1).zip';        Out='bg-nevado.png' },
    @{ Id='ciudad';      Zip='wallpapers_minecraft_village_pillage.zip';            Out='bg-ciudad.png' },
    @{ Id='nocturno';    Zip='wallpapers_minecraft_winter_celebration.zip';         Out='bg-nocturno.png' },
    @{ Id='magico';      Zip='wallpapers_minecraft_world_color.zip';                Out='bg-magico.png' },
    @{ Id='fantastico';  Zip='wallpapers_minecraft_island.zip';                     Out='bg-fantastico.png' }
)

function Extract-Zip($zipPath, $dest) {
    if (Test-Path $dest) { Remove-Item $dest -Recurse -Force }
    [System.IO.Compression.ZipFile]::ExtractToDirectory($zipPath, $dest)
}

foreach ($job in $jobs) {
    $id = $job.Id
    Write-Host "[$id] descargando..."
    $topZip = Join-Path $tmp "$id-top.zip"
    $topDir = Join-Path $tmp "$id-top"
    $workDir = Join-Path $tmp $id
    try {
        Invoke-WebRequest -Uri ($base + $job.Zip) -OutFile $topZip -UseBasicParsing -ErrorAction Stop
        Extract-Zip $topZip $topDir
        if (Test-Path $workDir) { Remove-Item $workDir -Recurse -Force }
        New-Item -ItemType Directory -Force -Path $workDir | Out-Null

        Get-ChildItem $topDir -Filter *.zip -Recurse |
            Where-Object { $_.FullName -notmatch '__MACOSX' } |
            ForEach-Object {
                $name = [System.IO.Path]::GetFileNameWithoutExtension($_.Name)
                $dest = Join-Path $workDir $name
                [System.IO.Compression.ZipFile]::ExtractToDirectory($_.FullName, $dest)
            }

        $direct = Join-Path $workDir 'direct'
        New-Item -ItemType Directory -Force -Path $direct | Out-Null
        Get-ChildItem $topDir -Recurse -Include *.png,*.jpg,*.jpeg |
            Where-Object { $_.FullName -notmatch '__MACOSX' } |
            Copy-Item -Destination $direct -Force

        $candidate = Get-ChildItem $workDir -Recurse -Include *.png,*.jpg,*.jpeg |
            Where-Object { $_.FullName -notmatch '__MACOSX' } |
            Sort-Object Length -Descending | Select-Object -First 1

        if (-not $candidate) { throw 'No se encontró imagen dentro del paquete' }

        $source = [System.Drawing.Image]::FromFile($candidate.FullName)
        $target = New-Object System.Drawing.Bitmap(1920,1080)
        $g = [System.Drawing.Graphics]::FromImage($target)
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

        $targetAspect = 1920 / 1080
        $sourceAspect = $source.Width / $source.Height
        if ($sourceAspect -gt $targetAspect) {
            $cropW = [int]($source.Height * $targetAspect)
            $cropX = [int](($source.Width - $cropW) / 2)
            $srcRect = New-Object System.Drawing.Rectangle($cropX, 0, $cropW, $source.Height)
        } else {
            $cropH = [int]($source.Width / $targetAspect)
            $cropY = [int](($source.Height - $cropH) / 2)
            $srcRect = New-Object System.Drawing.Rectangle(0, $cropY, $source.Width, $cropH)
        }
        $destRect = New-Object System.Drawing.Rectangle(0,0,1920,1080)
        $g.DrawImage($source, $destRect, $srcRect, [System.Drawing.GraphicsUnit]::Pixel)
        $g.Dispose()

        $destFile = Join-Path $outDir $job.Out
        $target.Save($destFile, [System.Drawing.Imaging.ImageFormat]::Png)
        $target.Dispose()
        $source.Dispose()
        Write-Host "[$id] OK $($job.Out) desde $($candidate.Name)"
    } catch {
        Write-Host "[$id] ERROR $($_.Exception.Message)"
    }
}

Write-Host 'Listo.'
