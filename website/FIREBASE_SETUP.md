# XD Launcher Web + Launcher de escritorio — Firebase Auth + Firestore (InfinityFree)

Sistema real de registro/inicio de sesión con **Firebase Authentication** y perfiles en **Cloud Firestore**. La **misma cuenta** sirve en la web (`xdluncher.gt.tc`) y en el **BEDROCK LAUNCHER** de escritorio, porque ambos clientes hablan con el mismo proyecto de Firebase y la misma colección `users`.

Funciona en **InfinityFree** (HTML, CSS, JS; sin Node.js, sin servidor propio, sin procesos permanentes).

## 1. Configurar Firebase

1. Entra en [Firebase Console](https://console.firebase.google.com/) y usa el proyecto `xd-launcher-8082d` (o crea uno nuevo).
2. **Agregar app web**: en *Descripción general del proyecto* → icono `</>` → registra la app. Copia el objeto `firebaseConfig`.
3. **Authentication**: ve a *Build → Authentication → Get started*.
   - Pestaña *Sign-in method* → habilita **Google** (elige un correo de soporte y guarda).
   - Pestaña *Sign-in method* → habilita **Email/Password** (obligatorio para el acceso con correo del launcher de escritorio).
     - *Enable Email/Password (Provider)* → en.
     - *Email link (passwordless sign-in)* → desactivado (opcional).
     - Si el proyecto es de prueba, añade al menos un correo en *Authorized domains* / *Users* para poder crear cuentas.
   - En *Settings → Authorized domains* agrega `xdluncher.gt.tc` y `localhost`.
4. **Firestore Database**: *Build → Firestore Database → Create database* → ubicación → modo **Production**.
5. **Reglas de seguridad de Firestore**: en *Firestore Database → Rules* pega el contenido de `firestore.rules` (incluido en este proyecto) y publica:
   - Cada usuario solo puede leer/escribir su propio documento `users/{uid}`.
   - `provider` solo puede ser `google.com` o `password`.
   - `createdAt`/`lastLoginAt` se validan con `request.time`; no se puede suplantar `uid` ni `provider`.
6. **Pegar configuración en el proyecto web**: edita `js/config.js` (ya viene con los valores reales de `xd-launcher-8082d`).
7. **Pegar configuración en el launcher de escritorio**: los valores públicos ya están en `src/XDLauncher/Services/XdAuthService.cs`
   (`ApiKey`, `ProjectId`). Si cambias de proyecto, actualiza ambos archivos.

## 2. Cómo funciona (seguridad)

### Web (`website/`)

- Inicio de sesión con **Google** (`signInWithPopup`) **o con correo y contraseña** (`signInWithEmailAndPassword`, `createUserWithEmailAndPassword`, `sendPasswordResetEmail`).
- Sesión persistente en el navegador (`browserLocalPersistence`).
- Perfil en **Cloud Firestore**, colección `users`, documento con ID = `uid`:
  `uid`, `name`, `email`, `photoURL`, `provider` (`google.com` | `password`), `createdAt`, `lastLoginAt`.

### Launcher de escritorio (`src/XDLauncher/`)

- `XdAuthService` speak REST de **Firebase Authentication** (Identity Toolkit):
  - `accounts:signInWithPassword` → inicio de sesión.
  - `accounts:signUp` + `accounts:update` → alta de cuenta con nombre visible.
  - `accounts:sendOobCode` (`PASSWORD_RESET`) → recuperación real de contraseña por correo.
  - `securetoken.googleapis.com/v1/token` → renovación de sesión (`refresh_token`).
  - `accounts:lookup` → datos reales del usuario (nombre, correo, foto, UID).
- `XdSessionStore` guarda la sesión en `%APPDATA%\XDLauncher\xd-session.json` con los tokens **cifrados con DPAPI** (ámbito del usuario actual).
  La contraseña **nunca** se escribe en disco: solo viaja por HTTPS al servidor de autenticación.
- `XdAuthService.SyncProfileAsync` sincroniza el documento `users/{uid}` usando el endpoint `documents:commit` con transformaciones de servidor (`REQUEST_TIME`), para cumplir exactamente las reglas de Firestore.
- El launcher **no se abre sin sesión válida**: `App.OnStartup` restaura la sesión, la valida contra el servidor y, si falla, muestra `LoginWindow` antes de crear `MainWindow`.
- La foto de perfil se descarga a la caché local (`avatars/`) y se muestra en barra lateral, dashboard y ajustes.

## 3. Archivos a subir a InfinityFree

Sube el **contenido** de la carpeta `website/` a la carpeta pública de tu hosting (`htdocs/` o la raíz del dominio):

- `index.html` (página principal + modal de acceso)
- `css/styles.css`
- `js/config.js` (configuración de Firebase)
- `js/main.js`
- `assets/logo.png`

Archivos de referencia: `firestore.rules`, `firestore.indexes.json`, `FIREBASE_SETUP.md`, `FIRESTORE_DB.md`.

**No requiere** Node.js, npm, terminal, Next.js ni procesos en segundo plano. Abre `https://xdluncher.gt.tc/` y listo.

## 4. Comprobación sin Node.js

- El sitio es estático (HTML/CSS/JS) servido por InfinityFree; los SDK de Firebase se cargan desde CDN (`gstatic.com`).
- Para probar en local: sirve la carpeta con cualquier servidor estático o la extensión "Live Server".
- Si `config.js` no está configurado, la web sigue funcionando y el botón de login muestra un aviso indicando que falta configurar Firebase.
- Comprobación del launcher: `MinecraftLauncher.exe --selftest` incluye la sección **[9] Cuenta de XD Launcher**, que valida la sesión guardada contra el servidor.

## 5. Estructura de datos (Firestore)

Colección: `users` — Documento: `{uid}`

| Campo | Tipo | Descripción |
|---|---|---|
| uid | string | UID de Firebase Auth (ID del documento) |
| name | string | Nombre del usuario |
| email | string | Correo |
| photoURL | string \| null | Foto de perfil |
| provider | string | `google.com` (Google) o `password` (correo y contraseña) |
| createdAt | timestamp | Fecha de creación (serverTimestamp) |
| lastLoginAt | timestamp | Último acceso (serverTimestamp) |
