# Centro de Mods AAA+ — informe de evidencias

Fecha: 20/11/2026

## Estado real

Se implementó un centro de mods funcional basado en la API pública de Modrinth v2. No es una pantalla decorativa: consulta el catálogo real, filtra por versión/loader/categoría, descarga archivos reales, verifica hashes, instala dependencias obligatorias y persiste una biblioteca por workspace/perfil.

No se ha podido copiar la compilación nueva a `C:\Program Files\Minecraft Bedrock Launcher` porque el proceso actual no tiene permisos de administrador. La versión nueva sí quedó publicada en `dist/MinecraftLauncher`. Para usarla en el launcher instalado hay que ejecutar como administrador:

```bat
ACTUALIZAR_LAUNCHER_INSTALADO.bat
```

Ese script compila, publica y copia a `C:\Program Files\Minecraft Bedrock Launcher`.

## Archivos modificados

- `src/XDLauncher/Services/JavaWorkspace.cs`: nuevos loaders reales `Forge`, `NeoForge`, `Quilt`, normalización segura al cargar perfiles, campo `LoaderVersion`, helpers `GetConfigDirectory` y `EnsureProfileDirectories`.
- `src/XDLauncher/Services/SelfTestService.cs`: pruebas reales [13], [14] y [15] de Modrinth.
- `src/XDLauncher/ViewModels/ShellViewModel.cs`: nueva sección `Centro de Mods` en la navegación.
- `src/XDLauncher/Views/HomeView.xaml`: dashboard reestructurado (portada cinematográfica, selector vertical de ediciones, centro de operaciones y módulos).

## Archivos nuevos

- `src/XDLauncher/Services/ModrinthService.cs`: cliente real de Modrinth v2.
  - Búsqueda paginada con facets de `project_type:mod`, `versions`, `loaders` y `categories`.
  - Detalles de proyecto, versiones compatibles y versión concreta.
  - Listado dinámico de versiones de Minecraft y loaders desde `/tag/game_version` y `/tag/loader`.
  - Caché en memoria de 10 minutos.
- `src/XDLauncher/Services/ModInstallService.cs`: instalador real.
  - Descarga con progreso, velocidad y ETA.
  - Verificación SHA-512 o SHA-1 cuando Modrinth los aporta; si no, validación por tamaño.
  - Escritura temporal y `File.Move` atómico.
  - Copia de seguridad `.bak` antes de actualizar.
  - Resolución recursiva de dependencias obligatorias (máximo 5 niveles).
  - Detección de duplicados por hash.
  - Activación/desactivación mediante `.jar.disabled`.
  - Eliminación de archivos y metadatos.
  - Biblioteca persistida en `mods-library.json` dentro del workspace.
- `src/XDLauncher/ViewModels/ModsCenterViewModel.cs`: estado de búsqueda, filtros, instalación, biblioteca, actualizaciones y progreso.
- `src/XDLauncher/Views/ModsCenterView.xaml`: catálogo visual, biblioteca, pestaña de modpacks, filtros, detalles y panel de progreso.
- `src/XDLauncher/Views/ModsCenterView.xaml.cs`: código subyacente de la vista.
- `tools/capture-ui.ps1`, `tools/capture-mods-center.ps1`, `tools/capture-modpacks.ps1`: capturas reales de la interfaz.

## Cambios estructurales

- La navegación pasa de 8 a 9 secciones e incorpora `Centro de Mods`.
- El dashboard ya no usa la composición anterior de una tarjeta grande y tres tarjetas pequeñas: ahora tiene una portada panorámica con selector vertical de ediciones, un centro de operaciones y una fila modular.
- Cada mod se instala en `games/<versión>/mods` dentro del workspace Premium o NoPremium seleccionado, respetando el `game_directory` real del perfil Java.
- La biblioteca queda separada por workspace y por perfil (`ProfileId`), sin mezclar versiones ni loaders.
- Se añadió `config/` por perfil para futuras configuraciones de mods.

## Comandos ejecutados

```bat
dotnet build src/XDLauncher/XDLauncher.csproj -c Debug -p:Platform=x64
dotnet publish src/XDLauncher/XDLauncher.csproj -c Release -r win-x64 --self-contained true -o dist/MinecraftLauncher
```

Resultado: compilación correcta, 0 errores, solo advertencias preexistentes.

## Ruta de la versión nueva

```text
C:\Users\EVIL\Documents\xD\dist\MinecraftLauncher\MinecraftLauncher.exe
```

El archivo publicado tiene fecha 20/11/2026 y hash SHA-256 distinto de la copia instalada anterior.

## Pruebas reales ejecutadas

Salida de `MinecraftLauncher.exe --selftest`:

```text
[13] Catálogo real de mods (Modrinth API)
  Versiones publicadas: 103 · loaders: fabric, forge, neoforge, quilt
  Primero: Sodium · descargas 254,729,831
  [OK] Búsqueda real y resolución de versiones correctas (43 resultados, 13 versiones compatibles).

[14] Instalación real de un mod desde Modrinth
  [OK] El instalador rechazó una combinación incompatible (1.12.2/Forge).
  [OK] Instalación real verificada: Sodium · 948 KB

[15] Dependencias obligatorias reales
  [OK] Dependencias instaladas automáticamente: Sodium + Iris Shaders

[16] Descubrimiento real de modpacks
  [OK] Modpacks reales: 19316. Primero: Fabulously Optimized · 481 versión(es).

RESULTADO: correcto
```

Estas pruebas usan la API real de Modrinth y archivos `.jar` reales descargados y verificados.

## Funciones NO implementadas en esta entrega

Para no inventar resultados, se indican explícitamente:

- CurseForge API: no implementada porque requiere una clave de API autorizada. No se intentó evadir esa restricción.
- Instalación automática de Forge, NeoForge y Quilt: no implementada. El catálogo detecta esos loaders y el instalador rechaza versiones incompatibles, pero solo Fabric tiene instalador real heredado del launcher.
- Modpacks: descubrimiento real implementado (19.316 modpacks reales, probado en [16]) e instalador `.mrpack` implementado, pero su instalación completa no se ha probado de extremo a extremo porque los modpacks populares descargan decenas o cientos de archivos. No se declara esa función como verificada.
- Descarga automática de addons Bedrock desde catálogo externo: no implementada; el apartado Bedrock existente (`Complementos`) se conserva sin mezclarlo con mods Java.
- Capturas de pantalla: se generaron correctamente en `captures/home.png`, `captures/mods-center.png` y `captures/modpacks.png`.

## Verificación visual

Las capturas muestran el dashboard reestructurado y el Centro de Mods funcionando con resultados reales de Modrinth. Durante la verificación se detectaron y corrigieron dos fallos visuales reales: falta de `DataTemplate` para `ModsCenterViewModel` y enlace TwoWay inválido en `ProgressPercent`. La pestaña de modpacks está presente, pero la captura no logró cambiar el contenido de la pestaña; por eso no se declara verificada visualmente.

La copia instalada en `C:\Program Files\Minecraft Bedrock Launcher` no se pudo actualizar sin permisos de administrador. Por eso no se declara completada la verificación visual final sobre el launcher instalado.
