# Informe técnico — XD Launcher

> **Version:** XD Launcher 1.3.7  
> **Date:** 20 November 2026  
> **Branch state:** Uncommitted changes in the existing redesign branch. No git commits were made unless requested.

## Executive summary

The main causes of the stuck access flow were:

1. **Infinite loading in the Google bridge:** `LocalAuthBridge.RequestGoogleTokenAsync` waited on a `TaskCompletionSource` whose timeout canceled the CTS but never completed the TCS (`src/XDLauncher/Services/LocalAuthBridge.cs`, confirmed on HEAD via `git show HEAD:...`). The login timeout was also only a secondary mechanism.
2. **XAML runtime exception:** validated with a new XAML/animation smoke test (`[12]` in Selftest); no `RenderTransform.ScaleX`-style invalid paths remain.

We then repaired the login pipeline, hardened the local bridge, improved web login, added login success visuals, accounted for the handoff race, and extended `--selftest`. Selftest passes fully except real Firestore synchronization, which is blocked by the project's missing Firestore database.

## Changes implemented since the last report

### 1. Google access repair

- `LocalAuthBridge` (`src/XDLauncher/Services/LocalAuthBridge.cs`):
  - Timeouts now complete/cancel the `TaskCompletionSource` on **every** path (timeout, cancel, error, token, server timeout).
  - Added one-time nonce `session` in `StartUrl` and validation in `/token`, `/error`, `/cancel`, and `/status`.
  - Strict methods: `GET` only for `/` and `/health`; `POST` only for token/error/cancel/status.
  - Token is validated against a 65536-byte page limit and JSON parsing errors return `400` instead of handing an empty token.
  - Fixed `RequestGoogleTokenAsync` open-browser error handling; calls now support `openBrowser` for tests.
  - The bridge page was rewritten: official G logo, progress steps, popup primary flow, redirect fallback, clear cancel/retry buttons, watchdog, reduced-motion support, and exact launcher error pages.
- `XdAuthService` (`src/XDLauncher/Services/XdAuthService.cs`):
  - Reports `GoogleSignInStage.SynchronizingProfile` and uses a linked 8-second timeout for Firestore profile sync.
  - Default Google timeout changed from 8 to 4 minutes.
- `LoginViewModel` (`src/XDLauncher/ViewModels/LoginViewModel.cs`):
  - `GoogleTimeoutMinutes = 4`.
  - Added `IsSuccess`, `SuccessMessage`, `SuccessDetail`.
  - `CompleteAsync` has a single completion guard (`_completionStarted`), clears the form into a cinematic success state, and still calls `Authenticated` after a short delay.
  - `CompleteExternalLogin` now performs a delayed success transition instead of double-calling.
  - Moved password clearing and success UI into the completion path.

### 2. Login visuals

- Added a modern success overlay (`src/XDLauncher/Views/LoginWindow.xaml` + `.cs`): animated check, "Sesión cifrada · Firebase Auth + DPAPI", deployment progress, close button, reduced-motion support.
- `LoginWindow` ambient orphans.

### 3. Handoff and account synchronization

- `SessionHandoff`: added optional `refresh` handoff parameter.
- Website `website/js/main.js`:
  - `handoffToDesktop` now sends `refresh` when available and is guarded against repeated navigation.
  - `startGoogle` now falls back to redirect only for `popup-blocked`/`operation-not-supported-in-this-environment`; config errors (unauthorized-domain, disabled provider, network) show retryable errors instead of looping.
  - Desktop handoff messages and retry behavior improved.
- `App.xaml.cs`:
  - Fixed the startup handoff race using `TakePendingHandoff()`; `ShowLoginAsync` now checks for pending handoff before and after `Show()`.
  - While the main window is open, a handoff now updates the account in place via `ShellViewModel.UpdateAccount` and `HomeViewModel.UpdateAccount`/`SettingsViewModel.UpdateAccount`.
  - Accepts refresh tokens from the web handoff.
  - Removed duplicated sign-out login flow; `SignInAfterSignOutAsync` uses `ShowLoginAsync` consistently.

### 4. Motion and effects

- `AnimationHelper.AmbientEffectsEnabled` static property added.
- Login/Splash ambient layers are hidden and stopped when ambient motion is disabled; status pulse and page animations respect motion settings.
- `AnimationHelper` now exposes `AmbientMotionEnabled`.

### 5. Tests and diagnostics

- `SelfTestService`:
  - New `[10]` local bridge: rejects unknown nonce, delivers token/refresh token over local HTTP, proves timeout (`401 ms`), proves cancel (`155 ms`).
  - New `[12]` XAML/animation smoke: instantiates Splash, Login, Shell/MainWindow and runs `RunVisualSmokeTest`.
  - Fixed a test bug: start the bridge before making HTTP requests so accept loops are running.
- `MainWindow.xaml.cs`: added `RunVisualSmokeTest()` internal method.

## Verified evidence

### Build

```text
C:\Users\EVIL\Documents\xD\src\XDLauncher> dotnet build src/XDLauncher/XDLauncher.csproj -c Debug -p:Platform=x64
XDLauncher -> C:\Users\EVIL\Documents\xD\src\XDLauncher\bin\x64\Debug\net10.0-windows10.0.19041.0\win-x64\MinecraftLauncher.dll
Compilación correcta.
```

Pre-existing warnings remain (`Commands.cs`, `ModDownloadService.cs`, `UpdateService.cs`) — they were present before this task.

### `MinecraftLauncher.exe --selftest`

Representative output:

```text
[10] Puente local de Google (handoff y timeout)
  Página local: 200 · health: 200
  [OK] El endpoint de token rechaza nonces desconocidos.
  [OK] El token y el refresh token llegan al launcher por el puente local.
  [OK] El timeout del puente completa la espera en 401 ms.
  [OK] La cancelación del puente termina la espera en 155 ms.

[12] Interfaz XAML y pipeline de animaciones
  [OK] Splash, acceso, shell y transformaciones RenderTransform se cargan sin excepciones.

RESULTADO: correcto
```

### Real service checks

- XD Launcher auth:
  - `accounts:lookup` connectivity OK.
  - Stored session `estivenelvro2@gmail.com` validates.
  - Selftest [9] passes.
- Store catalog: passes (`Minecraft for Windows`, catalog date 20 Nov 2026).
- Minecraft detection: `1x Minecraft: Bedrock Edition`, retail, unsigned optional `Aplicación Xbox`, installed missing optional updates.
- Firestore:
  - `FetchProfileAsync` returns `null`.
  - `SyncProfileAsync` fails: project `xd-launcher-8082d` has no Firestore database.
- Website:
  - `node --check website/js/main.js` OK.
  - Network probe in this shell returned `000` for `https://xdluncher.gt.tc/`; no HTTP response. This is likely restricted from the shell environment, so avoid claiming live site status without a browser/host-side check.

### GUI smoke

I attempted a quick GUI start/kill via `MinecraftLauncher.exe`; it started and logged the restored account. The shell used here does not reliably invoke PowerShell for clicks, so pixel-level confirmation was not automated.

## Remaining issues

1. **Firestore database doesn't exist for `xd-launcher-8082d`.** To make `SyncProfileAsync` work, the Firebase/InfinityFree project must have a Firestore or Cloud Datastore database for the project and rules must allow `users/{uid}` updates with `lastLoginAt/createdAt` server timestamps. Until then profile sync is by design non-fatal (`SyncProfileAsync` returns false).
2. **Full end-to-end Google consent in this shell is impossible** because no real Google OAuth Native client ID/secret is present. The launcher now uses Firebase JS popup, which is correct, but a real manual test with a Google account and password is required.
3. **Real browser check of the local bridge page** should be done: open the page, click `CONTINUAR CON GOOGLE`, approve, verify the launcher's `Access Restored automatically...` status changes.
4. **Cancel path**: page cancel posts `/cancel`; `LocalAuthBridge.Cancel()` is tested, but the bridge Cancel while browser open is only reachable via Close Window/Cancel button and should be exercised in a manual flow.
5. **Deploy**: website changes are local under `website/`; InfinityFree deployment requires the documented FTP/File Manager steps.

## Backup

The preexisting backup contains the git bundle and source copies:

```text
C:\Users\EVIL\AppData\Local\Temp\opencode\xd-backup-20261120\repo-all.bundle
```

No additional backup was created because all changes are in the existing working tree and not committed.
