using System.Net.Http;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using XdLauncher.Core;
using XdLauncher.Services;
using XdLauncher.ViewModels;

namespace XdLauncher;

public partial class App : Application
{
    private const string SingleInstanceMutexName = @"Local\XDLauncher.SingleInstance";

    private Mutex? _singleInstanceMutex;
    private ShellViewModel? _shell;
    private Views.LoginWindow? _loginWindow;
    private XdAccount? _pendingHandoffAccount;
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

        if (e.Args.Any(arg => arg.Equals("--capture-settings", StringComparison.OrdinalIgnoreCase)))
        {
            CaptureSettings("appearance", "settings-ultimate.png");
            Shutdown();
            return;
        }

        if (e.Args.Any(arg => arg.Equals("--capture-about", StringComparison.OrdinalIgnoreCase)))
        {
            CaptureSettings("about", "about-rebirth.png");
            Shutdown();
            return;
        }

        SessionHandoff.RegisterProtocol();

        var handoffUri = e.Args.FirstOrDefault(arg =>
            arg.StartsWith(SessionHandoff.Scheme + ":", StringComparison.OrdinalIgnoreCase));

        if (!AcquireSingleInstance())
        {
            // Si ya hay una instancia, se le reenvía el traspaso de sesión y se devuelve el foco.
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
            ApplyVisualSettings(settingsService.Settings);
            ThemeManager.SetCustomAccent(settingsService.Settings.CustomAccentHex);
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

            account ??= TakePendingHandoff();

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

            await AskOnboardingAsync(settingsService, account);
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

        try
        {
            var refreshToken = XdSessionStore.UnprotectToken(stored.ProtectedRefreshToken);
            if (!string.IsNullOrWhiteSpace(refreshToken))
            {
                var account = await auth.RefreshAsync(refreshToken);
                await PersistRestoredSessionAsync(auth, account);
                return account;
            }

            // Las sesiones de Google no tienen refresh token: se reutiliza el idToken
            // mientras siga vigente y se valida contra el servidor antes de entrar.
            var idToken = XdSessionStore.UnprotectToken(stored.ProtectedIdToken);
            if (!string.IsNullOrWhiteSpace(idToken) && stored.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(2))
            {
                var account = await auth.SignInWithIdTokenAsync(idToken);
                account.Provider = stored.Provider ?? account.Provider;
                await PersistRestoredSessionAsync(auth, account);
                return account;
            }
        }
        catch (XdAuthException ex)
        {
            LogService.Warn("La sesión guardada ya no es válida: " + ex.Message);
            XdSessionStore.Delete();
            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Sin conexión no se borra la sesión: se reintenta en el próximo arranque.
            LogService.Warn("No se pudo validar la sesión guardada por un problema de red: " + ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            LogService.Warn("La sesión guardada ya no es válida: " + ex);
            try
            {
                if (File.Exists(XdSessionStore.SessionFile))
                    File.Copy(XdSessionStore.SessionFile, XdSessionStore.SessionFile + ".invalid", true);
            }
            catch
            {
                // El diagnóstico no debe impedir el arranque.
            }
            XdSessionStore.Delete();
            return null;
        }

        return null;
    }

    private static async Task PersistRestoredSessionAsync(XdAuthService auth, XdAccount account)
    {
        XdSessionStore.Save(account);
        await auth.SyncProfileAsync(account);
        await auth.DownloadAvatarAsync(account);
    }

    /// <summary>Valida el token de sesión traspasado desde la web y abre la sesión.</summary>
    private async Task<XdAccount?> AcceptHandoffAsync(XdAuthService auth, string uri)
    {
        var token = SessionHandoff.ExtractToken(uri);
        if (string.IsNullOrWhiteSpace(token))
            return null;

        try
        {
            var refreshToken = SessionHandoff.ExtractRefreshToken(uri);
            var account = await auth.SignInWithIdTokenAsync(token, refreshToken: refreshToken);
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

        _ = CompleteHandoffAsync(uri);
    }

    private async Task CompleteHandoffAsync(string uri)
    {
        var auth = new XdAuthService();
        var account = await AcceptHandoffAsync(auth, uri);
        if (account is null)
        {
            ToastHub.Error("La sesión recibida del navegador no es válida o ya caducó.");
            return;
        }

        var login = _loginWindow;
        if (login is { IsAuthenticated: false })
        {
            // Si la pantalla de acceso ya está creada, se completa en ella. Si todavía
            // no se ha mostrado (carrera durante el arranque), ShowLoginAsync encontrará
            // la cuenta pendiente antes de abrir la ventana.
            if (login.IsLoaded)
            {
                login.CompleteExternalLogin(account);
                return;
            }

            _pendingHandoffAccount = account;
            return;
        }

        if (_shell is not null)
        {
            var previousEmail = _shell.Account.Email;
            _shell.UpdateAccount(account);
            ToastHub.Success(string.Equals(previousEmail, account.Email, StringComparison.OrdinalIgnoreCase)
                ? "Sesión de XD Launcher sincronizada."
                : $"Sesión cambiada a {account.DisplayName ?? account.Email}.");
            return;
        }

        _pendingHandoffAccount = account;
    }

    /// <summary>Consume la cuenta recibida por handoff mientras arrancaba la interfaz.</summary>
    private XdAccount? TakePendingHandoff()
    {
        var account = _pendingHandoffAccount;
        _pendingHandoffAccount = null;
        return account;
    }

    /// <summary>Muestra la pantalla de acceso y devuelve la cuenta validada.</summary>
    private async Task<XdAccount?> ShowLoginAsync(XdAuthService auth)
    {
        if (TakePendingHandoff() is { } ready)
            return ready;

        var loginViewModel = new ViewModels.LoginViewModel(auth);
        var login = new Views.LoginWindow(loginViewModel);
        _loginWindow = login;

        var tcs = new TaskCompletionSource<XdAccount?>();
        loginViewModel.Authenticated += account =>
        {
            _ = auth.DownloadAvatarAsync(account);
            tcs.TrySetResult(account);
            login.Close();
        };
        login.Closed += (_, _) =>
        {
            if (ReferenceEquals(_loginWindow, login))
                _loginWindow = null;

            if (!login.IsAuthenticated)
                tcs.TrySetResult(null);
        };

        login.Show();

        // El handoff puede haber llegado entre la comprobación anterior y este punto.
        if (TakePendingHandoff() is { } late)
            loginViewModel.CompleteExternalLogin(late);

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
        _shell = null;
        window.Close();
        _ = SignInAfterSignOutAsync(settingsService, dialogs, systemInfo, downloadService, updateService,
            updateCheckService);
    }

    private async Task SignInAfterSignOutAsync(SettingsService settingsService, DialogService dialogs,
        SystemInfoService systemInfo, MinecraftDownloadService downloadService, UpdateService updateService,
        UpdateCheckService updateCheckService)
    {
        try
        {
            var account = await ShowLoginAsync(new XdAuthService());
            if (account is null)
            {
                Shutdown();
                return;
            }

            ShowMainWindow(settingsService, dialogs, systemInfo, downloadService, updateService,
                updateCheckService, account);
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "No se pudo volver a la pantalla de acceso.");
            Shutdown(1);
        }
    }

    /// <summary>Runs the first-launch wizard and falls back to the legacy platform question.</summary>
    private async Task AskOnboardingAsync(SettingsService settingsService, XdAccount? account)
    {
        if (settingsService.Settings.OnboardingCompleted || _shell is null)
        {
            await AskPlatformAsync(settingsService);
            return;
        }

        try
        {
            var onboarding = Views.Dialogs.OnboardingWindow.Ask(account?.DisplayName ?? account?.Email);
            if (onboarding.Completed)
            {
                settingsService.Settings.OnboardingCompleted = true;
                settingsService.Settings.OnboardingLanguage = onboarding.Language;
                settingsService.Settings.AskPlatformOnStartup = false;
                settingsService.Save();
                _shell.Navigate(onboarding.InitialSection);
            }
            else
            {
                await AskPlatformAsync(settingsService);
            }
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "No se pudo mostrar el onboarding.");
            await AskPlatformAsync(settingsService);
        }
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

    private static void ApplyVisualSettings(LauncherSettings settings)
    {
        AnimationHelper.AnimationsEnabled = settings.AnimationsEnabled && !settings.PerformanceMode;
        AnimationHelper.AmbientEffectsEnabled = settings.AmbientEffectsEnabled && !settings.PerformanceMode;
        AnimationHelper.AmbientIntensity = settings.AmbientIntensity;
        AnimationHelper.TransparencyLevel = settings.TransparencyLevel;
        AnimationHelper.RespectSystemMotion = settings.RespectSystemMotion;
        AnimationHelper.NavTransitionsEnabled = settings.NavTransitionsEnabled && !settings.PerformanceMode;
        AnimationHelper.ParticleEffectsEnabled = settings.ParticleEffectsEnabled && !settings.PerformanceMode;
        AnimationHelper.HoverEffectsEnabled = settings.HoverEffectsEnabled;
        AnimationHelper.ParallaxEnabled = settings.ParallaxEnabled && !settings.PerformanceMode;
        AnimationHelper.CardAnimationsEnabled = settings.CardAnimationsEnabled && !settings.PerformanceMode;
        AnimationHelper.LoadingAnimationsEnabled = settings.LoadingAnimationsEnabled && !settings.PerformanceMode;
        AnimationHelper.MotionLevel = settings.MotionLevel;
        AnimationHelper.PerformanceMode = settings.PerformanceMode;
        ThemeManager.ApplyTransparency(settings.TransparencyLevel);
    }

    private static void CaptureSettings(string category, string fileName)
    {
        try
        {
            TryAttachToParentConsole();
            var settingsService = new SettingsService();
            var dialogs = new DialogService();
            var systemInfo = new SystemInfoService();
            ApplyVisualSettings(settingsService.Settings);
            ThemeManager.SetCustomAccent(settingsService.Settings.CustomAccentHex);
            ThemeManager.Apply(settingsService.Settings.Theme, settingsService.Settings.Accent);

            var state = new SettingsState(settingsService, dialogs, systemInfo);
            state.RefreshSystemInfo();
            var profile = new ProfileState(new AuthService(), state, dialogs);
            var account = new XdAccount
            {
                UserId = "capture",
                Email = "capture@xd.local",
                DisplayName = "Capture",
                IdToken = "capture",
                Provider = "password"
            };
            var viewModel = new SettingsViewModel(state, profile, account) { SelectedCategory = category };
            var view = new Views.SettingsView
            {
                DataContext = viewModel,
                Width = 1400,
                Height = 900,
                Background = new SolidColorBrush(Color.FromRgb(6, 8, 16))
            };
            view.Measure(new Size(1400, 900));
            view.Arrange(new Rect(0, 0, 1400, 900));
            view.UpdateLayout();

            var bitmap = new RenderTargetBitmap(1400, 900, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(view);
            bitmap.Freeze();
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            var path = Path.Combine(@"C:\Users\EVIL\Documents\xD\captures", fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var stream = File.Create(path);
            encoder.Save(stream);
            Console.WriteLine(path);
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "No se pudo capturar la vista de ajustes.");
            Console.Error.WriteLine(ex);
        }
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
