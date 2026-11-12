using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Windows.ApplicationModel;
using Windows.Management.Deployment;
using XdLauncher.Models;

namespace XdLauncher.Services;

public sealed class GameSnapshot
{
    public List<GameInstallation> Installations { get; init; } = [];
    public bool IsGameRunning { get; init; }
    public string? Error { get; init; }
    public DateTimeOffset DetectedAt { get; init; } = DateTimeOffset.Now;
    public GameInstallation? Primary { get; init; }
}

public sealed class LaunchResult
{
    public required bool Success { get; init; }
    public required string Message { get; init; }
    public uint ProcessId { get; init; }
    public bool AlreadyRunning { get; init; }
    public Exception? Error { get; init; }
}

/// <summary>
/// Detects and activates Minecraft Bedrock through the official Windows package APIs.
/// It never modifies game files, licenses or DRM.
/// </summary>
public sealed class GameService
{
    private static readonly string[] CorePackageNames =
    [
        Paths.RetailPackageName, Paths.PreviewPackageName
    ];

    public async Task<GameSnapshot> DetectAsync()
    {
        try
        {
            var packages = await Task.Run(EnumeratePackages);
            var running = IsGameRunning();
            var installations = new List<GameInstallation>();

            foreach (var package in packages)
            {
                if (!CorePackageNames.Contains(package.Id.Name, StringComparer.OrdinalIgnoreCase))
                    continue;

                installations.Add(await CreateInstallationAsync(package, packages, running));
            }

            installations = installations
                .OrderBy(i => i.Channel == InstallationChannel.Retail ? 0 : 1)
                .ToList();

            return new GameSnapshot
            {
                Installations = installations,
                IsGameRunning = running,
                Primary = installations.FirstOrDefault(i => i.Channel == InstallationChannel.Retail)
                          ?? installations.FirstOrDefault()
            };
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error detectando Minecraft Bedrock.");
            return new GameSnapshot
            {
                Error = "No se pudo consultar el sistema de paquetes de Windows. " + ex.Message,
                IsGameRunning = IsGameRunning()
            };
        }
    }

    private static List<Package> EnumeratePackages()
    {
        var manager = new PackageManager();
        string userSid;
        try
        {
            userSid = WindowsIdentity.GetCurrent().User?.Value ?? string.Empty;
        }
        catch
        {
            userSid = string.Empty;
        }

        IEnumerable<Package> packages;
        try
        {
            packages = manager.FindPackagesForUser(userSid);
        }
        catch (UnauthorizedAccessException)
        {
            packages = manager.FindPackagesForUser(string.Empty);
        }

        return packages.ToList();
    }

    private static async Task<GameInstallation> CreateInstallationAsync(Package package, List<Package> allPackages, bool running)
    {
        var channel = package.Id.Name.Equals(Paths.PreviewPackageName, StringComparison.OrdinalIgnoreCase)
            ? InstallationChannel.Preview
            : InstallationChannel.Retail;

        var statusText = DescribeStatus(package.Status);
        var healthy = statusText == "Correcto";
        var version = FormatVersion(package.Id.Version);
        var aumid = channel == InstallationChannel.Preview ? Paths.PreviewAumid : Paths.RetailAumid;

        try
        {
            var entries = await package.GetAppListEntriesAsync();
            var entry = entries.FirstOrDefault();
            if (entry is not null && !string.IsNullOrWhiteSpace(entry.AppUserModelId))
                aumid = entry.AppUserModelId;
        }
        catch (Exception ex)
        {
            LogService.Warn($"No se pudo enumerar la entrada de la aplicación: {ex.Message}");
        }

        string installLocation;
        try
        {
            installLocation = package.InstalledLocation.Path;
        }
        catch
        {
            installLocation = string.Empty;
        }

        var dependencies = new List<GameDependency>();
        try
        {
            foreach (var dependency in package.Dependencies)
            {
                dependencies.Add(new GameDependency
                {
                    Name = dependency.Id.Name,
                    Version = FormatVersion(dependency.Id.Version),
                    IsFramework = dependency.IsFramework
                });
            }
        }
        catch (Exception ex)
        {
            LogService.Warn($"No se pudieron leer las dependencias del paquete: {ex.Message}");
        }

        var installation = new GameInstallation
        {
            Channel = channel,
            DisplayName = channel == InstallationChannel.Preview ? "Minecraft Preview" : "Minecraft: Bedrock Edition",
            PackageName = package.Id.Name,
            PackageFullName = package.Id.FullName,
            FamilyName = package.Id.FamilyName,
            Version = version,
            Architecture = package.Id.Architecture.ToString(),
            Publisher = package.PublisherDisplayName,
            InstalledDate = package.InstalledDate,
            InstallLocation = installLocation,
            AppUserModelId = aumid,
            IsHealthy = healthy,
            PackageStatus = statusText,
            SignatureKind = package.SignatureKind.ToString(),
            Dependencies = dependencies,
            IsRunning = running
        };

        installation.Components.AddRange(BuildComponentChecks(installation, allPackages));
        return installation;
    }

    private static List<ComponentCheck> BuildComponentChecks(GameInstallation installation, List<Package> packages)
    {
        var checks = new List<ComponentCheck>
        {
            CheckPackage(packages, "Microsoft.GamingServices", "Servicios de juego",
                "Necesario para instalar y mantener el juego desde Microsoft Store.", true, "9MWPM2CQNLHN", "\uE7FC"),
            CheckPackage(packages, "Microsoft.XboxIdentityProvider", "Xbox Identity Provider",
                "Componente oficial necesario para iniciar sesión con Xbox Live.", true, "9WZDNCRD1HKW", "\uE7FC"),
            CheckPackage(packages, "Microsoft.GamingApp", "Aplicación Xbox",
                "Recomendada para gestionar la cuenta y las funciones sociales de Xbox.", false, "9MV0B5HZVK9Z", "\uE7FC"),
            CheckPackage(packages, "Microsoft.WindowsStore", "Microsoft Store",
                "Se usa para instalar, reparar y actualizar Minecraft de forma oficial.", true, "9WZDNCRFJBMP", "\uE719")
        };

        var vclibs = packages.FirstOrDefault(p =>
            p.Id.Name.Equals("Microsoft.VCLibs.140.00.UWPDesktop", StringComparison.OrdinalIgnoreCase));
        checks.Add(vclibs is null
            ? new ComponentCheck
            {
                Name = "Visual C++ Runtime (UWP)",
                PackageName = "Microsoft.VCLibs.140.00.UWPDesktop",
                Description = "Dependencia oficial del paquete de Minecraft. Se repara desde Store.",
                State = ComponentState.Missing,
                IsRequired = true,
                IconGlyph = "\uE756"
            }
            : new ComponentCheck
            {
                Name = "Visual C++ Runtime (UWP)",
                PackageName = vclibs.Id.Name,
                Description = "Dependencia oficial del paquete de Minecraft.",
                State = ComponentState.Installed,
                Version = FormatVersion(vclibs.Id.Version),
                IsRequired = true,
                IconGlyph = "\uE756"
            });

        var appRuntime = packages.FirstOrDefault(p =>
            p.Id.Name.StartsWith("Microsoft.WindowsAppRuntime.1.8", StringComparison.OrdinalIgnoreCase) ||
            p.Id.Name.StartsWith("Microsoft.WinAppRuntime", StringComparison.OrdinalIgnoreCase));
        checks.Add(appRuntime is null
            ? new ComponentCheck
            {
                Name = "Windows App Runtime 1.8",
                PackageName = "Microsoft.WindowsAppRuntime.1.8",
                Description = "Dependencia oficial declarada por la versión actual de Minecraft.",
                State = ComponentState.Missing,
                IsRequired = true,
                IconGlyph = "\uE8F1"
            }
            : new ComponentCheck
            {
                Name = "Windows App Runtime 1.8",
                PackageName = appRuntime.Id.Name,
                Description = "Dependencia oficial declarada por la versión actual de Minecraft.",
                State = ComponentState.Installed,
                Version = FormatVersion(appRuntime.Id.Version),
                IsRequired = true,
                IconGlyph = "\uE8F1"
            });

        // Package dependencies are authoritative when available.
        foreach (var dependency in installation.Dependencies)
        {
            var check = checks.FirstOrDefault(c =>
                c.PackageName.Equals(dependency.Name, StringComparison.OrdinalIgnoreCase));
            if (check is { State: ComponentState.Missing })
            {
                var index = checks.IndexOf(check);
                checks[index] = new ComponentCheck
                {
                    Name = check.Name,
                    PackageName = dependency.Name,
                    Description = check.Description,
                    State = ComponentState.Installed,
                    Version = dependency.Version,
                    IsRequired = check.IsRequired,
                    StoreProductId = check.StoreProductId,
                    IconGlyph = check.IconGlyph
                };
            }
        }

        return checks;
    }

    private static ComponentCheck CheckPackage(List<Package> packages, string name, string displayName,
        string description, bool required, string storeId, string icon)
    {
        var package = packages.FirstOrDefault(p =>
            p.Id.Name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
            p.Id.Name.StartsWith(name + ".", StringComparison.OrdinalIgnoreCase));

        return package is null
            ? new ComponentCheck
            {
                Name = displayName,
                PackageName = name,
                Description = description,
                State = ComponentState.Missing,
                IsRequired = required,
                StoreProductId = storeId,
                IconGlyph = icon
            }
            : new ComponentCheck
            {
                Name = displayName,
                PackageName = package.Id.Name,
                Description = description,
                State = ComponentState.Installed,
                Version = FormatVersion(package.Id.Version),
                IsRequired = required,
                StoreProductId = storeId,
                IconGlyph = icon
            };
    }

    public bool IsGameRunning()
    {
        try
        {
            var processes = Process.GetProcessesByName("Minecraft.Windows");
            try
            {
                return processes.Any(p => !p.HasExited);
            }
            finally
            {
                foreach (var process in processes)
                    process.Dispose();
            }
        }
        catch
        {
            return false;
        }
    }

    public async Task<LaunchResult> LaunchAsync(GameInstallation installation)
    {
        if (IsGameRunning())
        {
            var focused = TryBringToFront(installation);
            return new LaunchResult
            {
                Success = true,
                AlreadyRunning = true,
                Message = focused
                    ? "Minecraft ya está en ejecución. Se ha solicitado traerlo al frente."
                    : "Minecraft ya está en ejecución."
            };
        }

        var aumid = installation.Aumid;
        LogService.Info($"Activando Minecraft: {aumid} ({installation.Version})");

        try
        {
            var manager = (IApplicationActivationManager)new ApplicationActivationManager();
            var hr = manager.ActivateApplication(aumid, null, ActivateOptions.None, out var processId);
            if (hr < 0)
                Marshal.ThrowExceptionForHR(hr);

            LogService.Info($"Activación COM correcta. PID: {processId}");
            await WaitForGameProcessAsync(TimeSpan.FromSeconds(8));
            var running = IsGameRunning();
            return new LaunchResult
            {
                Success = true,
                ProcessId = processId,
                Message = running
                    ? "Minecraft se está ejecutando."
                    : "Minecraft se está iniciando. Puede tardar unos segundos."
            };
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException or Win32Exception or UnauthorizedAccessException)
        {
            LogService.Warn($"La activación COM falló ({ex.Message}). Se usará el mecanismo de shell oficial.");
            return await LaunchViaShellAsync(installation);
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "No se pudo iniciar Minecraft.");
            return new LaunchResult
            {
                Success = false,
                Message = "No se pudo iniciar Minecraft: " + ex.Message,
                Error = ex
            };
        }
    }

    private async Task<LaunchResult> LaunchViaShellAsync(GameInstallation installation)
    {
        try
        {
            // shell:AppsFolder uses the same official Windows application activation path as Start menu.
            var startInfo = new ProcessStartInfo("explorer.exe", $@"shell:AppsFolder\{installation.Aumid}")
            {
                UseShellExecute = true
            };
            Process.Start(startInfo);

            await WaitForGameProcessAsync(TimeSpan.FromSeconds(10));
            var running = IsGameRunning();
            return new LaunchResult
            {
                Success = true,
                Message = running
                    ? "Minecraft se está ejecutando."
                    : "Se solicitó el inicio de Minecraft. Si no aparece, ábrelo una vez desde el menú Inicio para registrar la aplicación."
            };
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "El mecanismo de shell tampoco pudo iniciar Minecraft.");
            return new LaunchResult
            {
                Success = false,
                Message = "No se pudo iniciar Minecraft. Instálalo o repáralo desde Microsoft Store. Detalle: " + ex.Message,
                Error = ex
            };
        }
    }

    /// <summary>
    /// Activates the running packaged app (Windows restores and foregrounds it) and then
    /// reinforces the foreground state through the main window handle when available.
    /// </summary>
    public bool TryBringToFront(GameInstallation installation)
    {
        var activated = false;
        try
        {
            var manager = (IApplicationActivationManager)new ApplicationActivationManager();
            var hr = manager.ActivateApplication(installation.Aumid, null, ActivateOptions.None, out _);
            activated = hr >= 0;
        }
        catch (Exception ex)
        {
            LogService.Warn($"No se pudo reactivar Minecraft para traerlo al frente: {ex.Message}");
        }

        try
        {
            foreach (var process in Process.GetProcessesByName("Minecraft.Windows"))
            {
                try
                {
                    process.Refresh();
                    var handle = process.MainWindowHandle;
                    if (handle == IntPtr.Zero)
                        continue;

                    ShowWindow(handle, SW_RESTORE);
                    SetForegroundWindow(handle);
                    activated = true;
                }
                finally
                {
                    process.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            LogService.Warn($"No se pudo enfocar la ventana de Minecraft: {ex.Message}");
        }

        return activated;
    }

    private static async Task WaitForGameProcessAsync(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var processes = Process.GetProcessesByName("Minecraft.Windows");
                try
                {
                    if (processes.Any(p => !p.HasExited))
                        return;
                }
                finally
                {
                    foreach (var process in processes)
                        process.Dispose();
                }
            }
            catch
            {
                // keep polling
            }

            await Task.Delay(400);
        }
    }

    public async Task<VerificationReport> VerifyAsync(GameInstallation installation)
    {
        var report = new VerificationReport();
        var snapshot = await DetectAsync();
        var fresh = snapshot.Installations.FirstOrDefault(i =>
            i.FamilyName.Equals(installation.FamilyName, StringComparison.OrdinalIgnoreCase));

        if (fresh is null)
        {
            report.Items.Add(new VerificationItem
            {
                Name = "Paquete de Minecraft",
                State = VerificationState.Error,
                Detail = "Windows ya no encuentra el paquete. Puede haberse desinstalado o movido."
            });
            return report;
        }

        report.Items.Add(new VerificationItem
        {
            Name = "Paquete registrado",
            State = VerificationState.Ok,
            Detail = $"{fresh.PackageFullName}"
        });

        report.Items.Add(new VerificationItem
        {
            Name = "Estado del paquete",
            State = fresh.IsHealthy ? VerificationState.Ok : VerificationState.Error,
            Detail = fresh.PackageStatus
        });

        report.Items.Add(new VerificationItem
        {
            Name = "Firma digital",
            State = fresh.SignatureKind.Contains("Store", StringComparison.OrdinalIgnoreCase)
                ? VerificationState.Ok : VerificationState.Warning,
            Detail = $"Tipo de firma: {fresh.SignatureKind}"
        });

        if (!string.IsNullOrWhiteSpace(fresh.InstallLocation) && Directory.Exists(fresh.InstallLocation))
        {
            var executable = Path.Combine(fresh.InstallLocation, "Minecraft.Windows.exe");
            try
            {
                report.Items.Add(new VerificationItem
                {
                    Name = "Ejecutable del juego",
                    State = File.Exists(executable) ? VerificationState.Ok : VerificationState.Warning,
                    Detail = File.Exists(executable) ? "Minecraft.Windows.exe presente." : "No se encontró el ejecutable en la carpeta del paquete."
                });
            }
            catch (UnauthorizedAccessException)
            {
                report.Items.Add(new VerificationItem
                {
                    Name = "Archivos del juego",
                    State = VerificationState.Unknown,
                    Detail = "Windows Apps está protegida. El launcher no necesita modificar ni leer los archivos internos."
                });
            }
        }
        else
        {
            report.Items.Add(new VerificationItem
            {
                Name = "Ubicación de instalación",
                State = VerificationState.Unknown,
                Detail = "La ruta no es accesible desde una aplicación sin privilegios. Es normal en instalaciones MSIXVC."
            });
        }

        foreach (var component in fresh.Components)
        {
            report.Items.Add(new VerificationItem
            {
                Name = component.Name,
                State = component.State == ComponentState.Installed ? VerificationState.Ok
                    : component.IsRequired ? VerificationState.Error : VerificationState.Warning,
                Detail = component.State == ComponentState.Installed
                    ? $"Instalado{(string.IsNullOrWhiteSpace(component.Version) ? string.Empty : $" · {component.Version}")}"
                    : "Componente no detectado."
            });
        }

        var build = Environment.OSVersion.Version.Build;
        report.Items.Add(new VerificationItem
        {
            Name = "Versión de Windows",
            State = build >= 17763 ? VerificationState.Ok : VerificationState.Error,
            Detail = build >= 22000 ? $"Windows 11 (build {build})" : $"Windows 10 (build {build})"
        });

        report.Items.Add(new VerificationItem
        {
            Name = "Cuenta de Microsoft",
            State = VerificationState.Unknown,
            Detail = "Xbox Live se autentica dentro del juego. El launcher nunca solicita contraseñas del juego."
        });

        return report;
    }

    public UpdateInfo ComputeUpdate(GameInstallation? installation, StoreProduct? product)
    {
        if (installation is null)
        {
            return new UpdateInfo
            {
                Availability = UpdateAvailability.Unknown,
                Notes = product?.Notes,
                StoreUpdatedAt = product?.LastUpdated,
                StoreUrl = product?.StoreUrl
            };
        }

        if (product?.LastUpdated is null)
        {
            return new UpdateInfo
            {
                Availability = UpdateAvailability.Unknown,
                LocalInstalledAt = installation.InstalledDate,
                StoreUrl = product?.StoreUrl,
                Notes = product?.Notes
            };
        }

        // InstalledDate is the package registration date. Store metadata can lag or lead by a few hours.
        var available = product.LastUpdated.Value > installation.InstalledDate.AddHours(24);
        return new UpdateInfo
        {
            Availability = available ? UpdateAvailability.Available : UpdateAvailability.UpToDate,
            LocalInstalledAt = installation.InstalledDate,
            StoreUpdatedAt = product.LastUpdated,
            Notes = product.Notes,
            StoreUrl = product.StoreUrl
        };
    }

    public void OpenStore() => FileOps.OpenStoreProduct(AppInfo.MinecraftProductId);
    public void OpenUpdates() => FileOps.OpenStoreUpdates();
    public void OpenComponentStore(string productId) => FileOps.OpenStoreProduct(productId);

    private static string FormatVersion(PackageVersion version)
        => $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";

    private static string DescribeStatus(Windows.ApplicationModel.PackageStatus status)
    {
        var issues = new List<string>();
        if (status.NotAvailable) issues.Add("no disponible");
        if (status.PackageOffline) issues.Add("paquete sin conexión");
        if (status.DataOffline) issues.Add("datos sin conexión");
        if (status.Disabled) issues.Add("deshabilitado");
        if (status.DependencyIssue) issues.Add("dependencia con problemas");
        if (status.LicenseIssue) issues.Add("licencia con problemas");
        if (status.Modified) issues.Add("modificado");
        if (status.Tampered) issues.Add("manipulado");
        if (status.NeedsRemediation) issues.Add("requiere reparación");
        if (status.Servicing) issues.Add("en mantenimiento");
        if (issues.Count > 0)
            return string.Join(", ", issues);

        try
        {
            return status.VerifyIsOK() ? "Correcto" : "Requiere atención";
        }
        catch
        {
            return "Requiere atención";
        }
    }

    private const int SW_RESTORE = 9;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private enum ActivateOptions
    {
        None = 0x00000000,
        DesignMode = 0x00000001,
        TestMode = 0x00000002,
        NoErrorUI = 0x00000004
    }

    [ComImport]
    [Guid("2e941141-7f97-4756-ba1d-9decde894a3d")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IApplicationActivationManager
    {
        [PreserveSig]
        int ActivateApplication(
            [MarshalAs(UnmanagedType.LPWStr)] string appUserModelId,
            [MarshalAs(UnmanagedType.LPWStr)] string? arguments,
            ActivateOptions options,
            out uint processId);

        [PreserveSig]
        int ActivateForFile(
            [MarshalAs(UnmanagedType.LPWStr)] string appUserModelId,
            IntPtr itemArray,
            [MarshalAs(UnmanagedType.LPWStr)] string verb,
            out uint processId);

        [PreserveSig]
        int ActivateForProtocol(
            [MarshalAs(UnmanagedType.LPWStr)] string appUserModelId,
            IntPtr itemArray,
            out uint processId);
    }

    [ComImport]
    [Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C")]
    [ClassInterface(ClassInterfaceType.None)]
    private class ApplicationActivationManager;
}
