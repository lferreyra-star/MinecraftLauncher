# XD Launcher · Minecraft Bedrock + Java

Launcher de escritorio para **Minecraft Bedrock Edition en Windows 10/11**, **Minecraft Java Edition**
y la sección **MINECRAFT MOBILE CONNECT**: conecta tu teléfono Android por USB o Wi-Fi, transmite su
pantalla en tiempo real dentro del launcher y controla **el Minecraft Bedrock original que ya tienes
instalado en el móvil** con el teclado y el ratón del PC.

El inicio incluye un **selector de edición** (Java Edition, Bedrock Windows o Bedrock Android) que
adapta el botón principal de juego a la edición elegida.

> Launcher de escritorio no oficial. No está afiliado a Mojang Studios ni a Microsoft.
> Minecraft es una marca de Mojang Synergies AB.

---

## 1. MINECRAFT MOBILE CONNECT

No se instala ninguna copia extra de Minecraft, no se usa la versión de Microsoft Store desde el PC,
ni ningún emulador. La función **no toca** los archivos internos del juego: ni mundos, ni skins, ni
addons, ni la cuenta de Microsoft/Xbox, ni el progreso. Solo usa ADB y scrcpy, exactamente igual que
haría la herramienta oficial `scrcpy` de Genymobile.

### Flujo recomendado

1. Abre `MinecraftLauncher.exe`.
2. En el menú lateral entra en **Móvil** (o usa el botón **Abrir sección** de la tarjeta
   *MINECRAFT MOBILE CONNECT* del inicio).
3. Pulsa **CONECTAR CELULAR** (detecta cualquier dispositivo autorizado por USB o Wi-Fi) o
   **CONECTAR POR USB** / **CONECTAR POR WIFI** para ser explícito.
4. Pulsa **INICIAR TRANSMISIÓN**.
5. Abre Minecraft Bedrock **en el teléfono** y juegas desde el PC con teclado y ratón.
6. **PANTALLA COMPLETA** (o la tecla **F11**) deja la transmisión a pantalla completa.

### Botones disponibles

| Botón | Qué hace |
|---|---|
| CONECTAR CELULAR | Detecta el primer dispositivo Android autorizado (USB o Wi-Fi). |
| CONECTAR POR USB | Espera un teléfono por cable USB y avisa si falta la autorización. |
| CONECTAR POR WIFI | Abre el diálogo de emparejamiento Wi-Fi (búsqueda mDNS, conexión y emparejamiento con código). |
| INICIAR TRANSMISIÓN | Lanza scrcpy con la calidad configurada y lo incrusta en el launcher. |
| DETENER TRANSMISIÓN | Cierra scrcpy y libera la ventana incrustada. |
| PANTALLA COMPLETA | Alterna el modo a pantalla completa (oculta barra de título y menú). |
| CONFIGURAR CONTROLES | Reasigna todas las teclas del PC (WASD, espacio, Shift, E, Esc, 1-9, chat, F5…). |
| DESCONECTAR | Detiene la transmisión y corta la conexión ADB Wi-Fi. |
| JUGAR | Atajo: conecta el teléfono y arranca la transmisión en un paso. |

La página muestra en todo momento **nombre del dispositivo**, **estado de la conexión**,
**resolución** y **FPS reales** (leídos del contador de scrcpy, no estimados).

### Controles del PC

Minecraft Bedrock para Android admite teclado y ratón de forma nativa, así que por defecto el
launcher envía las teclas directamente al juego mediante el modo **UHID** de scrcpy
(`--keyboard=uhid --mouse=uhid`), que **no necesita** el permiso `INJECT_EVENTS`:

| Acción | Tecla por defecto |
|---|---|
| Moverse | W / A / S / D |
| Mirar | Ratón |
| Atacar / romper | Clic izquierdo |
| Colocar / usar | Clic derecho |
| Saltar | Espacio |
| Agacharse | Shift |
| Inventario | E |
| Pausa | Esc |
| Ranuras 1-9 | 1 … 9 |
| Correr | Ctrl |
| Chat | T |
| Cambiar cámara | F5 |
| Pantalla completa del launcher | F11 |
| Alternar control táctil / nativo | F10 |

Todas las teclas son **reasignables** en *CONFIGURAR CONTROLES*.

### Modo táctil opcional (botones en pantalla)

Si activas **Control táctil**, el launcher deja de mandar el teclado al juego y traduce las pulsaciones
a toques reales sobre la pantalla del teléfono usando el comando oficial `input` de Android:

* WASD tira del joystick táctil.
* Los botones (saltar, agacharse, inventario, pausa, soltar, chat, ranuras 1-9, atacar, colocar)
  se pulsan en la posición que calibres.

**Calibración**: *Calibrar posiciones táctiles* captura una imagen real del teléfono con
`adb exec-out screencap -p`, te la muestra y tú pulsas sobre ella la posición de cada botón. Las
coordenadas se guardan normalizadas (0-1), por lo que sobreviven a cambios de resolución y de
orientación.

### Wi-Fi: emparejamiento oficial

1. En el teléfono: **Ajustes › Ajustes de desarrollador › Depuración por Wi-Fi**.
2. Pulsa **CONECTAR POR WIFI** → *Buscar por Wi-Fi* (mDNS) o escribe la dirección a mano.
3. Android pedirá autorizar el equipo: el launcher **nunca** salta ese diálogo, es Android quien
   autoriza (requisito de Android, no una limitación del launcher).
4. Si la conexión pide emparejamiento, introduce la **dirección de emparejamiento** y el **código de
   6 dígitos** que muestra el móvil (*Emparejar dispositivo con código de emparejamiento*).

### Herramientas que instala el launcher

Se descargan **solo desde las fuentes oficiales** y se instalan en
`%APPDATA%\XDLauncher\tools`:

* **ADB**: `platform-tools-latest-windows.zip` de `dl.google.com/android/repository`.
* **scrcpy**: última release oficial de `github.com/Genymobile/scrcpy`, **verificada con el
  SHA-256 publicado** (`SHA256SUMS.txt`).

Si ya tienes el SDK de Android o `adb` en el PATH, el launcher los detecta y los usa.

---

## 2. Minecraft Java Edition

La sección **Minecraft Java** (menú lateral o botón del inicio) descarga e instala versiones oficiales
desde el manifiesto de Mojang y lanza el cliente real de Java Edition.

* **Biblioteca oficial**: releases, snapshots y versiones antiguas, con buscador y filtros
  (Todas, Release, Snapshot, Antiguas, Instaladas).
* **Instalación real**: cliente `.jar`, bibliotecas, nativos, índice de recursos, objetos y
  configuración de registro, todo desde `piston-meta.mojang.com`, `libraries.minecraft.net` y
  `resources.download.minecraft.net`.
* **Java automático**: detecta un Java 64 bits del sistema; si no existe, descarga el runtime
  oficial de Mojang y lo guarda en `%APPDATA%\XDLauncher\java\runtimes`.
* **Lanzamiento real**: construye la línea de argumentos oficial (JVM + juego), aplica las reglas de
  sistema operativo y abre `javaw.exe` con el cliente de Mojang. Incluye botón **Cerrar juego**.
* **Memoria y carpeta**: RAM asignada (1–32 GB), nombre del perfil local y carpeta de partidas
  configurables.
* **Cuentas**: con una cuenta Microsoft conectada en el launcher se solicita una sesión oficial de
  Minecraft Services (Xbox/XSTS). Sin cuenta Microsoft se usa un **perfil local no premium** y se
  avisa claramente; no se falsifica ninguna identidad ni token.
* **Registros**: la salida del juego se escribe en el registro del launcher para diagnosticar fallos.

> Alcance actual: se instalan y lanzan **versiones vanilla oficiales**. Los cargadores de mods
> (Forge, NeoForge, Fabric, OptiFine) y los modpacks **no están implementados todavía**; no se muestra
> ninguna opción falsa para ellos.

---

## 3. Funciones de Windows (conservadas)

* Detección de la instalación oficial de Bedrock (Retail/Preview) vía `PackageManager`.
* Verificación de paquete, firma y componentes obligatorios antes de jugar.
* Activación oficial por COM (`ApplicationActivationManager`).
* Comprobación de actualizaciones contra el catálogo oficial de Microsoft Store.
* Perfil local o cuenta Microsoft con Xbox Live (inicio de sesión por **código de dispositivo**,
  token cifrado con DPAPI; nunca se piden contraseñas).
* Gestor de complementos: importación de `.mcpack`, `.mcaddon`, `.mcworld` y `.mctemplate` en la
  carpeta oficial `com.mojang`, sin modificar los originales.
* Personalización: tema claro/oscuro, color de acento, fondo (4 fondos preinstalados o imagen
  propia), tamaño de ventana y escala de interfaz.
* Novedades oficiales, registro de actividad, diagnóstico del equipo y copia de diagnóstico.

Todo está en español y sigue funcionando exactamente igual que antes.

---

## 4. Compilar

Requisitos: .NET 10 SDK (probado con 10.0.404) e Inno Setup 6 (probado con 6.7.3).

```bash
dotnet build src/XDLauncher/XDLauncher.csproj -c Release -p:Platform=x64
```

Publicación portable (no requiere instalar .NET):

```bash
dotnet publish src/XDLauncher/XDLauncher.csproj -c Release -r win-x64 --self-contained true -o dist/MinecraftLauncher
```

Instalador (requiere Inno Setup 6; usa la carpeta anterior como origen):

```bash
iscc installer/MinecraftBedrockLauncher.iss
```

El instalador se genera como `dist/MinecraftLauncher_Setup.exe` y el ejecutable como
`dist/MinecraftLauncher/MinecraftLauncher.exe`.

Autodiagnóstico sin interfaz:

```bash
MinecraftLauncher.exe --selftest
```

---

## 5. Limitaciones conocidas (dicho con claridad)

* **Java**: solo se instalan versiones **vanilla** oficiales. Los cargadores de mods y modpacks aún no
  existen en la interfaz para no ofrecer botones que no funcionan.
* **Java sin cuenta Microsoft**: el perfil local permite jugar en un jugador y en LAN, pero los
  servidores que exigen cuenta premium rechazarán esa sesión. Es una limitación de Mojang, no del
  launcher.
* **Java premium**: requiere configurar el *Client ID* de Microsoft Entra en **Ajustes** y que la
  cuenta posea Minecraft Java Edition. Sin ese dato se usa el perfil local.
* **Descarga de Java**: si el equipo no tiene un Java 64 bits compatible, el launcher descarga el
  runtime oficial de Mojang (puede ocupar cientos de MB) la primera vez que se juega.
* **No se ha podido validar con un teléfono real autorizado por el usuario final**: la función se
  ha probado contra un dispositivo Android real conectado por USB (Xiaomi, Android 13) y contra un
  AVD local usado **solo como banco de pruebas**, nunca como parte del producto.
* **Audio**: si el PC no tiene un dispositivo de salida de audio, scrcpy aborta la sesión. El launcher
  lo detecta y **reintenta automáticamente sin sonido**, avisando con un aviso en pantalla.
* **MIUI/HyperOS**: Xiaomi pide activar *«Depuración USB (ajustes de seguridad)»* para algunos
  controles. Con el modo UHID de scrcpy el teclado y el ratón funcionan sin ese permiso; si
  necesitas otros gestos, activa esa opción en el móvil y reinícialo.
* **Los juegos con bloqueo de teclado/ratón** pueden ignorar la entrada del PC; en ese caso usa el
  modo táctil.
* **scrcpy no es compatible con todas las versiones de Android** para audio bidireccional; la
  transmisión de vídeo y la entrada UHID son lo que usa esta función.
* El launcher **no modifica** el juego del teléfono: no instala APKs, no cambia permisos y no toca
  mundos, cuentas ni progreso.

---

## 6. Estructura del proyecto

```
src/XDLauncher
├─ App.xaml(.cs)              Composition root, instancia única, selector de plataforma inicial
├─ MainWindow.xaml(.cs)       Ventana sin marco, layout de pantalla completa
├─ Core/                      Convertidores, comandos, INotifyPropertyChanged
├─ Models/                    Modelos de datos (incluye MobileConnectModels.cs y JavaModels.cs)
├─ Services/                  Java, descargas, ADB, scrcpy, entrada, settings, tema, toasts
├─ Themes/                    Shared.xaml (estilos) + Dark.xaml / Light.xaml
├─ ViewModels/                Shell, Home, Game, Catalog, Profile, Settings, MobileConnectState, Java
└─ Views/                     Páginas + diálogos (incluye MobileView, ScrcpyHost, JavaView)
```

Clases clave de Minecraft Java:

| Archivo | Responsabilidad |
|---|---|
| `Services/JavaService.cs` | Manifiesto oficial, instalación vanilla, Java automático, argumentos y lanzamiento real. |
| `Services/AuthService.cs` | Device code de Microsoft, Xbox Live, XSTS y sesión de Minecraft Services. |
| `Services/DownloadService.cs` | Descargas por HTTP con progreso y verificación SHA-256. |
| `ViewModels/JavaViewModel.cs` | Biblioteca, filtros, instalación, progreso, configuración y JUGAR. |
| `Views/JavaView.xaml` | Interfaz de la sección Minecraft Java. |

Clases clave de MINECRAFT MOBILE CONNECT:

| Archivo | Responsabilidad |
|---|---|
| `Services/AdbService.cs` | Detección USB/Wi-Fi, `adb connect/pair`, mDNS, capturas, `adb shell` persistente. |
| `Services/ScrcpyService.cs` | Descarga y verificación de scrcpy, lanzamiento, ventana, FPS. |
| `Services/InputMappingService.cs` | Hook de teclado/ratón (WH_KEYBOARD_LL / WH_MOUSE_LL) con reasignación de teclas. |
| `Services/TouchInputService.cs` | Inyección de toques (`input tap` / `input motionevent`) y joystick. |
| `Services/MobileInputCatalog.cs` | Catálogo de acciones, teclas por defecto y objetivos táctiles. |
| `ViewModels/MobileConnectState.cs` | Ciclo de vida ADB/scrcpy, estado, FPS, reconexión, comandos. |
| `Views/Controls/ScrcpyHost.cs` | `HwndHost` que reparenta la ventana de scrcpy con `SetParent`. |
