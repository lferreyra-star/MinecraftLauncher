using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using XdLauncher.Services;
using XdLauncher.ViewModels;

namespace XdLauncher;

public partial class App : Application
{
    private const string SingleInstanceMutexName = @"Local\XDLauncher.SingleInstance";

    private Mutex? _singleInstanceMutex;
    private ShellViewModel? _shell;
    private bool _shuttingDown;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Paths.ApplyConfiguredDataRoot();
        Paths.EnsureLauncherDirectories();
        LogService.Initialize();
        RegisterExceptionHandlers();

        if (e.Args.Any(arg => arg.Equals("--selftest", StringComparison.OrdinalIgnoreCase)))
        {
            await RunSelfTestAsync();
            return;
        }

        SessionHandoff.RegisterProtocol();

        var handoffUri = e.Args.FirstOrDefault(arg =>
            arg.StartsWith(SessionHandoff.Scheme + ":", StringComparison.OrdinalIgnoreCase));

        if (!AcquireSingleInstance())
        {
            // Si ya hay una instancia, se le reenvía el traspaso de sesión y seDvuelve el foco.
            if (handoffUri is not null)
                SessionHandoff.ForwardToRunningInstance(handoffUri);
            ActivateExistingWindow();
            Shutdown();
            return;
        }

        SessionHandoff.StartListener(uri => Dispatcher.Invoke(() => HandleHandoffUri(uri)));

        try
        {
            // La splash y la pantalla de acceso son ventanas auxiliares: la aplicación
            // no puede cerrarse por defecto cuando se cierra una de ellas.
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Read the theme before creating any window so the first frame is already themed.
            var settingsService = new SettingsService();
            ThemeManager.Apply(settingsService.Settings.Theme, settingsService.Settings.Accent);

            var splash = new Views.SplashWindow();
            splash.Show();
            splash.Report("Preparando carpetas y datos…", 0.12);

            var dialogs = new DialogService();
            var systemInfo = new SystemInfoService();
            var downloadService = new MinecraftDownloadService();
            var updateService = new UpdateService();
            var currentVersion = typeof(App).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
            var updateCheckService = new UpdateCheckService("lferreyra-star/MinecraftLauncher", currentVersion);

            var auth = new XdAuthService();
            splash.Report("Conectando con el servidor de XD Launcher…", 0.35);
            await auth.PingAsync();

            splash.Report("Restaurando tu sesión…", 0.6);
            var account = await RestoreSessionAsync(auth);
        LogService.Info(account is null
            ? "No hay sesión local de XD Launcher guardada."
            : $"Sesión local restaurada: {account.Email}");

            if (account is null && handoffUri is not null)
            {
                splash.Report("Validando la sesión recibida del navegador…", 0.75);
                account = await AcceptHandoffAsync(auth, handoffUri);
            }

            if (account is null)
            {
                splash.Report("Iniciando sesión…", 0.85);
                splash.Close();
                account = await ShowLoginAsync(auth);
                if (account is null)
                {
                    Shutdown();
                    return;
                }
            }
            else
            {
                splash.Report("Sesión restaurada. Cargando el launcher…", 0.9);
            }

            splash.Close();
            ShowMainWindow(settingsService, dialogs, systemInfo, downloadService, updateService,
                updateCheckService, account);

            await AskPlatformAsync(settingsService);
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "No se pudo iniciar la interfaz del launcher.");
            MessageBox.Show(
                "XD Launcher no pudo iniciarse." + Environment.NewLine + Environment.NewLine +
                ex.Message + Environment.NewLine + Environment.NewLine +
                $"Revisa el registro en:{Environment.NewLine}{LogService.CurrentFile ?? Paths.LogsDirectory}",
                "XD Launcher",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    /// <summary>Restaura la sesión guardada validando el token contra el servidor.</summary>
    private async Task<XdAccount?> RestoreSessionAsync(XdAuthService auth)
    {
        var stored = XdSessionStore.Load();
        if (stored is null)
            return null;

        var refreshToken = XdSessionStore.UnprotectToken(stored.ProtectedRefreshToken);
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            XdSessionStore.Delete();
            return null;
        }

        try
        {
            var account = await auth.RefreshAsync(refreshToken);
            XdSessionStore.Save(account);
            await auth.SyncProfileAsync(account);
            await auth.DownloadAvatarAsync(account);
            return account;
        }
        catch (Exception ex)
        {
            LogService.Warn("La sesión guardada ya no es válida: " + ex.Message);
            XdSessionStore.Delete();
            return null;
        }
    }

    /// <summary>Valida el token de sesión traspasado desde la web y abre la sesión.</summary>
    private async Task<XdAccount?> AcceptHandoffAsync(XdAuthService auth, string uri)
    {
        var token = SessionHandoff.ExtractToken(uri);
        if (string.IsNullOrWhiteSpace(token))
            return null;

        try
        {
            var account = await auth.SignInWithIdTokenAsync(token);
            XdSessionStore.Save(account);
            await auth.SyncProfileAsync(account);
            await auth.DownloadAvatarAsync(account);
            LogService.Info($"Sesión de XD Launcher recibida desde la web: {account.Email}");
            return account;
        }
        catch (Exception ex)
        {
            LogService.Warn("El traspaso de sesión no fue válido: " + ex.Message);
            return null;
        }
    }

    /// <summary>Procesa una URI de traspaso recibida mientras la aplicación está abierta.</summary>
    private void HandleHandoffUri(string uri)
    {
        LogService.Info("Traspaso de sesión recibido por el canal local.");
        var token = SessionHandoff.ExtractToken(uri);
        if (string.IsNullOrWhiteSpace(token))
        {
            LogService.Warn("El traspaso de sesión no contenía un token.");
            return;
        }

        var auth = new XdAuthService();
        _ = AcceptHandoffAsync(auth, uri).ContinueWith(task =>
        {
            var account = task.Result;
            if (account is null)
            {
                Dispatcher.Invoke(() => ToastHub.Error("La sesión recibida del navegador no es válida."));
                return;
            }

            Dispatcher.Invoke(() => ToastHub.Success($"Sesión iniciada como {account.DisplayName ?? account.Email}."));
        }, TaskScheduler.Default);
    }

    /// <summary>Muestra la pantalla de acceso y devuelve la cuenta validada.</summary>
    private async Task<XdAccount?> ShowLoginAsync(XdAuthService auth)
    {
        var loginViewModel = new ViewModels.LoginViewModel(auth);
        var login = new Views.LoginWindow(loginViewModel);
        login.Show();

        var tcs = new TaskCompletionSource<XdAccount?>();
        loginViewModel.Authenticated += account =>
        {
            _ = auth.DownloadAvatarAsync(account);
            tcs.TrySetResult(account);
            login.Close();
        };
        login.Closed += (_, _) =>
        {
            if (!login.IsAuthenticated)
                tcs.TrySetResult(null);
        };

        return await tcs.Task;
    }

    /// <summary>Crea el shell principal y abre la ventana principal del launcher.</summary>
    private void ShowMainWindow(SettingsService settingsService, DialogService dialogs, SystemInfoService systemInfo,
        MinecraftDownloadService downloadService, UpdateService updateService,
        UpdateCheckService updateCheckService, XdAccount account)
    {
        var shell = new ShellViewModel(
            settingsService,
            new GameService(),
            new AuthService(),
            new AddonService(),
            new StoreCatalogService(),
            systemInfo,
            dialogs,
            downloadService,
            updateService,
            updateCheckService,
            account);
        _shell = shell;

        var window = new MainWindow(shell);
        MainWindow = window;
        shell.SignOutRequested += () => HandleSignOut(window, settingsService, dialogs, systemInfo,
            downloadService, updateService, updateCheckService);
        window.Show();

        // A partir de aquí la ventana principal controla el cierre de la aplicación.
        ShutdownMode = ShutdownMode.OnMainWindowClose;
    }

    /// <summary>Cierra la sesión y devuelve al usuario a la pantalla de acceso.</summary>
    private void HandleSignOut(MainWindow window, SettingsService settingsService, DialogService dialogs,
        SystemInfoService systemInfo, MinecraftDownloadService downloadService, UpdateService updateService,
        UpdateCheckService updateCheckService)
    {
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        window.Close();

        var auth = new XdAuthService();
        var loginViewModel = new ViewModels.LoginViewModel(auth);
        var login = new Views.LoginWindow(loginViewModel);
        login.Show();

        loginViewModel.Authenticated += account =>
        {
            _ = auth.DownloadAvatarAsync(account);
            login.Close();
            ShowMainWindow(settingsService, dialogs, systemInfo, downloadService, updateService,
                updateCheckService, account);
        };
        login.Closed += (_, _) =>
        {
            if (!login.IsAuthenticated)
                Shutdown();
        };

        login.Closed += (_, _) =>
        {
            if (!login.IsAuthenticated)
                Shutdown();
        };
    }

    /// <summary>Offers the Windows game or the phone stream the first time the launcher opens.</summary>
    private async Task AskPlatformAsync(SettingsService settingsService)
    {
        try
        {
            if (!settingsService.Settings.AskPlatformOnStartup || _shell is null)
                return;

            var dialog = Views.Dialogs.PlatformChoiceDialog.Ask();
            switch (dialog.Choice)
            {
                case Views.Dialogs.PlatformChoice.Mobile:
                    _shell.Navigate("mobile");
                    break;
                case Views.Dialogs.PlatformChoice.Windows:
                    _shell.Navigate("home");
                    break;
            }

            if (settingsService.Settings.AskPlatformOnStartup != dialog.AskAgain)
            {
                settingsService.Settings.AskPlatformOnStartup = dialog.AskAgain;
                settingsService.Save();
            }
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "No se pudo mostrar el selector de plataforma.");
        }

        await Task.CompletedTask;
    }

    private async Task RunSelfTestAsync()
    {
        TryAttachToParentConsole();
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var exitCode = 1;
        try
        {
            exitCode = await new SelfTestService().RunAsync();
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "El autodiagnóstico falló de forma inesperada.");
            Console.Error.WriteLine("Autodiagnóstico fallido: " + ex);
            exitCode = 1;
        }
        finally
        {
            Shutdown(exitCode);
        }
    }

    private bool AcquireSingleInstance()
    {
        try
        {
            _singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var createdNew);
            if (!createdNew)
            {
                _singleInstanceMutex.Dispose();
                _singleInstanceMutex = null;
            }
            return createdNew;
        }
        catch (Exception ex)
        {
            // If the mutex cannot be created, start normally instead of blocking the user.
            LogService.Error(ex, "No se pudo crear el mutex de instancia única.");
            return true;
        }
    }

    private static void ActivateExistingWindow()
    {
        var handle = FindWindow(null, AppInfo.WindowTitle);
        if (handle == IntPtr.Zero)
            return;

        if (IsIconic(handle))
            ShowWindow(handle, SW_RESTORE);

        SetForegroundWindow(handle);
    }

    private void RegisterExceptionHandlers()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                LogService.Error(ex, "Excepción no controlada.");
            else
                LogService.Error("Excepción no controlada sin objeto Exception.");
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            LogService.Error(args.Exception, "Tarea no observada.");
            args.SetObserved();
        };
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogService.Error(e.Exception, "Excepción no controlada en la interfaz.");

        if (e.Exception is System.Windows.Markup.XamlParseException xaml)
        {
            LogService.Error($"XAML: {xaml.BaseUri} línea {xaml.LineNumber} posición {xaml.LinePosition} · {xaml.Message}");
        }

        if (_shuttingDown)
            return;

        e.Handled = true;
        var result = MessageBox.Show(
            "Se produjo un error inesperado, pero el launcher puede continuar." + Environment.NewLine + Environment.NewLine +
            e.Exception.Message + Environment.NewLine + Environment.NewLine +
            "¿Quieres cerrar el launcher?" + Environment.NewLine +
            $"El registro está en:{Environment.NewLine}{LogService.CurrentFile ?? Paths.LogsDirectory}",
            "XD Launcher",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            _shuttingDown = true;
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _shell?.Shutdown();
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        _singleInstanceMutex = null;

        LogService.Info($"XD Launcher finalizado con código {e.ApplicationExitCode}.");
        base.OnExit(e);
    }

    private static void TryAttachToParentConsole()
    {
        try
        {
            AttachConsole(AttachParentProcess);
        }
        catch
        {
            // A WinExe has no console by default; the report file is always written.
        }
    }

    private const int AttachParentProcess = -1;
    private const int SW_RESTORE = 9;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr FindWindow(string? className, string? windowName);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr handle);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr handle, int command);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr handle);
}
