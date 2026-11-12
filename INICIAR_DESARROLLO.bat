@echo off
title XD Launcher - Desarrollo con Hot Reload
setlocal
cd /d "%~dp0"

echo ==================================================
echo   XD Launcher - Modo Desarrollo (Hot Reload)
echo ==================================================
echo.
echo  - Ejecuta el launcher desde el codigo fuente (config Development).
echo  - Cambios en .cs : Hot Reload en vivo, se aplican al guardar.
echo  - Cambios en .xaml : recompila y reinicia el launcher automaticamente.
echo  - Cambios grandes de C# : reinicio automatico, sin preguntar.
echo  - Los errores de compilacion se muestran aqui; corrige y guarda de nuevo.
echo  - Ctrl+R en esta ventana : reiniciar el launcher a mano si quieres.
echo  - No se toca tu instalacion ni el EXE publicado en dist\.
echo  - Cierra el launcher o esta ventana para terminar.
echo.

rem Cierra instancias anteriores del launcher para evitar el bloqueo de instancia unica
taskkill /IM MinecraftLauncher.exe /F >nul 2>&1

rem Reinicia automaticamente cuando Hot Reload no puede aplicar el cambio
set DOTNET_WATCH_RESTART_ON_RUDE_EDIT=1
rem Consola legible (sin emojis)
set DOTNET_WATCH_SUPPRESS_EMOJIS=1

"C:\Program Files\dotnet\dotnet" watch run --project "src\XDLauncher\XDLauncher.csproj" -c Development

echo.
echo El launcher se cerro.
pause
