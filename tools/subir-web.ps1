<#
  Despliega la web de XD Launcher en un hosting InfinityFree por FTP.

  Uso:
    powershell -ExecutionPolicy Bypass -File tools/subir-web.ps1 -Host_ ftpupload.net -User if0_XXXXXXX -Password CLAVE
    powershell -ExecutionPolicy Bypass -File tools/subir-web.ps1 -DryRun

  Las credenciales son las de la cuenta que administra el dominio (InfinityFree >
  FTP / MySQL). No se guardan en ningun sitio: se piden por parametro o de forma
  interactiva. La carpeta de destino debe ser la raiz publica del dominio
  (normalmente htdocs).
#>
param(
    [string]$Host_ = "",
    [string]$User = "",
    [string]$Password = "",
    [string]$RemoteDir = "htdocs",
    [switch]$DryRun,
    [switch]$KeepOld
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$webDir = Join-Path $root 'website'

if (-not (Test-Path $webDir)) { throw "No se encuentra la carpeta website/ en $root" }

Write-Host ""
Write-Host "=================================================="
Write-Host "  Desplegar la web de XD Launcher en InfinityFree"
Write-Host "=================================================="
Write-Host ""

if ($DryRun) {
    Write-Host "[DRY RUN] Ficheros que se subirian a /$RemoteDir/:"
    Get-ChildItem -Path $webDir -Recurse -File | ForEach-Object {
        $rel = $_.FullName.Substring($webDir.Length).TrimStart('\') -replace '\\', '/'
        Write-Host ("  {0,-34} {1,8:N1} KB" -f $rel, ($_.Length / 1KB))
    }
    Write-Host ""
    Write-Host "En InfinityFree: FTP > MySQL > crea la BD si vas a usar Firestore desde PHP."
    Write-Host "Publica tambien las reglas: website/firestore.rules (Firebase Console > Firestore > Rules)."
    Write-Host ""
    exit 0
}

if ([string]::IsNullOrWhiteSpace($Host_)) {
    $Host_ = Read-Host "Servidor FTP (por defecto ftpupload.net)"
    if ([string]::IsNullOrWhiteSpace($Host_)) { $Host_ = 'ftpupload.net' }
}
if ([string]::IsNullOrWhiteSpace($User)) { $User = Read-Host "Usuario FTP (ej. if0_12345678)" }
if ([string]::IsNullOrWhiteSpace($Password)) {
    $secure = Read-Host "Contrasena FTP" -AsSecureString
    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    $Password = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
}
if ([string]::IsNullOrWhiteSpace($User) -or [string]::IsNullOrWhiteSpace($Password)) {
    throw "Faltan las credenciales de FTP."
}

$cred = New-Object System.Net.NetworkCredential($User, $Password)
$base = "ftp://$Host_"

function Get-FtpListing([string]$path) {
    try {
        $req = [System.Net.FtpWebRequest]::Create("$base$path")
        $req.Method = [System.Net.WebRequestMethods+Ftp]::ListDirectoryDetails
        $req.Credentials = $cred
        $req.UsePassive = $true
        $resp = $req.GetResponse()
        $reader = New-Object System.IO.StreamReader($resp.GetResponseStream())
        $text = $reader.ReadToEnd()
        $reader.Close(); $resp.Close()
        return $text
    } catch {
        return ""
    }
}

function Ensure-RemoteDir([string]$path) {
    $parts = $path.Split('/') | Where-Object { $_ -ne "" }
    $current = ""
    foreach ($part in $parts) {
        $current += "/$part"
        if ((Get-FtpListing "$current/") -ne "") { continue }
        try {
            $req = [System.Net.FtpWebRequest]::Create("$base$current/")
            $req.Method = [System.Net.WebRequestMethods+Ftp]::MakeDirectory
            $req.Credentials = $cred
            $req.UsePassive = $true
            $resp = $req.GetResponse(); $resp.Close()
            Write-Host "  + carpeta $current"
        } catch {
            Write-Host "  = carpeta $current (ya existe)"
        }
    }
}

Write-Host "Conectando a $base ..."
Ensure-RemoteDir $RemoteDir

$files = Get-ChildItem -Path $webDir -Recurse -File | Where-Object { $_.Name -notlike "_*" }
$ok = 0; $fail = 0

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
        $out.Close(); $stream.Close()
        $resp = $req.GetResponse(); $resp.Close()
        $ok++
        Write-Host "  + $rel"
    } catch {
        $fail++
        Write-Warning "  ! $rel -> $($_.Exception.Message)"
    }
}

Write-Host ""
Write-Host "Subidos: $ok  |  fallidos: $fail"
if ($fail -gt 0) { Write-Warning "Revisa los ficheros fallidos y repite la ejecucion." }

Write-Host ""
Write-Host "Comprobaciones finales en Firebase Console:"
Write-Host "  1. Authentication > Sign-in method > Google  -> habilitado"
Write-Host "  2. Authentication > Sign-in method > Email/Password -> habilitado (para correo)"
Write-Host "  3. Authentication > Settings > Authorized domains -> xdluncher.gt.tc"
Write-Host "  4. Firestore Database > Rules -> pega website/firestore.rules y publica"
Write-Host "  5. Borra la cache del navegador (Ctrl+F5) para tomar el JS nuevo"
