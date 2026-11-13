param(
    [string]$Host_ = "",
    [string]$User = "",
    [string]$Password = "",
    [string]$RemoteDir = "htdocs",
    [switch]$DryRun
)

# ---------------------------------------------------------------------------
#  Sube la web de XD Launcher (carpeta website/) a InfinityFree por FTP.
#  InfinityFree no permite FTP anónimo: hay que usar las credenciales del
#  panel de control (usuario y contraseña de FTP, no las de MySQL).
#
#  Uso:
#    powershell -ExecutionPolicy Bypass -File tools/subir-web.ps1 -Host_ ftpupload.net -User usuario -Password clave
#    powershell -ExecutionPolicy Bypass -File tools/subir-web.ps1 -DryRun   (solo lista lo que se subiría)
# ---------------------------------------------------------------------------

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$webDir = Join-Path $root 'website'

if (-not (Test-Path $webDir)) { throw "No se encuentra la carpeta website/ en $root" }

Write-Host ""
Write-Host "=============================================="
Write-Host "  Subir la web de XD Launcher a InfinityFree"
Write-Host "=============================================="
Write-Host ""

if ($DryRun) {
    Write-Host "[DRY RUN] Archivos que se subirían a /$RemoteDir/:"
    Get-ChildItem -Path $webDir -Recurse -File | ForEach-Object {
        $rel = $_.FullName.Substring($webDir.Length).TrimStart('\') -replace '\\', '/'
        Write-Host ("  {0,-32} {1,8:N1} KB" -f $rel, ($_.Length / 1KB))
    }
    Write-Host ""
    Write-Host "Sube también, si aún no lo has hecho:"
    Write-Host "  - firestore.rules  (reglas de seguridad de Firestore: se edita en la consola)"
    Write-Host ""
    exit 0
}

if ([string]::IsNullOrWhiteSpace($Host_) -or [string]::IsNullOrWhiteSpace($User) -or [string]::IsNullOrWhiteSpace($Password)) {
    throw "Faltan datos. Uso: -Host_ <servidor ftp> -User <usuario> -Password <clave> (verlos en InfinityFree > FTP/MySQL)"
}

$cred = New-Object System.Net.NetworkCredential($User, $Password)
$ftp = New-Object System.Net.FtpWebRequest
$base = "ftp://$Host_"

function Ensure-RemoteDir([string]$path) {
    $parts = $path.Split('/') | Where-Object { $_ -ne "" }
    $current = ""
    foreach ($part in $parts) {
        $current += "/$part"
        try {
            $req = [System.Net.FtpWebRequest]::Create("$base$current/")
            $req.Method = [System.Net.WebRequestMethods+Ftp]::MakeDirectory
            $req.Credentials = $cred
            $req.UsePassive = $true
            $resp = $req.GetResponse()
            $resp.Close()
            Write-Host "  + carpeta $current"
        }
        catch {
            Write-Host "  = carpeta $current (ya existe)"
        }
    }
}

Write-Host "Conectando a $base ..."
Ensure-RemoteDir $RemoteDir

$files = Get-ChildItem -Path $webDir -Recurse -File
$ok = 0
$fail = 0

foreach ($file in $files) {
    $rel = $file.FullName.Substring($webDir.Length).TrimStart('\') -replace '\\', '/'
    $target = "$base/$RemoteDir/$rel"

    $dir = [System.IO.Path]::GetDirectoryName($target).Replace('\', '/')
    if ($dir -ne "$base/$RemoteDir") { Ensure-RemoteDir ($dir.Substring($base.Length + 1).TrimStart('/')) }

    try {
        $req = [System.Net.FtpWebRequest]::Create($target)
        $req.Method = [System.Net.WebRequestMethods+Ftp]::UploadFile
        $req.Credentials = $cred
        $req.UsePassive = $true
        $req.UseBinary = $true
        $req.ContentType = "application/octet-stream"

        $stream = $file.OpenRead()
        $req.ContentLength = $stream.Length
        $out = $req.GetRequestStream()
        $stream.CopyTo($out)
        $out.Close()
        $stream.Close()

        $resp = $req.GetResponse()
        $resp.Close()
        $ok++
        Write-Host "  + $rel"
    }
    catch {
        $fail++
        Write-Warning "  ! $rel -> $($_.Exception.Message)"
    }
}

Write-Host ""
Write-Host "Subidos: $ok · fallidos: $fail"
if ($fail -gt 0) { Write-Warning "Revisa los archivos fallidos y vuelve a ejecutar." }
Write-Host ""
Write-Host "Recuerda: en Firebase Console > Authentication > Settings > Authorized domains"
Write-Host "debe estar xdluncher.gt.tc, y las reglas de firestore.rules deben publicarse."
