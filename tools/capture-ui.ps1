Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$exe = "C:\Users\EVIL\Documents\xD\src\XDLauncher\bin\x64\Debug\net10.0-windows10.0.19041.0\win-x64\MinecraftLauncher.exe"
$out = "C:\Users\EVIL\Documents\xD\captures\home.png"

$proc = Start-Process -FilePath $exe -PassThru
Start-Sleep -Seconds 10

$bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$bitmap = New-Object System.Drawing.Bitmap($bounds.Width, $bounds.Height)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size)
$bitmap.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
$graphics.Dispose()
$bitmap.Dispose()

if (-not $proc.HasExited) {
    $proc.Kill()
}

Write-Output $out
