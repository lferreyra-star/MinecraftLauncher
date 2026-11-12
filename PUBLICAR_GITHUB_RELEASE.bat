@echo off
title XD Launcher - Publicar en GitHub
setlocal
cd /d "%~dp0"

echo ==================================================
echo   XD Launcher - Publicar release en GitHub
echo ==================================================
echo.
echo  - Sube el numero de version (parche) automaticamente.
echo  - Compila, genera el instalador y lo sube a GitHub Releases.
echo  - Marca la version con un tag (v1.3.1, v1.3.2, ...).
echo.
echo  Puedes arrastrar este flujo en modo prueba con: PUBLICAR_GITHUB_RELEASE.bat -DryRun
echo.

"C:\Program Files\dotnet\dotnet" --version >nul 2>&1
if errorlevel 1 (
    echo ERROR: .NET SDK no encontrado.
    pause
    exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -File "tools\release.ps1" %*
pause
