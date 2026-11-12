param(
    [switch]$DryRun,
    [switch]$UpdateInstalled,
    [switch]$NoPrompt
)

# ============================================================
#  XD Launcher - Publicar version en GitHub Releases
#  1) Sube el parche de version (1.3.0 -> 1.3.1)
#  2) Compila self-contained -> dist\MinecraftLauncher
#  3) Genera el instalador con Inno Setup -> dist\MinecraftLauncher_Setup.exe
#  4) Commit + tag + push
#  5) Crea el release en GitHub y sube el instalador
# ============================================================

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$csprojPath = Join-Path $root 'src\XDLauncher\XDLauncher.csproj'
$issPath    = Join-Path $root 'installer\MinecraftBedrockLauncher.iss'
$distDir    = Join-Path $root 'dist\MinecraftLauncher'
$setupExe   = Join-Path $root 'dist\MinecraftLauncher_Setup.exe'
$repo       = 'lferreyra-star/MinecraftLauncher'
$branch     = 'master'

function Read-TextSmart([string]$path) {
    $bytes = [System.IO.File]::ReadAllBytes($path)
    $hasBom = ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)
    if ($hasBom) {
        return @{ Text = [System.Text.Encoding]::UTF8.GetString($bytes, 3, $bytes.Length - 3); Bom = $true; Enc = 'utf8' }
    }
    $hasNonAscii = $false
    foreach ($b in $bytes) { if ($b -gt 127) { $hasNonAscii = $true; break } }
    if ($hasNonAscii) {
        return @{ Text = [System.Text.Encoding]::GetEncoding(1252).GetString($bytes); Bom = $false; Enc = 'ansi' }
    }
    return @{ Text = [System.Text.Encoding]::UTF8.GetString($bytes); Bom = $false; Enc = 'utf8' }
}

function Write-TextSmart([string]$path, [hashtable]$file) {
    if ($file.Enc -eq 'ansi') {
        [System.IO.File]::WriteAllText($path, $file.Text, [System.Text.Encoding]::GetEncoding(1252))
    }
    elseif ($file.Bom) {
        $utf8Bom = New-Object System.Text.UTF8Encoding $true
        [System.IO.File]::WriteAllText($path, $file.Text, $utf8Bom)
    }
    else {
        $utf8 = New-Object System.Text.UTF8Encoding $false
        [System.IO.File]::WriteAllText($path, $file.Text, $utf8)
    }
}

# ------------------------------------------------------------
# 1) Version actual + bump de parche
# ------------------------------------------------------------
$csproj = Read-TextSmart $csprojPath
$version = [regex]::Match($csproj.Text, '<Version>([^<]+)</Version>').Groups[1].Value
if ([string]::IsNullOrWhiteSpace($version)) { throw "No se encontro <Version> en el csproj." }
$parts = $version.Split('.')
$newVersion = '{0}.{1}.{2}' -f $parts[0], $parts[1], ([int]$parts[2] + 1)
$tag = "v$newVersion"
Write-Host ""
Write-Host "Version: $version -> $newVersion ($tag)"

if (-not $DryRun) {
    $csproj.Text = $csproj.Text -replace '<Version>[^<]+</Version>', "<Version>$newVersion</Version>"
    $csproj.Text = $csproj.Text -replace '<FileVersion>[^<]+</FileVersion>', "<FileVersion>$newVersion.0</FileVersion>"
    Write-TextSmart $csprojPath $csproj

    $iss = Read-TextSmart $issPath
    $iss.Text = $iss.Text -replace '#define AppVersion "[^"]+"', "#define AppVersion `"$newVersion`""
    $iss.Text = $iss.Text -replace '(?m)^VersionInfoVersion=.+$', "VersionInfoVersion=$newVersion"
    Write-TextSmart $issPath $iss
    Write-Host "csproj e .iss actualizados."
}
else {
    Write-Host "[DRY RUN] No se modifican csproj ni .iss."
}

# ------------------------------------------------------------
# 2) Compilar self-contained
#    (EAP=Continue: el stderr de comandos nativos no debe abortar el script;
#     los fallos reales se detectan con $LASTEXITCODE y throws explícitos)
# ------------------------------------------------------------
$ErrorActionPreference = 'Continue'
Write-Host ""
Write-Host "Limpiando obj (evita artefactos generados con nombres de ensamblado antiguos)..."
Remove-Item (Join-Path $root 'src\XDLauncher\obj') -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "Compilando Release self-contained (esto puede tardar un rato)..."
& 'C:\Program Files\dotnet\dotnet.exe' publish $csprojPath -c Release -r win-x64 --self-contained true -o $distDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish fallo (codigo $LASTEXITCODE). No se publico nada." }
Write-Host "Compilacion OK."

# ------------------------------------------------------------
# 3) Instalador con Inno Setup
# ------------------------------------------------------------
$iscc = Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'
if (-not (Test-Path $iscc)) { throw "No se encontro ISCC.exe en $iscc" }
Write-Host "Generando instalador con Inno Setup..."
& $iscc /Qp (Join-Path $root 'installer\MinecraftBedrockLauncher.iss') | Out-Null
if ($LASTEXITCODE -ne 0) { throw "ISCC fallo (codigo $LASTEXITCODE)." }
if (-not (Test-Path $setupExe)) { throw "No se genero $setupExe" }
$setupSizeMB = [math]::Round((Get-Item $setupExe).Length / 1MB, 1)
Write-Host "Instalador generado: $setupSizeMB MB"

# ------------------------------------------------------------
# 4) SHA-256
# ------------------------------------------------------------
$sha = (Get-FileHash $setupExe -Algorithm SHA256).Hash.ToLower()
Write-Host "SHA-256: $sha"

if ($DryRun) {
    Write-Host ""
    Write-Host "[DRY RUN] Todo listo. Si ejecutas sin -DryRun se haran estos pasos extra:"
    Write-Host "  - Bump de version a $newVersion (csproj + .iss)"
    Write-Host "  - git commit + tag $tag + push a $branch"
    Write-Host "  - Crear release $tag en GitHub y subir el instalador"
    exit 0
}

# ------------------------------------------------------------
# 5) Commit + tag + push
#    (sin 2>&1: el stderr de git en PowerShell no es un error real)
# ------------------------------------------------------------
Write-Host ""
Write-Host "Haciendo commit y push..."

# Si quedó un index.lock huerfano (de una ejecución interrumpida), limpiarlo
$lockFile = Join-Path $root '.git\index.lock'
if (Test-Path $lockFile) {
    $gitRunning = Get-Process git -ErrorAction SilentlyContinue
    if ($gitRunning) { throw "Hay un proceso git activo; cierralo y reintenta." }
    Remove-Item $lockFile -Force
    Write-Warning "Se elimino un index.lock huerfano."
}

& git add -A
& git commit -m "Release $tag"
& git tag -f $tag
& git push origin $branch
if ($LASTEXITCODE -ne 0) { throw "git push fallo. El release NO se creo; revisa la conexion." }
& git push origin $tag
if ($LASTEXITCODE -ne 0) { throw "git push del tag fallo." }

# ------------------------------------------------------------
# 6) Release en GitHub (token del credential manager, nunca se muestra)
# ------------------------------------------------------------
Write-Host "Creando release en GitHub..."
$fillInput = "protocol=https`nhost=github.com`n`n"
$prevEap = $ErrorActionPreference
$ErrorActionPreference = 'Continue'
$credOutput = $fillInput | & git credential fill 2>$null
$ErrorActionPreference = $prevEap
$tokenLine = $credOutput | Where-Object { $_ -like 'password=*' } | Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($tokenLine)) { throw "No hay credenciales de GitHub en el credential manager." }
$token = $tokenLine.Substring('password='.Length)

$headers = @{
    Authorization = "token $token"
    'User-Agent'  = 'xd-launcher-release-script'
}

$releaseNotes = @"
Nueva version del launcher.

**Instalador:** ``MinecraftLauncher_Setup.exe`` ($setupSizeMB MB)
**SHA-256:** ``$sha``
**Fecha:** $((Get-Date).ToString('yyyy-MM-dd HH:mm'))
"@

$releaseBody = @{
    tag_name         = $tag
    target_commitish = $branch
    name             = "Minecraft Launcher $tag"
    body             = $releaseNotes
    draft            = $false
    prerelease       = $false
} | ConvertTo-Json

$release = Invoke-RestMethod -Method Post -Uri "https://api.github.com/repos/$repo/releases" -Headers $headers -Body $releaseBody -ContentType 'application/json; charset=utf-8'
Write-Host "Release creado: $($release.html_url)"

Write-Host "Subiendo instalador ($setupSizeMB MB)..."
$uploadUrl = ($release.upload_url -replace '\{\?.*$', '') + "?name=MinecraftLauncher_Setup.exe"
$assetHeaders = @{
    Authorization = "token $token"
    'User-Agent'  = 'xd-launcher-release-script'
    'Content-Type' = 'application/octet-stream'
}
$asset = Invoke-RestMethod -Method Post -Uri $uploadUrl -Headers $assetHeaders -InFile $setupExe -TimeoutSec 900
Write-Host "Asset subido: $($asset.browser_download_url)"

# ------------------------------------------------------------
# 7) (Opcional) Actualizar el launcher instalado
# ------------------------------------------------------------
if ($UpdateInstalled) {
    $installDir = 'C:\Program Files\Minecraft Bedrock Launcher'
    if (-not (Test-Path $installDir)) { $installDir = 'C:\Program Files\XD Launcher' }
    if (Test-Path $installDir) {
        & taskkill /IM MinecraftLauncher.exe /F 2>$null | Out-Null
        & taskkill /IM MinecraftBedrockLauncher.exe /F 2>$null | Out-Null
        try {
            robocopy $distDir $installDir /E /NFL /NDL /NJH /NP /MT | Out-Null
            if ($LASTEXITCODE -ge 8) { throw "robocopy fallo (codigo $LASTEXITCODE)" }
            Get-ChildItem $installDir -Filter 'MinecraftBedrockLauncher.*' -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
            Write-Host "Launcher instalado actualizado en $installDir"
        }
        catch {
            Write-Warning "No se pudo actualizar el launcher instalado (¿permisos de admin?). Ejecuta ACTUALIZAR_LAUNCHER_INSTALADO.bat."
        }
    }
    else {
        Write-Warning "No se encontro el directorio de instalacion."
    }
}
elseif (-not $NoPrompt) {
    $answer = Read-Host "Actualizar tambien el launcher instalado en Program Files? (S/N)"
    if ($answer -match '^[sS]') {
        & powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath 'powershell' -Verb RunAs -ArgumentList '-NoProfile','-ExecutionPolicy','Bypass','-File','`"$PSCommandPath`"','-NoPrompt','-UpdateInstalled'"
    }
}

Write-Host ""
Write-Host "===================================================="
Write-Host "Publicacion completada: $tag"
Write-Host "Release: https://github.com/$repo/releases/tag/$tag"
Write-Host "SHA-256: $sha"
Write-Host "===================================================="
