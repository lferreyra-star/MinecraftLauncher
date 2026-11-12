@echo off
title XD Launcher - Actualizar instalado
setlocal
cd /d "%~dp0"

rem Requiere admin para escribir en Program Files
>nul 2>&1 net session || (
    echo Pidiendo permisos de administrador...
    powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

set "INSTALLDIR=C:\Program Files\Minecraft Bedrock Launcher"
if not exist "%INSTALLDIR%" set "INSTALLDIR=C:\Program Files\XD Launcher"

rem No sobrescribir el EXE mientras el launcher esta corriendo
taskkill /IM MinecraftLauncher.exe /F >nul 2>&1
taskkill /IM MinecraftBedrockLauncher.exe /F >nul 2>&1

echo Compilando Release self-contained...
"C:\Program Files\dotnet\dotnet" publish "src\XDLauncher\XDLauncher.csproj" -c Release -r win-x64 --self-contained true -o "dist\MinecraftLauncher"
if errorlevel 1 (
    echo.
    echo ERROR: la compilacion fallo. No se toco el launcher instalado.
    pause
    exit /b 1
)

echo Copiando a %INSTALLDIR% ...
robocopy "dist\MinecraftLauncher" "%INSTALLDIR%" /E /NFL /NDL /NJH /NP /MT >nul
if errorlevel 8 (
    echo.
    echo ERROR: no se pudo copiar al launcher instalado.
    pause
    exit /b 1
)

rem Limpieza de archivos con el nombre antiguo, si quedaran
if exist "%INSTALLDIR%\MinecraftBedrockLauncher.exe" del /q "%INSTALLDIR%\MinecraftBedrockLauncher.*" >nul 2>&1

echo.
echo Listo: tu launcher instalado ahora tiene la ultima version del codigo.
echo Abriendo el launcher...
start "" "%INSTALLDIR%\MinecraftLauncher.exe"
exit /b 0
