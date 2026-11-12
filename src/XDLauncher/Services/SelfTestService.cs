using System.Diagnostics;
using System.Security.Principal;
using System.Text;
using XdLauncher.Models;

namespace XdLauncher.Services;

/// <summary>Headless diagnostic used by --selftest. It never launches Minecraft.</summary>
public sealed class SelfTestService
{
    private readonly StringBuilder _output = new();

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var failures = 0;
        Line("XD Launcher · autodiagnóstico");
        Line("========================================");
        Line($"Fecha: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
        Line($"Producto: {AppInfo.ProductName} {AppInfo.FullVersion}");
        Line($"Sistema: {Environment.OSVersion.VersionString} · {Environment.OSVersion.Version} · {Environment.OSVersion.Platform}");
        Line($"Runtime: .NET {Environment.Version} · {System.Runtime.InteropServices.RuntimeInformation.OSArchitecture}");
        Line($"Equipo: {Environment.MachineName} · usuario {Environment.UserName}");
        Line($"Elevación: {(IsElevated() ? "sí" : "no")}");
        Line($"Directorio de la aplicación: {AppContext.BaseDirectory}");
        Line($"Datos del launcher: {Paths.AppDataRoot}");
        Line(string.Empty);

        Line("[1] Almacenamiento");
        try
        {
            Paths.EnsureLauncherDirectories();
            var probe = Path.Combine(Paths.AppDataRoot, $".write-test-{Guid.NewGuid():N}");
            await File.WriteAllTextAsync(probe, "ok", cancellationToken);
            File.Delete(probe);
            Pass("Carpeta de datos escribible.");
        }
        catch (Exception ex)
        {
            Fail("No se puede escribir en la carpeta de datos: " + ex.Message);
            failures++;
        }

        Line(string.Empty);
        Line("[2] DPAPI (protección de tokens)");
        try
        {
            const string secret = "xd-launcher-selftest";
            var protectedBytes = AccountStore.Protect(secret);
            var roundTrip = AccountStore.Unprotect(protectedBytes);
            if (roundTrip == secret)
                Pass("Cifrado y descifrado DPAPI para el usuario actual.");
            else
            {
                Fail("DPAPI devolvió un valor distinto.");
                failures++;
            }
        }
        catch (Exception ex)
        {
            Fail("DPAPI no disponible: " + ex.Message);
            failures++;
        }

        Line(string.Empty);
        Line("[3] Configuración");
        try
        {
            var settings = new SettingsService();
            Line($"  Tema: {settings.Settings.Theme} · acento: {settings.Settings.Accent} · fondo: {settings.Settings.BackgroundId}");
            Line($"  Inicio con Windows: {(StartupService.IsEnabled() ? "activado" : "desactivado")}");
            Line($"  Client ID de Microsoft configurado: {(string.IsNullOrWhiteSpace(settings.Settings.MicrosoftClientId) ? "no" : "sí")}");
            Pass("settings.json cargado.");
        }
        catch (Exception ex)
        {
            Fail("No se pudo cargar la configuración: " + ex.Message);
            failures++;
        }

        Line(string.Empty);
        Line("[4] Detección de Minecraft (PackageManager)");
        GameSnapshot snapshot = new();
        try
        {
            var game = new GameService();
            snapshot = await game.DetectAsync();
            if (snapshot.Error is { Length: > 0 })
                Fail("Error de detección: " + snapshot.Error);
            else if (snapshot.Installations.Count == 0)
                Line("  No hay una instalación de Minecraft en este equipo (resultado esperado si no está comprado/instalado).");
            else
            {
                foreach (var installation in snapshot.Installations)
                {
                    Line($"  {installation.DisplayName}");
                    Line($"    Versión: {installation.Version} · canal: {installation.ChannelLabel} · {installation.Architecture}");
                    Line($"    Paquete: {installation.PackageFullName}");
                    Line($"    Estado: {installation.PackageStatus} · firma: {installation.SignatureKind}");
                    Line($"    Instalado: {installation.InstalledDate.ToLocalTime():yyyy-MM-dd HH:mm} · AUMID: {installation.Aumid}");
                    Line($"    Componentes: {installation.Components.Count(c => c.IsInstalled)}/{installation.Components.Count} · faltan {installation.MissingRequiredCount} obligatorios");
                    foreach (var check in installation.Components)
                        Line($"      - {check.Name}: {check.StateLabel}{(string.IsNullOrWhiteSpace(check.Version) ? string.Empty : " · " + check.Version)}");
                }
                Pass($"Detección correcta: {snapshot.Installations.Count} instalación(es). Minecraft en ejecución: {(snapshot.IsGameRunning ? "sí" : "no")}.");
            }
        }
        catch (Exception ex)
        {
            Fail("La detección produjo una excepción: " + ex.Message);
            failures++;
        }

        Line(string.Empty);
        Line("[5] Activación COM");
        try
        {
            // The class is registered by CLSID; its ProgID is not present on every Windows build.
            var type = Type.GetTypeFromCLSID(new Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C"));
            if (type is null)
            {
                Fail("ApplicationActivationManager no está registrado.");
                failures++;
            }
            else
            {
                var instance = Activator.CreateInstance(type);
                if (instance is null)
                {
                    Fail("No se pudo crear ApplicationActivationManager.");
                    failures++;
                }
                else
                {
                    System.Runtime.InteropServices.Marshal.FinalReleaseComObject(instance);
                    Pass("ApplicationActivationManager disponible.");
                }
            }
        }
        catch (Exception ex)
        {
            Fail("No se pudo instanciar ApplicationActivationManager: " + ex.Message);
            failures++;
        }

        Line(string.Empty);
        Line("[6] Catálogo de Microsoft Store");
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(25));
            var catalog = new StoreCatalogService();
            var product = await catalog.GetProductAsync(force: true, timeout.Token);
            if (product is null)
            {
                Fail("No se pudo obtener metadatos del catálogo (sin conexión o servicio bloqueado).");
                failures++;
            }
            else
            {
                Line($"  Producto: {product.Title}");
                Line($"  Última actualización del catálogo: {product.LastUpdated?.ToLocalTime():yyyy-MM-dd HH:mm} ({(product.LastUpdated is null ? "no indicada" : "oficial")})");
                Line($"  Notas: {(string.IsNullOrWhiteSpace(product.Notes) ? "sin notas" : product.Notes.Length + " caracteres")}");
                Line($"  Imágenes: {product.ImageUrls.Count}");
                Pass("Catálogo oficial consultado.");
            }
        }
        catch (Exception ex)
        {
            Fail("Error consultando el catálogo: " + ex.Message);
            failures++;
        }

        Line(string.Empty);
        Line("[7] Complementos");
        try
        {
            var addons = new AddonService();
            var installed = addons.ScanInstalled();
            var library = addons.ScanLibrary();
            Line($"  Instalados: {installed.Count} · biblioteca: {library.Count}");
            Line($"  Carpetas com.mojang: {string.Join(", ", Paths.ExistingComMojangPaths().DefaultIfEmpty("ninguna"))}");
            Pass("Exploración de complementos completada.");
        }
        catch (Exception ex)
        {
            Fail("Error explorando complementos: " + ex.Message);
            failures++;
        }

        Line(string.Empty);
        Line("[8] Comprobación de actualizaciones");
        try
        {
            if (snapshot.Primary is null)
                Line("  Omitida: no hay instalación local.");
            else
            {
                var catalog = new StoreCatalogService();
                var product = await catalog.GetProductAsync(force: false, cancellationToken);
                var update = new GameService().ComputeUpdate(snapshot.Primary, product);
                Line($"  {update.StatusText}: {update.Detail}");
            }
            Pass("Lógica de actualización ejecutada.");
        }
        catch (Exception ex)
        {
            Fail("Error comprobando actualizaciones: " + ex.Message);
            failures++;
        }

        Line(string.Empty);
        Line("[9] Cuenta de XD Launcher (autenticación real)");
        try
        {
            var auth = new XdAuthService();
            var online = await auth.PingAsync(cancellationToken);
            Line($"  Servidor de autenticación: {(online ? "en línea" : "sin conexión")}");

            var stored = XdSessionStore.Load();
            if (stored is null)
            {
                Line("  Sesión local: ninguna (el launcher pedirá iniciar sesión).");
                Pass("Sistema de autenticación de XD Launcher accesible.");
            }
            else
            {
                var refreshToken = XdSessionStore.UnprotectToken(stored.ProtectedRefreshToken);
                if (string.IsNullOrWhiteSpace(refreshToken))
                {
                    Fail("La sesión guardada no tiene token de actualización legible.");
                    failures++;
                }
                else
                {
                    var account = await auth.RefreshAsync(refreshToken, cancellationToken);
                    Line($"  Sesión guardada: {account.Email}");
                    Line($"  Nombre del servidor: {account.DisplayName ?? "—"}");
                    Line($"  UID: {account.UserId}");
                    var profile = await auth.FetchProfileAsync(account, cancellationToken);
                    Line($"  Perfil Firestore: {(profile is null ? "sin documento" : profile.Name)}");
                    Pass("Sesión de XD Launcher validada contra el servidor.");
                }
            }
        }
        catch (Exception ex)
        {
            Fail("Error validando la cuenta de XD Launcher: " + ex.Message);
            failures++;
        }

        Line(string.Empty);
        Line("[10] Minecraft Java Edition · Premium / No-Premium");
        try
        {
            var premiumWorkspace = Paths.CreatePremiumJavaWorkspace();
            var noPremiumWorkspace = Paths.CreateNoPremiumJavaWorkspace();
            premiumWorkspace.EnsureDirectories();
            noPremiumWorkspace.EnsureDirectories();

            Line($"  Workspace Premium: {premiumWorkspace.Root}");
            Line($"  Workspace No-Premium: {noPremiumWorkspace.Root}");
            if (premiumWorkspace.Root.Equals(noPremiumWorkspace.Root, StringComparison.OrdinalIgnoreCase) ||
                premiumWorkspace.Root.StartsWith(noPremiumWorkspace.Root, StringComparison.OrdinalIgnoreCase) ||
                noPremiumWorkspace.Root.StartsWith(premiumWorkspace.Root, StringComparison.OrdinalIgnoreCase))
            {
                Fail("Los workspaces Premium y No-Premium comparten rutas.");
                failures++;
            }
            else
            {
                Pass("Los workspaces usan directorios independientes.");
            }

            var java = new JavaService(premiumWorkspace);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));

            var manifest = await java.GetManifestAsync(force: true, timeout.Token);
            Line($"  Versiones en el manifiesto: {manifest.Versions.Count} · última release: {manifest.LatestRelease ?? "desconocida"}");
            Line($"  Versiones instaladas (Premium): {(java.GetInstalledVersionIds().Count == 0 ? "ninguna" : string.Join(", ", java.GetInstalledVersionIds()))}");
            Line($"  Versiones instaladas (No-Premium): {(new JavaService(noPremiumWorkspace).GetInstalledVersionIds().Count == 0 ? "ninguna" : string.Join(", ", new JavaService(noPremiumWorkspace).GetInstalledVersionIds()))}");

            var uuid = JavaService.OfflineUuid("Jugador");
            var uuidAgain = JavaService.OfflineUuid("Jugador");
            if (uuid == uuidAgain && uuid.Length == 36 && uuid[14] == '3' &&
                uuid[19] is '8' or '9' or 'a' or 'b')
                Pass("UUID offline determinista con formato Java válido.");
            else
            {
                Fail("El UUID offline no tiene el formato esperado.");
                failures++;
            }

            var runtime = await java.DetectJavaAsync(17, null, timeout.Token);
            if (runtime is null)
                Line("  No hay Java 17+ de 64 bits instalado; el launcher descargará el runtime oficial al jugar.");
            else
                Line($"  Java detectado: {runtime.MajorVersion} ({runtime.VersionText}) · {runtime.Path}");

            var fabricRoot = "https://meta.fabricmc.net/v2/versions/loader/" + manifest.LatestRelease;
            var fabricCatalog = await DownloadService.TryDownloadTextAsync(fabricRoot, timeout.Token);
            if (!string.IsNullOrWhiteSpace(fabricCatalog) && fabricCatalog.Contains("\"loader\"", StringComparison.Ordinal))
                Pass("Catálogo oficial de Fabric consultado.");
            else
            {
                Fail("No se pudo consultar el catálogo de Fabric.");
                failures++;
            }

            var tempPremiumRoot = Path.Combine(Paths.CacheDirectory, "selftest-premium");
            var tempNoPremiumRoot = Path.Combine(Paths.CacheDirectory, "selftest-nonpremium");
            try
            {
                var premiumStore = new JavaProfileStore(
                    new JavaWorkspace(JavaWorkspaceKind.Premium, tempPremiumRoot));
                var noPremiumStore = new JavaProfileStore(
                    new JavaWorkspace(JavaWorkspaceKind.NoPremium, tempNoPremiumRoot));

                var profile = premiumStore.GetOrCreate("1.20.1");
                profile.PlayerName = "JugadorPrueba";
                profile.MemoryMb = 3072;
                premiumStore.SaveProfile(profile);

                var reloaded = new JavaProfileStore(
                    new JavaWorkspace(JavaWorkspaceKind.Premium, tempPremiumRoot));
                var persisted = reloaded.Find("1.20.1");
                if (persisted is { PlayerName: "JugadorPrueba", MemoryMb: 3072 } &&
                    noPremiumStore.Find("1.20.1") is null)
                    Pass("Perfiles guardados y aislados entre workspaces.");
                else
                {
                    Fail("El guardado de perfiles no quedó aislado o no persistió.");
                    failures++;
                }
            }
            finally
            {
                if (Directory.Exists(tempPremiumRoot))
                    Directory.Delete(tempPremiumRoot, true);
                if (Directory.Exists(tempNoPremiumRoot))
                    Directory.Delete(tempNoPremiumRoot, true);
            }

            Pass("Manifiesto oficial, Fabric y detección de Java ejecutados.");
        }
        catch (Exception ex)
        {
            Fail("Error en las comprobaciones de Minecraft Java: " + ex.Message);
            failures++;
        }

        Line(string.Empty);
        Line("========================================");
        Line(failures == 0
            ? "RESULTADO: correcto (las funciones que requieren Minecraft se omiten si no está instalado)."
            : $"RESULTADO: {failures} comprobación(es) con problemas.");
        Line($"Registro: {LogService.CurrentFile ?? Paths.LogsDirectory}");

        var report = _output.ToString();
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "selftest-report.txt");
            await File.WriteAllTextAsync(path, report, cancellationToken);
            Line($"Informe guardado en: {path}");
        }
        catch
        {
            // Console output is still available.
        }

        Console.WriteLine(report);
        return failures == 0 ? 0 : 2;
    }

    private static bool IsElevated()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    private void Line(string text) => _output.AppendLine(text);
    private void Pass(string text) => Line("  [OK] " + text);
    private void Fail(string text) => Line("  [ERROR] " + text);
}
