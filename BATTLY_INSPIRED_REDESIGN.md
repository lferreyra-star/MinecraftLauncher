# XD Launcher · rediseño inspirado en Battly (entrega real)

## Auditoría

- Battly Launcher usa Electron/TypeScript y tiene licencia personalizada; no se copió su código. Solo se estudiaron patrones públicos: barra inferior flotante, navegación por paneles, panel 70/30 en inicio, densidad de información y estado de servicios.
- Se confirmó que el ejecutable instalado coincide con `dist` antes de esta entrega; el problema no era solo caché, sino composición visual.

## Cambios estructurales aplicados

- `src/XDLauncher/MainWindow.xaml`
  - Eliminada la navegación superior.
  - Nueva barra inferior flotante con los 9 módulos y estado del juego.
  - Contenido principal con más espacio útil y menos paneles vacíos.
- `src/XDLauncher/Themes/Rebirth.xaml`
  - `RebirthDockItem`: navegación compacta con icono, etiqueta e indicador activo.
- `src/XDLauncher/Views/HomeView.xaml`
  - Reconstrucción total en tres zonas: actividad a la izquierda, portada central de acción y estado/servicios a la derecha.
  - Portada cinematográfica con botón principal grande.
  - Selector de edición con texto visible.
  - Panel de instalación, sesión y estado real.
  - Accesos rápidos reales a Java, Juego, Mods, Descargas y Ajustes.
  - Mayores contrastes y tipografías más legibles.

## Archivos nuevos o modificados

- Modificados: `MainWindow.xaml`, `Themes/Rebirth.xaml`, `Views/HomeView.xaml`.
- Capturas nuevas: `captures/home.png`, `captures/dock/01-inicio.png` … `09-movil.png`.

## Pruebas realizadas

- Compilación Debug: 0 errores.
- Autodiagnóstico `--selftest`: correcto.
- Navegación verificada con capturas de las 9 secciones mediante `tools/capture-dock.ps1`.
- Publicación Release self-contained en `dist/MinecraftLauncher`.

## Resultado

La diferencia estructural es evidente: barra inferior, tres zonas de información, paneles llenos con datos reales y texto legible. Queda pendiente ejecutar `ACTUALIZAR_LAUNCHER_INSTALADO.bat` como administrador, porque este proceso no puede sobrescribir `C:\Program Files\Minecraft Bedrock Launcher`.
