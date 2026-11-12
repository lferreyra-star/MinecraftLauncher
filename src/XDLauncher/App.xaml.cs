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

        if (!AcquireSingleInstance())
        {
            ActivateExistingWindow();
            Shutdown();
            return;
        }

        try
        {
            // Read the theme before creating any window so the first frame is already themed.
            var settingsService = new SettingsService();
            ThemeManager.Apply(settingsService.Settings.Theme, settingsService.Settings.Accent);

            var dialogs = new DialogService();
            var systemInfo = new SystemInfoService();
            var downloadService = new MinecraftDownloadService();
            _shell = new ShellViewModel(
                settingsService,
                new GameService(),
                new AuthService(),
                new AddonService(),
                new StoreCatalogService(),
                systemInfo,
                dialogs,
                downloadService);

            var window = new MainWindow(_shell);
            MainWindow = window;
            window.Show();

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
