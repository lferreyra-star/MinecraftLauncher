# XD Launcher · reconstrucción global del sistema visual

## Auditoría real

- Proyecto WPF: `Program.cs` → `App.xaml` → `Shared/Dark/Rebirth/UltimateSettings`.
- Tres sistemas visuales convivían y `ModernTheme.xaml` estaba muerto.
- No había estilos implícitos globales para Button/TextBox/CheckBox; cada vista definía sus propios colores literales.
- El tema claro no conmutaba las superficies Rebirth/Ultimate: riesgo real de texto oscuro sobre fondo oscuro.
- Navegación duplicada: botón Ajustes en cabecera + dock; estados de sidebar muertos.
- `MobileView` no tenía `ScrollViewer`; tarjetas con alturas fijas recortaban contenido.
- Animaciones fragmentadas en MainWindow, Splash, Login y JavaView.

## Cambios centralizados

- `Themes/XDGlobal.xaml` nuevo, fusionado al final en `App.xaml`.
  - Estilo implícito global de `Window`, `TextBlock`, `Button`, `TextBox`, `CheckBox`.
  - Botones, tarjetas, textos, inputs e interruptores usan DynamicResource del tema activo.
  - Sobrescribe los estilos base heredados sin duplicar archivos.
- `Services/ToastHub.cs`: `ThemeManager` ahora actualiza también pinceles Rebirth y Ultimate al cambiar tema, evitando texto negro sobre fondo oscuro.
- `Themes/ModernTheme.xaml` eliminado: era un sistema muerto con claves duplicadas.
- `MainWindow.xaml`:
  - Eliminado el botón duplicado de Ajustes.
  - Nueva barra de estado global sobre el dock con sección, estado del juego, cuenta y versión.
  - Margen del contenido adaptado para no tapar el dock ni la barra de estado.
- `Views/MobileView.xaml`: `ScrollViewer` añadido y `MinHeight` para resoluciones pequeñas.
- `Views/AddonsView.xaml` y `Views/ModsCenterView.xaml`: tarjetas con `MinWidth/MinHeight` en vez de alturas rígidas.
- `Views/SettingsView.xaml`: panel contextual “ESTADO DEL SISTEMA” disponible en todas las categorías; color de acento legible en paletas oscuras.
- `Services/SelfTestService.cs`: prueba [18] de sistema visual global y temas claros/oscuros.

## Pruebas

- `dotnet build ... -c Debug -p:Platform=x64`: 0 errores.
- `--selftest`: correcto, incluye [17] tema personalizado y [18] sistema visual global.
- Navegación verificada en las 9 secciones con `tools/capture-dock.ps1`.
- Capturas nuevas en `captures/dock/01-inicio.png` … `09-movil.png`.
- Publicación Release self-contained en `dist/MinecraftLauncher`.

## Estado

El sistema global ahora afecta a todas las vistas, incluidas las que no se reescribieron individualmente. No se ha realizado una composición única nueva para cada uno de los 15 apartados listados; por eso no se declara completada una reconstrucción artística individual de absolutamente todas las pantallas. Lo que sí está corregido es la arquitectura visual: estilos centralizados, contraste legible, tema claro/oscuro coherente, navegación sin duplicados y contenedores con scroll.
