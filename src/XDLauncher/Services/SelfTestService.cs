using System.Diagnostics;
using System.Net.Http;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using XdLauncher.Core;
using XdLauncher.Models;
using XdLauncher.ViewModels;
using XdLauncher.Views;

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
        Line("[10] Puente local de Google (handoff y timeout)");
        try
        {
            using var bridge = new LocalAuthBridge();
            using var http = new HttpClient
            {
                BaseAddress = new Uri($"http://127.0.0.1:{bridge.Port}"),
                Timeout = TimeSpan.FromSeconds(10)
            };

            var pending = bridge.RequestGoogleTokenAsync(TimeSpan.FromSeconds(8), cancellationToken,
                openBrowser: false);
            await Task.Delay(150, cancellationToken);

            var page = await http.GetAsync("/", cancellationToken);
            var health = await http.GetAsync("/health", cancellationToken);
            Line($"  Página local: {(int)page.StatusCode} · health: {(int)health.StatusCode}");

            var wrongJson = JsonSerializer.Serialize(new { session = "nonce-incorrecto", token = "x" });
            using var wrongContent = new StringContent(wrongJson, Encoding.UTF8, "application/json");
            var wrong = await http.PostAsync("/token", wrongContent, cancellationToken);
            if (wrong.StatusCode == System.Net.HttpStatusCode.Forbidden)
                Pass("El endpoint de token rechaza nonces desconocidos.");
            else
            {
                Fail($"El endpoint de token aceptó un nonce inválido ({(int)wrong.StatusCode}).");
                failures++;
            }

            var goodJson = JsonSerializer.Serialize(new
            {
                session = bridge.Session,
                token = "token-de-autodiagnostico",
                refreshToken = "refresh-de-autodiagnostico"
            });
            using var goodContent = new StringContent(goodJson, Encoding.UTF8, "application/json");
            var delivered = await http.PostAsync("/token", goodContent, cancellationToken);
            var result = await pending.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
            if (delivered.IsSuccessStatusCode &&
                result.IdToken == "token-de-autodiagnostico" &&
                result.RefreshToken == "refresh-de-autodiagnostico")
                Pass("El token y el refresh token llegan al launcher por el puente local.");
            else
            {
                Fail("El puente no entregó el token esperado.");
                failures++;
            }

            using (var timeoutBridge = new LocalAuthBridge())
            {
                var clock = Stopwatch.StartNew();
                try
                {
                    await timeoutBridge.RequestGoogleTokenAsync(TimeSpan.FromMilliseconds(400),
                        cancellationToken, openBrowser: false);
                    Fail("La espera del puente no terminó con TimeoutException.");
                    failures++;
                }
                catch (TimeoutException)
                {
                    if (clock.Elapsed < TimeSpan.FromSeconds(4))
                        Pass($"El timeout del puente completa la espera en {clock.ElapsedMilliseconds} ms.");
                    else
                    {
                        Fail($"El timeout del puente tardó demasiado ({clock.ElapsedMilliseconds} ms).");
                        failures++;
                    }
                }
            }

            using (var cancelBridge = new LocalAuthBridge())
            {
                var clock = Stopwatch.StartNew();
                var waiting = cancelBridge.RequestGoogleTokenAsync(TimeSpan.FromSeconds(10),
                    cancellationToken, openBrowser: false);
                await Task.Delay(150, cancellationToken);
                cancelBridge.Cancel();
                try
                {
                    await waiting.WaitAsync(TimeSpan.FromSeconds(4), cancellationToken);
                    Fail("La cancelación del puente no terminó la espera.");
                    failures++;
                }
                catch (OperationCanceledException)
                {
                    Pass($"La cancelación del puente termina la espera en {clock.ElapsedMilliseconds} ms.");
                }
            }
        }
        catch (Exception ex)
        {
            Fail("Error probando el puente local de Google: " + ex.Message);
            failures++;
        }

        Line(string.Empty);
        Line("[11] Minecraft Java Edition · Premium / No-Premium");
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
        Line("[12] Interfaz XAML y pipeline de animaciones");
        try
        {
            var previousAnimations = AnimationHelper.AnimationsEnabled;
            AnimationHelper.AnimationsEnabled = true;

            _ = new SplashWindow();
            var loginViewModel = new LoginViewModel(new XdAuthService());
            _ = new LoginWindow(loginViewModel);

            var account = new XdAccount
            {
                UserId = "selftest",
                Email = "selftest@xdlauncher.invalid",
                DisplayName = "Auto Test",
                IdToken = "selftest",
                Provider = "password"
            };
            var shell = new ShellViewModel(
                new SettingsService(),
                new GameService(),
                new AuthService(),
                new AddonService(),
                new StoreCatalogService(),
                new SystemInfoService(),
                new DialogService(),
                new MinecraftDownloadService(),
                new UpdateService(),
                new UpdateCheckService("lferreyra-star/MinecraftLauncher", "0.0.0"),
                account);
            var main = new XdLauncher.MainWindow(shell);
            main.RunVisualSmokeTest();
            shell.Shutdown();

            AnimationHelper.AnimationsEnabled = previousAnimations;
            Pass("Splash, acceso, shell y transformaciones RenderTransform se cargan sin excepciones.");
        }
        catch (Exception ex)
        {
            Fail("Error cargando la interfaz o sus animaciones: " + ex.Message);
            failures++;
        }

        Line(string.Empty);
        Line("[13] Catálogo real de mods (Modrinth API)");
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(25));
            var modrinth = new ModrinthService();

            var versions = await modrinth.GetGameVersionsAsync(timeout.Token);
            var loaders = await modrinth.GetLoadersAsync(timeout.Token);
            Line($"  Versiones publicadas: {versions.Count} · loaders: {string.Join(", ", loaders)}");

            var page = await modrinth.SearchAsync("sodium", "1.20.1", "Fabric", null, "Popularidad", 0, 5, timeout.Token);
            if (page.Projects.Count > 0)
            {
                Line($"  Primero: {page.Projects[0].Name} · descargas {page.Projects[0].Downloads:N0}");
                var versionsForProject = await modrinth.GetVersionsAsync(page.Projects[0].Id, "1.20.1", "Fabric", timeout.Token);
                if (versionsForProject.Count > 0)
                    Pass($"Búsqueda real y resolución de versiones correctas ({page.Total} resultados, {versionsForProject.Count} versiones compatibles).");
                else
                    Fail("La API respondió pero no devolvió versiones compatibles para el primer resultado.");
            }
            else
            {
                Fail("La búsqueda real de Modrinth no devolvió resultados.");
                failures++;
            }
        }
        catch (Exception ex)
        {
            Fail("No se pudo consultar Modrinth: " + ex.Message);
            failures++;
        }

        Line(string.Empty);
        Line("[14] Instalación real de un mod desde Modrinth");
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(2));
            var modrinth = new ModrinthService();
            var installer = new ModInstallService(modrinth);

            var page = await modrinth.SearchAsync("sodium", "1.20.1", "Fabric", null, "Popularidad", 0, 3, timeout.Token);
            var project = page.Projects.FirstOrDefault(p => string.Equals(p.Slug, "sodium", StringComparison.OrdinalIgnoreCase))
                          ?? page.Projects.FirstOrDefault();
            if (project is null)
                throw new InvalidOperationException("Modrinth no devolvió ningún proyecto para probar.");

            var tempRoot = Path.Combine(Path.GetTempPath(), "xd-modtest-" + Guid.NewGuid().ToString("N"));
            var workspace = new JavaWorkspace(JavaWorkspaceKind.NoPremium, tempRoot);
            var profile = new JavaProfile { VersionId = "1.20.1", ModLoader = JavaModLoaders.Fabric };
            workspace.EnsureProfileDirectories(profile);

            var result = await installer.InstallAsync(project, "1.20.1", "Fabric", workspace, profile,
                includeOptionalDependencies: false, null, timeout.Token);

            if (result.Installed.Count == 0)
                throw new InvalidOperationException("La instalación no registró ningún mod.");

            var installed = result.Installed[0];
            var installedPath = Path.Combine(workspace.GetModsDirectory(profile), installed.FileName);
            if (!File.Exists(installedPath) || new FileInfo(installedPath).Length <= 0)
                throw new InvalidOperationException("El archivo del mod no se instaló correctamente.");

            var library = installer.GetProfileLibrary(workspace, profile);
            if (library.Count == 0 || !string.Equals(library[0].ProjectId, installed.ProjectId, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("La biblioteca no conservó el mod instalado.");

            var installedSizeKb = new FileInfo(installedPath).Length / 1024;

            installer.SetEnabled(installed, workspace, false);
            if (!File.Exists(installedPath + ".disabled"))
                throw new InvalidOperationException("La desactivación no renombró el archivo.");
            installer.SetEnabled(installed, workspace, true);
            if (!File.Exists(installedPath))
                throw new InvalidOperationException("La reactivación no restauró el archivo.");

            try
            {
                await installer.InstallAsync(project, "1.12.2", "Forge", workspace, profile,
                    includeOptionalDependencies: false, null, timeout.Token);
                Fail("El instalador aceptó una combinación incompatible 1.12.2/Forge.");
                failures++;
            }
            catch (InvalidOperationException)
            {
                Pass("El instalador rechazó una combinación incompatible (1.12.2/Forge).");
            }

            installer.Remove(installed, workspace);
            if (File.Exists(installedPath) || installer.GetProfileLibrary(workspace, profile).Count != 0)
                throw new InvalidOperationException("La eliminación no limpió archivo y biblioteca.");

            Pass($"Instalación real verificada: {installed.Name} · {installedSizeKb} KB");

            try
            {
                if (Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, true);
            }
            catch
            {
                // El directorio temporal se limpiará en el próximo reinicio.
            }
        }
        catch (Exception ex)
        {
            Fail("Error en la instalación real del mod: " + ex.Message);
            failures++;
        }

        Line(string.Empty);
        Line("[15] Dependencias obligatorias reales");
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(2));
            var modrinth = new ModrinthService();
            var installer = new ModInstallService(modrinth);
            var page = await modrinth.SearchAsync("iris", "1.20.1", "Fabric", null, "Popularidad", 0, 5, timeout.Token);
            var project = page.Projects.FirstOrDefault(p => string.Equals(p.Slug, "iris", StringComparison.OrdinalIgnoreCase))
                          ?? page.Projects.FirstOrDefault();
            if (project is null)
                throw new InvalidOperationException("No se encontró un proyecto con dependencias para probar.");

            var tempRoot = Path.Combine(Path.GetTempPath(), "xd-deptest-" + Guid.NewGuid().ToString("N"));
            var workspace = new JavaWorkspace(JavaWorkspaceKind.NoPremium, tempRoot);
            var profile = new JavaProfile { VersionId = "1.20.1", ModLoader = JavaModLoaders.Fabric };
            workspace.EnsureProfileDirectories(profile);

            var result = await installer.InstallAsync(project, "1.20.1", "Fabric", workspace, profile,
                includeOptionalDependencies: false, null, timeout.Token);

            var names = result.Installed.Select(item => item.Name).ToArray();
            if (!names.Any(n => string.Equals(n, project.Name, StringComparison.OrdinalIgnoreCase)) ||
                result.Installed.Count < 2)
                throw new InvalidOperationException("No se instalaron las dependencias obligatorias: " + string.Join(", ", names));

            Pass("Dependencias instaladas automáticamente: " + string.Join(" + ", names));
            try { if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true); } catch { }
        }
        catch (Exception ex)
        {
            Fail("Error probando dependencias reales: " + ex.Message);
            failures++;
        }

        Line(string.Empty);
        Line("[16] Descubrimiento real de modpacks");
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(25));
            var modrinth = new ModrinthService();
            var page = await modrinth.SearchAsync(string.Empty, null, null, null, "Popularidad", 0, 5,
                timeout.Token, projectType: "modpack");
            if (page.Projects.Count == 0)
                throw new InvalidOperationException("Modrinth no devolvió modpacks compatibles.");

            var first = page.Projects[0];
            var versions = await modrinth.GetVersionsAsync(first.Id, null, null, timeout.Token);
            if (!versions.Any(v => v.Files.Any(f => f.Url.EndsWith(".mrpack", StringComparison.OrdinalIgnoreCase))))
                throw new InvalidOperationException("El primer modpack no tiene archivos .mrpack.");

            Pass($"Modpacks reales: {page.Total}. Primero: {first.Name} · {versions.Count} versión(es).");
        }
        catch (Exception ex)
        {
            Fail("Error probando modpacks reales: " + ex.Message);
            failures++;
        }

        Line(string.Empty);
        Line("[17] Temas del centro de ajustes");
        try
        {
            ThemeManager.SetCustomAccent("#FF00FF");
            ThemeManager.Apply("Dark", "Personalizado");
            var brush = System.Windows.Application.Current?.Resources["AccentBrush"] as System.Windows.Media.SolidColorBrush;
            if (brush is null || brush.Color != System.Windows.Media.Colors.Magenta)
                throw new InvalidOperationException("El acento personalizado no se aplicó globalmente.");

            ThemeManager.Apply("Dark", "Verde");
            Pass("El tema personalizado cambia AccentBrush en toda la aplicación.");
        }
        catch (Exception ex)
        {
            Fail("Error probando temas: " + ex.Message);
            failures++;
        }

        Line(string.Empty);
        Line("[18] Sistema visual global");
        try
        {
            ThemeManager.Apply("Light", "Verde");
            var lightBg = System.Windows.Application.Current?.Resources["WindowBackgroundBrush"] as System.Windows.Media.SolidColorBrush;
            ThemeManager.Apply("Dark", "Verde");
            var darkBg = System.Windows.Application.Current?.Resources["WindowBackgroundBrush"] as System.Windows.Media.SolidColorBrush;

            if (lightBg is null || darkBg is null || lightBg.Color == darkBg.Color)
                throw new InvalidOperationException("El fondo global no cambia entre temas.");
            Pass("Temas claros/oscuros aplicados al sistema visual centralizado.");
        }
        catch (Exception ex)
        {
            Fail("Error probando el sistema visual global: " + ex.Message);
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
