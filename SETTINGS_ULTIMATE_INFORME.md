# XD Launcher Settings Ultimate AAA+ — informe real

Fecha: 21/11/2026

## Auditoría previa

- Interfaz real: WPF/C#, punto de entrada `Program.cs`, recursos en `App.xaml`.
- El launcher instalado se copia desde `dist/MinecraftLauncher`; la copia instalada no se pudo actualizar porque el proceso actual no tiene permisos sobre un proceso elevado existente.
- Causa detectada durante la verificación: un proceso `MinecraftLauncher.exe` elevado permanecía abierto y el launcher nuevo salía por el mutex de instancia única, así que las capturas iniciales mostraban la pantalla antigua.
- Se añadió `--capture-settings` para renderizar la vista real fuera de la instancia elevada y verificar el diseño.

## Archivos modificados

- `src/XDLauncher/Views/SettingsView.xaml`: reconstrucción total del centro de ajustes.
  - Encabezado cinematográfico.
  - Navegación secundaria con 14 categorías.
  - Paneles por categoría con visibilidad controlada por `SelectedCategory`.
  - Tarjetas, switches, sliders y selectores con estilos nuevos.
- `src/XDLauncher/ViewModels/SettingsViewModel.cs`: categorías, selección y comandos de carpetas.
- `src/XDLauncher/ViewModels/SettingsState.cs`: nuevas propiedades reales para acento personalizado, canal preferido y configuración Java.
- `src/XDLauncher/Services/SettingsService.cs`: persistencia de `CustomAccentHex`.
- `src/XDLauncher/Services/ToastHub.cs` (`ThemeManager`): nuevas paletas, color personalizado y corrección del reemplazo del diccionario de tema para no borrar `Rebirth.xaml`/`UltimateSettings.xaml`.
- `src/XDLauncher/App.xaml`: carga de `UltimateSettings.xaml`.
- `src/XDLauncher/Services/SelfTestService.cs`: prueba [17] de cambio global de tema.
- `src/XDLauncher/App.xaml.cs`: modo `--capture-settings`.

## Archivos nuevos

- `src/XDLauncher/Themes/UltimateSettings.xaml`: sistema visual del centro de ajustes.
- `captures/sections/08-ajustes-antes.png`: captura antes del rediseño.
- `captures/settings-ultimate.png`: captura después del rediseño.

## Controles conectados a funciones reales

- Tema base claro/oscuro, acentos y color personalizado: `ThemeManager.Apply` + persistencia.
- Fondos e importación local: `SettingsState.SelectedBackground` y `AddBackgroundCommand`.
- Escala e interfaz: `UiScale`, `WindowSizeLabel`, `ShowNews`.
- Animaciones y ambiente: `AnimationsEnabled`, `AmbientEffectsEnabled`.
- Cuenta: sesión XD, perfil local, Microsoft Client ID y restauración automática.
- Java: nombre, memoria, ruta de Java y directorio del juego persistidos.
- Sistema: inicio con Windows, actualizaciones, carpetas, caché y reset.
- Rendimiento: CPU/GPU/RAM/Windows reales de `SystemSummary`, memoria del launcher, gráficos y administrador de tareas.
- Diagnóstico: copiar diagnóstico y abrir registros.
- Navegación a descargas desde Ajustes.

## Correcciones visuales

- Texto de selección de tema con contraseño insuficiente corregido.
- Claves duplicadas `UltimatePanel` corregidas.
- Paneles nuevos con jerarquía, alumno, espaciado y alineación consistentes.
- Navegación lateral por categorías con estados y desplazamiento.

## Pruebas

```bat
dotnet build src/XDLauncher/XDLauncher.csproj -c Debug -p:Platform=x64
MinecraftLauncher.exe --selftest
MinecraftLauncher.exe --capture-settings
dotnet publish src/XDLauncher/XDLauncher.csproj -c Release -r win-x64 --self-contained true -o dist/MinecraftLauncher
```

Resultado: 0 errores de compilación. Autodiagnóstico correcto, incluyendo [17] `El tema personalizado cambia AccentBrush en toda la aplicación`.

## Estado no completado

- No todas las 14 categorías tienen controles configurables nuevos; Privacidad y Descargas muestran información real y accesos, no opciones simuladas.
- No se verificó visualmente cada categoría de forma aislada, solo la vista principal de Ajustes y la navegación.
- El launcher instalado en Program Files sigue sin actualizarse por permisos; la versión nueva está publicada en `dist/MinecraftLauncher/MinecraftLauncher.exe`.
