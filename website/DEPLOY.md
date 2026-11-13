# Despliegue de la web de XD Launcher en InfinityFree

Los ficheros de `website/` ya están corregidos y verificados. Para que
`https://xdluncher.gt.tc` sirva exactamente la misma versión que en localhost
solo hay que subir **4 ficheros** a la raíz pública del hosting.

## Opción A — File Manager de InfinityFree (sin scripts)

1. Entra en el panel de InfinityFree de la cuenta que administra `xdluncher.gt.tc`.
2. Abre **File Manager** → `htdocs` (la carpeta pública del dominio).
3. **Sube** (sobrescribiendo) estos ficheros de la carpeta `website/` de tu proyecto:

   ```
   index.html
   css/styles.css
   js/main.js
   .htaccess
   ```

4. `assets/logo.png` y `js/config.js` no han cambiado, pero si quieres estar seguro,
   súbelo también. El resto de ficheros (`.md`, `firestore.*`) son opcionales.
5. Verifica en `https://xdluncher.gt.tc/` con **Ctrl+F5** (forzar recarga sin caché).

> IMPORTANTE: sube el contenido de `website/`, no la carpeta `website` en sí.
> Es decir, `index.html` debe quedar en `htdocs/index.html`.

## Opción B — FTP por script (recomendado si hay muchas actualizaciones)

Necesitas el **usuario FTP y la contraseña FTP** de la cuenta de `xdluncher.gt.tc`
(están en InfinityFree → *Account* → *FTP/MySQL*, no son las de MySQL).

```powershell
powershell -ExecutionPolicy Bypass -File tools/subir-web.ps1 `
    -Host_ gt.tc -User if0_XXXXXXX -Password "TU_CLAVE" -RemoteDir htdocs
```

También puedes pedir las credenciales de forma interactiva omittingiendo `-User` y `-Password`:

```powershell
powershell -ExecutionPolicy Bypass -File tools/subir-web.ps1 -Host_ gt.tc
```

Para ver qué se subiría sin tocar nada:

```powershell
powershell -ExecutionPolicy Bypass -File tools/subir-web.ps1 -DryRun
```

## Verificación tras subir

```bash
# Debe devolver 200 los cinco recursos
curl -s -o /dev/null -w "%{http_code} " https://xdluncher.gt.tc/
curl -s -o /dev/null -w "%{http_code} " https://xdluncher.gt.tc/css/styles.css
curl -s -o /dev/null -w "%{http_code} " https://xdluncher.gt.tc/js/config.js
curl -s -o /dev/null -w "%{http_code} " https://xdluncher.gt.tc/js/main.js
curl -s -o /dev/null -w "%{http_code}\n" https://xdluncher.gt.tc/assets/logo.png

# El marcador de build debe decir xd-1.0 (indica que ya serves la versión nueva)
curl -s https://xdluncher.gt.tc/ | grep -o 'data-xd-build="[^"]*"'
```

Si el marcador no aparece, el hosting está sirviendo la versión anterior: borra la
caché del navegador (Ctrl+Shift+R) o el `cache` de InfinityFree.

## Pasos en Firebase Console (una sola vez)

1. **Authentication → Sign-in method → Google** → habilitado (ya lo está).
2. **Authentication → Sign-in method → Email/Password** → **Enable**.
   *(Sin esto, "Iniciar sesión" y "Crear cuenta" con correo devuelven
   `auth/operation-not-allowed`.)*
3. **Authentication → Settings → Authorized domains** → debe incluir `xdluncher.gt.tc`.
4. **Firestore Database → Rules** → pega el contenido de `website/firestore.rules` y publica.

## Problemas ya resueltos (no hace falta tocar nada)

- El login con Google usaba `signInWithPopup`, que el navegador bloquea
  (`auth/popup-blocked`). Ahora usa popup con **reintento automático por redirección**.
- `getRedirectResult` se quedaba esperando eternamente y bloqueaba la página.
  Ahora solo se consulta si la URL trae parámetros de redirección, y la sesión se
  recupera además con `onAuthStateChanged`.
- `index.html` apuntaba a `js/main.v3.js`, un fichero que no existía: el JavaScript
  no se ejecutaba en producción. Ahora apunta a `js/main.js`.
- Faltaban prefijos `-webkit-` en `backdrop-filter` y las clases `.magnetic`,
  `.card-glow`, `.hero-copy` y `.download-aside` no existían en el CSS.
- Se añadió `.htaccess` con `DirectoryIndex index.html`, cabeceras de seguridad,
  caché correcta y compresión, sin reescrituras que rompan `?desktop=1`.
