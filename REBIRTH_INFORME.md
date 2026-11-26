# XD Launcher Rebirth — informe real de reconstrucción visual

Fecha: 21/11/2026

## Auditoría

- Proyecto: WPF/C# (`src/XDLauncher/XDLauncher.csproj`), `AssemblyName=MinecraftLauncher`, `StartupObject=XdLauncher.Program`.
- Punto de entrada real: `Program.cs` → `App.InitializeComponent()` → `App.xaml` → `App.OnStartup`.
- Recursos visuales: `App.xaml` cargaba `Shared.xaml` + `Dark.xaml`; `ThemeManager` reemplaza el diccionario de tema en caliente.
- La versión instalada en `C:\Program Files\Minecraft Bedrock Launcher` se copia desde `dist/MinecraftLauncher` mediante `ACTUALIZAR_LAUNCHER_INSTALADO.bat`. El hash del DLL instalado no coincide con el publicado ahora: el usuario sigue viendo una compilación antigua hasta ejecutar ese script con administrador.
- Causas reales de que los cambios anteriores no se vieran:
  1. Modificaciones solo en diccionarios de color/estilos, sin cambiar la composición de `MainWindow`.
  2. Cachés/baml obsoletos al publicar sin limpiar `obj`.
  3. Vista `ModsCenterView` sin `DataTemplate`, por lo que se mostraba el nombre del ViewModel.
  4. Clave `RebirthPanel` duplicada entre brush y style, que impedía iniciar la aplicación.
  5. La copia instalada no se actualizaba por permisos de administrador.

## Cambios estructurales aplicados

- `src/XDLauncher/MainWindow.xaml`: reemplazo total del shell.
  - Eliminada la barra lateral clásica.
  - Nueva navegación superior con los 9 módulos visibles a la vez.
  - Barra de título con identidad `REBIRTH`, estado del juego y cuenta.
  - Fondo ambiental con dos orbes animados, patrón de rejilla y viñeta.
  - Panel de cabecera de sección y `ContentControl PageHost`.
  - Notificaciones rediseñadas.
- `src/XDLauncher/Themes/Rebirth.xaml`: nuevo sistema visual del shell.
  - Paleta Rebirth, gradientes, glow,面板 y estilos de navegación.
  - Estilos `RebirthNavItem`, `RebirthWindowButton`, `RebirthCloseButton`, `RebirthPanelStyle`.
- `src/XDLauncher/App.xaml`: carga del nuevo diccionario `Rebirth.xaml`.
- `src/XDLauncher/MainWindow.xaml.cs`: retirada la lógica de animación de sidebar, sustituida por navigation animada.
- `src/XDLauncher/Views/HomeView.xaml`: dashboard cinematográfico con portada panorámica, selector vertical de ediciones, centro de operaciones y actividad.
- `src/XDLauncher/Views/ModsCenterView.xaml`: centro de mods con catálogo, biblioteca, modpacks y panel de progreso.
- `src/XDLauncher/Themes/Shared.xaml`: `DataTemplate` añadido para `ModsCenterViewModel`.
- `src/XDLauncher/Services/SelfTestService.cs`: pruebas reales [13]–[16] de mods y modpacks.

## Sistemas preservados

Firebase, Google Login, sesión, Minecraft Java, Bedrock, Android, versiones, descargas, mods, complementos y ajustes siguen conectados a sus ViewModels y servicios originales. No se sustituyeron por demos.

## Compilación y pruebas

```bat
dotnet build src/XDLauncher/XDLauncher.csproj -c Debug -p:Platform=x64
dotnet publish src/XDLauncher/XDLauncher.csproj -c Release -r win-x64 --self-contained true -o dist/MinecraftLauncher
```

Resultado: 0 errores. Autodiagnóstico `--selftest`: correcto, incluyendo búsqueda real de mods, instalación real verificada, dependencias reales y descubrimiento de modpacks.

## Capturas reales

- `captures/home.png`
- `captures/sections/01-inicio.png`
- `captures/sections/02-java.png`
- `captures/sections/03-juego.png`
- `captures/sections/04-versiones.png`
- `captures/sections/05-complementos.png`
- `captures/sections/06-mods.png`
- `captures/sections/07-descargas.png`
- `captures/sections/08-ajustes.png`
- `captures/sections/09-movil.png`

Las capturas muestran el nuevo shell Rebirth aplicado a todas las secciones.

## Estado no completado

No se ha rediseñado internamente cada UserControl antiguo; las secciones cambian de shell, cabecera, navegación, fondos y estilos, pero其内部 composición sigue siendo la anterior en Java, Juego, Versiones, Complementos, Descargas, Ajustes y Móvil. Por tanto no se declara completada la reconstrucción visual integral de todas las pantallas.

Tampoco se pudo actualizar el launcher instalado en Program Files porque el proceso actual no tiene permisos de administrador (la elevación UAC fue cancelada). Para ver la versión Rebirth hay que ejecutar `ACTUALIZAR_LAUNCHER_INSTALADO.bat` como administrador.
