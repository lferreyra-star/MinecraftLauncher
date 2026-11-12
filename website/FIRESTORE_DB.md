# Base de datos Firestore (XD Launcher)

## Estructura
- **Colección:** `users`
- **ID de documento:** `uid` (el UID de Firebase Authentication del usuario)
- **Campos:**
  - `uid` (string) — igual al ID del documento
  - `name` (string)
  - `email` (string)
  - `photoURL` (string | null)
  - `provider` (string) — `google.com`
  - `createdAt` (timestamp)
  - `lastLoginAt` (timestamp)

## Crear/subir en Firebase
1. Firebase Console → **Firestore Database** → **Create database** (si no existe).
2. No hace falta importar registros: al iniciar sesión con Google la app crea/actualiza `users/{uid}` automáticamente (con el UID real de Authentication).
3. (Opcional) Para crear la colección manualmente: **Iniciar colección** → ID colección `users` → ID documento = UID de un usuario de Authentication → agrega los campos de arriba (`createdAt`/`lastLoginAt` tipo *timestamp*).
4. Publica reglas: **Firestore Database → Rules** → pega `firestore.rules` de este proyecto → **Publicar**.
5. (Opcional) Índices: `firestore.indexes.json` incluido (no se requieren índices compuestos para esta estructura).

Archivos incluidos: `firestore.rules`, `firestore.indexes.json`, `firestore_seed.json` (ejemplo de esquema).
