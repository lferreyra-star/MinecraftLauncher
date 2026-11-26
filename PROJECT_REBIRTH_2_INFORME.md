# XD Launcher Project Rebirth 2.0 — informe parcial real

Fecha: 21/11/2026

## Esto NO está terminado

Se implementó una nueva identidad visual y una pantalla “Acerca de” completamente rediseñada, pero no se ha completado el rediseño radical de todas las pantallas. No voy a declarar terminado un trabajo que no lo está.

## Causa de que sigas viendo una interfaz antigua

Existe un proceso `MinecraftLauncher.exe` elevado iniciado antes de los cambios. El launcher nuevo sale inmediatamente por el mutex de instancia única y Windows enfoca la ventana antigua. Además, no se pueden reemplazar los archivos de `C:\Program Files\Minecraft Bedrock Launcher` sin permisos de administrador. Para ver la versión nueva hay que cerrar ese proceso y ejecutar `ACTUALIZAR_LAUNCHER_INSTALADO.bat` como administrador.

## Referencias de diseño estudiadas

Se tomaron principios generales de Prism Launcher, Modrinth App, CurseForge, Lunar Client y Badlion Client: bibliotecas densas pero organizadas, navegación jerárquica, tarjetas de contenido con información útil, identidad gaming fuerte y transiciones suaves. No se copió código ni marca de esos proyectos.

## Cambios implementados en esta entrega

- `src/XDLauncher/Views/AboutView.xaml` y `.cs`: nueva vista “Acerca de” con composición completa.
  - Hero de identidad XD con logotipo grande y versión.
  - Estado real de actualizaciones automáticas.
  - Panel de sistema con CPU/GPU/RAM/Windows reales.
  - Tecnologías usadas.
  - Historial de versiones.
  - Créditos y enlaces oficiales.
  - Diagnóstico y licencia.
- `src/XDLauncher/Views/SettingsView.xaml`: el panel antiguo “Acerca de” fue sustituido por `AboutView`.
- `src/XDLauncher/App.xaml.cs`: modo `--capture-about` para verificar la vista real sin depender del proceso elevado.
- `src/XDLauncher/Services/SelfTestService.cs`: prueba [17] de tema global.
- `dist/MinecraftLauncher`: versión nueva publicada.

## Archivos nuevos

- `src/XDLauncher/Views/AboutView.xaml`
- `src/XDLauncher/Views/AboutView.xaml.cs`
- `captures/about-rebirth.png`

## Capturas

- Antes: `captures/sections/08-ajustes-antes.png`
- Después: `captures/settings-ultimate.png`
- Acerca de: `captures/about-rebirth.png`

La diferencia estructural en “Acerca de” es evidente: la tarjeta pequeña flotante fue sustituida por una composición completa a pantalla completa.

## Compilación y pruebas

```bat
dotnet build src/XDLauncher/XDLauncher.csproj -c Debug -p:Platform=x64
dotnet publish src/XDLauncher/XDLauncher.csproj -c Release -r win-x64 --self-contained true -o dist/MinecraftLauncher
MinecraftLauncher.exe --selftest
MinecraftLauncher.exe --capture-about
```

Resultado: 0 errores; autodiagnóstico correcto.

## Funcionalidades preservadas

Firebase, Google Login, sesiones, Java, Bedrock, Android, mods, complementos, descargas, ajustes y diagnóstico siguen conectados a sus servicios originales.

## Pendiente real

- Rediseñar individualmente todas las pantallas restantes, no solo shell, Ajustes y Acerca de.
- Verificar navegación real en el launcher instalado tras actualizar con administrador.
- Comprobar resoluciones 1366x768, 1440x900 y 1920x1080 en el ejecutable instalado.
