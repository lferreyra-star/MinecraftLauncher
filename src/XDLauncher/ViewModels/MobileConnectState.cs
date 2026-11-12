using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using XdLauncher.Core;
using XdLauncher.Models;
using XdLauncher.Services;

namespace XdLauncher.ViewModels;

public enum MobileConnectionStatus
{
    Idle,
    Scanning,
    Connected,
    Streaming,
    Error
}

/// <summary>
/// Owns the whole phone-to-PC pipeline: ADB detection, pairing, scrcpy streaming,
/// FPS/resolution reporting and the optional touch mapping.
/// </summary>
public sealed class MobileConnectState : ObservableObject, IAsyncDisposable
{
    private readonly SettingsService _settingsService;
    private readonly DialogService _dialogs;
    private readonly AdbService _adb = new();
    private readonly ScrcpyService _scrcpy = new();
    private readonly TouchInputService _touch;
    private readonly DispatcherTimer _timer;
    private InputMappingService? _input;
    private ScrcpySession? _session;
    private bool _isBusy;
    private MobileConnectionStatus _status = MobileConnectionStatus.Idle;
    private string _statusMessage = "Sin conexión";
    private AndroidDeviceInfo? _selectedDevice;
    private double _fps;
    private string _resolution = "—";
    private string _adbStatus = "Comprobando ADB…";
    private string _scrcpyStatus = "Comprobando scrcpy…";
    private bool _isFullscreen;
    private bool _touchModeEnabled;
    private bool _autoReconnect = true;
    private int _missedPolls;
    private bool _wasStreaming;
    private bool _audioFallbackTried;
    private DateTimeOffset _sessionStartedAt;

    public MobileConnectState(SettingsService settingsService, DialogService dialogs)
    {
        _settingsService = settingsService;
        _dialogs = dialogs;
        _touch = new TouchInputService(_adb, () => _settingsService.Settings.MobileConnect);
        _autoReconnect = settingsService.Settings.MobileConnect.AutoReconnect;
        _touchModeEnabled = settingsService.Settings.MobileConnect.TouchMappingEnabled;

        ConnectCommand = new AsyncRelayCommand(ConnectAsync, () => !IsBusy);
        PlayCommand = new AsyncRelayCommand(PlayAsync, () => !IsBusy);
        ConnectUsbCommand = new AsyncRelayCommand(ConnectUsbAsync, () => !IsBusy);
        ConnectWifiCommand = new AsyncRelayCommand(ConnectWifiAsync, () => !IsBusy);
        StartStreamCommand = new AsyncRelayCommand(StartStreamAsync, () => !IsBusy && HasDevice);
        StopStreamCommand = new AsyncRelayCommand(StopStreamAsync, () => !IsBusy && IsStreaming);
        FullscreenCommand = new RelayCommand(ToggleFullscreen);
        ConfigureControlsCommand = new RelayCommand(OpenControlsDialog);
        ConfigureTouchCommand = new AsyncRelayCommand(OpenTouchDialogAsync, () => !IsBusy);
        DisconnectCommand = new AsyncRelayCommand(DisconnectAsync, () => !IsBusy);
        InstallToolsCommand = new AsyncRelayCommand(InstallToolsAsync, () => !IsBusy);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsBusy);

        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        _timer.Tick += async (_, _) => await PollAsync();
    }

    #region Collections and simple state

    public ObservableCollection<AndroidDeviceInfo> Devices { get; } = new();
    public ObservableCollection<MobileQualityOption> Qualities { get; } =
    [
        new() { Id = "Native", Label = "Nativa", Description = "Resolución original del teléfono", MaxSize = 0 },
        new() { Id = "HD", Label = "HD 720p", Description = "hasta 1280 px de lado mayor", MaxSize = 1280 },
        new() { Id = "FHD", Label = "Full HD 1080p", Description = "hasta 1920 px de lado mayor", MaxSize = 1920 }
    ];

    public AsyncRelayCommand ConnectCommand { get; }
    public AsyncRelayCommand PlayCommand { get; }
    public AsyncRelayCommand ConnectUsbCommand { get; }
    public AsyncRelayCommand ConnectWifiCommand { get; }
    public AsyncRelayCommand StartStreamCommand { get; }
    public AsyncRelayCommand StopStreamCommand { get; }
    public RelayCommand FullscreenCommand { get; }
    public RelayCommand ConfigureControlsCommand { get; }
    public AsyncRelayCommand ConfigureTouchCommand { get; }
    public AsyncRelayCommand DisconnectCommand { get; }
    public AsyncRelayCommand InstallToolsCommand { get; }
    public AsyncRelayCommand RefreshCommand { get; }

    /// <summary>Raised when the embedded stream window must be attached to a host.</summary>
    public event Action<IntPtr>? StreamWindowAvailable;

    /// <summary>Raised when the stream ends so the host can release the window.</summary>
    public event Action? StreamEnded;

    /// <summary>Raised when the launcher must switch to the full screen layout.</summary>
    public event Action<bool>? FullscreenChanged;

    /// <summary>Raised when a message must be shown to the user.</summary>
    public event Action<string, bool>? NotificationRequested;

    #endregion

    #region Properties

    public MobileConnectionStatus Status
    {
        get => _status;
        private set
        {
            if (SetProperty(ref _status, value))
                OnPropertyChanged(nameof(StatusLabel));
        }
    }

    public string StatusLabel => Status switch
    {
        MobileConnectionStatus.Idle => "Sin conexión",
        MobileConnectionStatus.Scanning => "Buscando dispositivos…",
        MobileConnectionStatus.Connected => "Conectado",
        MobileConnectionStatus.Streaming => "Transmitiendo",
        _ => "Error"
    };

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
                RaiseCommandStates();
        }
    }

    public bool IsStreaming => Status == MobileConnectionStatus.Streaming;

    public bool HasDevice => SelectedDevice is { IsAuthorized: true };

    public AndroidDeviceInfo? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            if (SetProperty(ref _selectedDevice, value))
            {
                OnPropertyChanged(nameof(HasDevice));
                OnPropertyChanged(nameof(DeviceName));
                OnPropertyChanged(nameof(DeviceDetails));
                RaiseCommandStates();
            }
        }
    }

    public string DeviceName => SelectedDevice?.DisplayName ?? "Ningún dispositivo";

    public string DeviceDetails
    {
        get
        {
            if (SelectedDevice is null)
                return "Conecta un teléfono Android por USB o Wi-Fi para empezar.";
            var parts = new List<string> { SelectedDevice.ConnectionLabel, SelectedDevice.AndroidLabel };
            if (!string.IsNullOrWhiteSpace(SelectedDevice.Resolution))
                parts.Add(SelectedDevice.Resolution + " px");
            if (!string.IsNullOrWhiteSpace(SelectedDevice.WifiAddress))
                parts.Add(SelectedDevice.WifiAddress);
            return string.Join(" · ", parts);
        }
    }

    public double Fps
    {
        get => _fps;
        private set
        {
            if (SetProperty(ref _fps, value))
                OnPropertyChanged(nameof(FpsLabel));
        }
    }

    public string FpsLabel => Fps > 0 ? $"{Fps:0} FPS" : "— FPS";

    public string Resolution
    {
        get => _resolution;
        private set => SetProperty(ref _resolution, value);
    }

    public string AdbStatus
    {
        get => _adbStatus;
        private set => SetProperty(ref _adbStatus, value);
    }

    public string ScrcpyStatus
    {
        get => _scrcpyStatus;
        private set => SetProperty(ref _scrcpyStatus, value);
    }

    public bool IsFullscreen
    {
        get => _isFullscreen;
        private set
        {
            if (SetProperty(ref _isFullscreen, value))
                OnPropertyChanged(nameof(FullscreenLabel));
        }
    }

    public string FullscreenLabel => IsFullscreen ? "Salir de pantalla completa" : "PANTALLA COMPLETA";

    public bool TouchModeEnabled
    {
        get => _touchModeEnabled;
        set
        {
            if (!SetProperty(ref _touchModeEnabled, value))
                return;
            _settingsService.Settings.MobileConnect.TouchMappingEnabled = value;
            _settingsService.Save();
            if (_input is not null)
                _input.TouchModeEnabled = value;
            OnPropertyChanged(nameof(TouchModeLabel));
        }
    }

    public string TouchModeLabel => TouchModeEnabled
        ? "Modo táctil activo (WASD y clics se envían como toques)"
        : "Modo nativo (teclado y ratón van al juego)";

    public bool AutoReconnect
    {
        get => _autoReconnect;
        set
        {
            if (!SetProperty(ref _autoReconnect, value))
                return;
            _settingsService.Settings.MobileConnect.AutoReconnect = value;
            _settingsService.Save();
        }
    }

    public string Quality
    {
        get => _settingsService.Settings.MobileConnect.Quality;
        set
        {
            var mobile = _settingsService.Settings.MobileConnect;
            if (mobile.Quality == value)
                return;
            mobile.Quality = value;
            _settingsService.Save();
            OnPropertyChanged();
            OnPropertyChanged(nameof(MaxFpsLabel));
        }
    }

    public int MaxFps
    {
        get => _settingsService.Settings.MobileConnect.MaxFps;
        set
        {
            var mobile = _settingsService.Settings.MobileConnect;
            var clamped = Math.Clamp(value, 15, 240);
            if (mobile.MaxFps == clamped)
                return;
            mobile.MaxFps = clamped;
            _settingsService.Save();
            OnPropertyChanged();
            OnPropertyChanged(nameof(MaxFpsLabel));
        }
    }

    public string MaxFpsLabel => $"{MaxFps} FPS máx.";

    public bool AudioEnabled
    {
        get => _settingsService.Settings.MobileConnect.AudioEnabled;
        set
        {
            var mobile = _settingsService.Settings.MobileConnect;
            if (mobile.AudioEnabled == value)
                return;
            mobile.AudioEnabled = value;
            _settingsService.Save();
            OnPropertyChanged();
        }
    }

    public bool StayAwake
    {
        get => _settingsService.Settings.MobileConnect.StayAwake;
        set
        {
            var mobile = _settingsService.Settings.MobileConnect;
            if (mobile.StayAwake == value)
                return;
            mobile.StayAwake = value;
            _settingsService.Save();
            OnPropertyChanged();
        }
    }

    public bool ShowTouches
    {
        get => _settingsService.Settings.MobileConnect.ShowTouches;
        set
        {
            var mobile = _settingsService.Settings.MobileConnect;
            if (mobile.ShowTouches == value)
                return;
            mobile.ShowTouches = value;
            _settingsService.Save();
            OnPropertyChanged();
        }
    }

    #endregion

    #region Lifecycle

    public async Task InitializeAsync()
    {
        await RefreshToolsAsync();
        await RefreshDevicesAsync();
        _timer.Start();
    }

    private async Task RefreshToolsAsync()
    {
        var adb = await _adb.GetStatusAsync();
        AdbStatus = adb.IsAvailable ? adb.Detail : adb.Detail;

        var scrcpy = await _scrcpy.GetStatusAsync();
        ScrcpyStatus = scrcpy.IsInstalled ? scrcpy.Detail : scrcpy.Detail;
    }

    public async Task RefreshAsync()
    {
        await RefreshDevicesAsync();
    }

    private async Task RefreshDevicesAsync()
    {
        if (IsBusy)
            return;

        var previous = SelectedDevice?.Serial;
        var devices = await _adb.ListDevicesAsync();
        var lastWifi = _settingsService.Settings.MobileConnect.LastWifiAddress;

        Devices.Clear();
        foreach (var device in devices)
        {
            if (!string.IsNullOrWhiteSpace(lastWifi) &&
                device.Serial.Contains(lastWifi, StringComparison.OrdinalIgnoreCase))
                device.IsSelected = true;
            Devices.Add(device);
        }

        var restored = Devices.FirstOrDefault(d => d.Serial == previous);
        if (restored is not null)
        {
            SelectedDevice = restored;
            await _adb.RefreshDetailsAsync(restored);
            OnPropertyChanged(nameof(DeviceDetails));
        }
        else if (SelectedDevice is not null && !Devices.Any(d => d.Serial == SelectedDevice.Serial))
        {
            if (IsStreaming)
            {
                StatusMessage = "Se perdió la conexión con el teléfono.";
                Status = MobileConnectionStatus.Error;
            }
            SelectedDevice = null;
        }

        if (SelectedDevice is null)
        {
            var candidate = Devices.FirstOrDefault(d => d.IsAuthorized);
            if (candidate is not null)
            {
                SelectedDevice = candidate;
                await _adb.RefreshDetailsAsync(candidate);
                OnPropertyChanged(nameof(DeviceDetails));
            }
        }

        if (!IsStreaming && Devices.Count == 0)
        {
            Status = MobileConnectionStatus.Idle;
            StatusMessage = "Busca un teléfono por USB o activa la depuración por Wi-Fi.";
        }
    }

    #endregion

    #region Commands

    private async Task ConnectAsync()
    {
        await RefreshDevicesAsync();
        var authorized = Devices.FirstOrDefault(d => d.IsAuthorized);
        if (authorized is null)
        {
            NotificationRequested?.Invoke(
                "No hay ningún teléfono autorizado. Conecta el cable USB y acepta el aviso de depuración en el móvil, o usa CONECTAR POR WIFI.", true);
            return;
        }

        SelectedDevice = authorized;
        await _adb.RefreshDetailsAsync(authorized);
        Status = MobileConnectionStatus.Connected;
        StatusMessage = $"{authorized.DisplayName} conectado por {authorized.ConnectionLabel}.";
        _settingsService.Settings.MobileConnect.LastSerial = authorized.Serial;
        _settingsService.Save();
    }

    /// <summary>Connects the phone if needed and starts the stream: the "JUGAR" flow.</summary>
    private async Task PlayAsync()
    {
        await ConnectAsync();
        if (SelectedDevice is { IsAuthorized: true })
            await StartStreamAsync();
    }

    private async Task ConnectUsbAsync()
    {
        Status = MobileConnectionStatus.Scanning;
        StatusMessage = "Buscando teléfonos por USB…";
        await RefreshDevicesAsync();

        var usb = Devices.FirstOrDefault(d => d.IsAuthorized && !d.IsWireless);
        if (usb is null)
        {
            var pending = Devices.FirstOrDefault(d => d.IsUnauthorized);
            if (pending is not null)
            {
                Status = MobileConnectionStatus.Error;
                StatusMessage = "El teléfono espera la autorización de depuración USB. Desbloquea la pantalla y acepta el aviso.";
                NotificationRequested?.Invoke("Desbloquea el teléfono y acepta el diálogo de depuración USB.", true);
                return;
            }

            Status = MobileConnectionStatus.Error;
            StatusMessage = "No se detectó ningún teléfono por USB. Conecta el cable y activa la depuración USB.";
            NotificationRequested?.Invoke("No se detectó ningún teléfono por USB.", true);
            return;
        }

        SelectedDevice = usb;
        await _adb.RefreshDetailsAsync(usb);
        Status = MobileConnectionStatus.Connected;
        StatusMessage = $"{usb.DisplayName} conectado por USB.";
        _settingsService.Settings.MobileConnect.LastSerial = usb.Serial;
        _settingsService.Save();
    }

    private async Task ConnectWifiAsync()
    {
        var result = await Views.Dialogs.MobileConnectDialog.ShowAsync(_adb, _settingsService);
        if (result is null)
            return;

        Status = MobileConnectionStatus.Scanning;
        StatusMessage = $"Conectando con {result.Address}…";
        await RefreshDevicesAsync();

        var device = Devices.FirstOrDefault(d =>
            d.Serial.Contains(result.Address.Split(':')[0], StringComparison.OrdinalIgnoreCase));
        if (device is not null && device.IsAuthorized)
        {
            SelectedDevice = device;
            await _adb.RefreshDetailsAsync(device);
            Status = MobileConnectionStatus.Connected;
            StatusMessage = $"{device.DisplayName} conectado por Wi-Fi.";
            _settingsService.Settings.MobileConnect.LastSerial = device.Serial;
            _settingsService.Settings.MobileConnect.LastWifiAddress = result.Address;
            _settingsService.Save();
            return;
        }

        Status = MobileConnectionStatus.Error;
        StatusMessage = "No se pudo conectar por Wi-Fi: " + result.Message;
        NotificationRequested?.Invoke(result.Message, true);
    }

    private async Task StartStreamAsync()
    {
        var device = SelectedDevice;
        if (device is null || !device.IsAuthorized)
        {
            NotificationRequested?.Invoke("Selecciona un teléfono autorizado antes de iniciar la transmisión.", true);
            return;
        }

        IsBusy = true;
        try
        {
            var status = await _scrcpy.GetStatusAsync();
            if (!status.IsInstalled)
            {
                var install = await _scrcpy.InstallLatestAsync();
                if (!install.Ok)
                {
                    NotificationRequested?.Invoke(install.Message, true);
                    return;
                }
                ScrcpyStatus = install.Message;
            }

            _audioFallbackTried = false;
            await StartSessionAsync(BuildOptions(device), CancellationToken.None);

            // Configure the touch bridge and the key hook.
            await _touch.ConfigureAsync(device.Serial);
            _input ??= new InputMappingService(MobileInputCatalog.GetBindings(_settingsService.Settings.MobileConnect));
            _input.Touch = _touch;
            _input.TouchModeEnabled = _settingsService.Settings.MobileConnect.TouchMappingEnabled;
            _input.FullscreenRequested += () => ToggleFullscreen();
            _input.TouchModeToggleRequested += () => TouchModeEnabled = !TouchModeEnabled;
            _input.Start();

            Fps = 0;
            var size = await _adb.GetScreenSizeAsync(device.Serial);
            Resolution = size ?? "—";
            Status = MobileConnectionStatus.Streaming;
            StatusMessage = $"Transmitiendo {device.DisplayName}. Abre Minecraft en el teléfono para jugar.";
            _wasStreaming = true;
            _missedPolls = 0;
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "No se pudo iniciar la transmisión.");
            Status = MobileConnectionStatus.Error;
            StatusMessage = "No se pudo iniciar la transmisión: " + ex.Message;
            NotificationRequested?.Invoke(StatusMessage, true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private ScrcpyOptions BuildOptions(AndroidDeviceInfo device)
    {
        var mobile = _settingsService.Settings.MobileConnect;
        return new ScrcpyOptions
        {
            Serial = device.Serial,
            Quality = mobile.Quality,
            MaxFps = mobile.MaxFps,
            AudioEnabled = mobile.AudioEnabled,
            StayAwake = mobile.StayAwake,
            ShowTouches = mobile.ShowTouches,
            KeyboardMouseEnabled = true,
            WindowTitle = "XD Mobile Connect"
        };
    }

    private async Task StartSessionAsync(ScrcpyOptions options, CancellationToken cancellationToken)
    {
        _session = await _scrcpy.StartAsync(options, cancellationToken);
        _session.FpsUpdated += OnFpsUpdated;
        _session.Exited += OnScrcpyExited;
        _sessionStartedAt = DateTimeOffset.UtcNow;

        if (_session.WindowHandle == IntPtr.Zero)
        {
            StatusMessage = "scrcpy se inició en una ventana independiente; no se pudo incrustar en el launcher.";
            NotificationRequested?.Invoke(StatusMessage, true);
        }
        else
        {
            StreamWindowAvailable?.Invoke(_session.WindowHandle);
        }
    }

    private async Task StopStreamAsync()
    {
        IsBusy = true;
        try
        {
            StopSession();
            Status = SelectedDevice is { IsAuthorized: true }
                ? MobileConnectionStatus.Connected
                : MobileConnectionStatus.Idle;
            StatusMessage = "Transmisión detenida.";
        }
        finally
        {
            IsBusy = false;
        }

        await Task.CompletedTask;
    }

    private void StopSession()
    {
        try
        {
            if (_session is not null)
            {
                _session.FpsUpdated -= OnFpsUpdated;
                _session.Exited -= OnScrcpyExited;
                _session.Dispose();
                _session = null;
            }
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "No se pudo cerrar la sesión de scrcpy.");
        }

        _input?.Stop();
        StreamEnded?.Invoke();
        Fps = 0;
        _wasStreaming = false;
    }

    private async Task DisconnectAsync()
    {
        IsBusy = true;
        try
        {
            StopSession();
            await _touch.ShutdownSessionAsync();

            var device = SelectedDevice;
            if (device is not null)
            {
                if (device.IsWireless)
                    await _adb.DisconnectAsync(device.Serial);
                else if (!string.IsNullOrWhiteSpace(_settingsService.Settings.MobileConnect.LastWifiAddress))
                    await _adb.DisconnectAsync(_settingsService.Settings.MobileConnect.LastWifiAddress);
            }

            await RefreshDevicesAsync();
            Status = MobileConnectionStatus.Idle;
            StatusMessage = "Dispositivo desconectado.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task InstallToolsAsync()
    {
        IsBusy = true;
        try
        {
            var adb = await _adb.InstallPlatformToolsAsync();
            if (!adb.Ok)
            {
                NotificationRequested?.Invoke(adb.Message, true);
            }
            else
            {
                NotificationRequested?.Invoke(adb.Message, false);
            }

            var scrcpy = await _scrcpy.InstallLatestAsync();
            if (!scrcpy.Ok)
                NotificationRequested?.Invoke(scrcpy.Message, true);
            else
                NotificationRequested?.Invoke(scrcpy.Message, false);

            await RefreshToolsAsync();
            await RefreshDevicesAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ToggleFullscreen()
    {
        IsFullscreen = !IsFullscreen;
        FullscreenChanged?.Invoke(IsFullscreen);
    }

    private void OpenControlsDialog()
    {
        var mobile = _settingsService.Settings.MobileConnect;
        var bindings = MobileInputCatalog.GetBindings(mobile);
        var result = Views.Dialogs.ControlsDialog.Show(bindings);
        if (result is null)
            return;

        mobile.KeyBindings = result;
        _settingsService.Save();
        _input?.UpdateBindings(result);
        NotificationRequested?.Invoke("Asignación de teclas guardada.", false);
    }

    private async Task OpenTouchDialogAsync()
    {
        var device = SelectedDevice;
        if (device is null || !device.IsAuthorized)
        {
            NotificationRequested?.Invoke("Conecta un teléfono antes de calibrar los botones táctiles.", true);
            return;
        }

        var mobile = _settingsService.Settings.MobileConnect;
        var targets = MobileInputCatalog.GetTouchTargets(mobile);
        var screenshot = await _adb.CaptureScreenshotAsync(device.Serial);
        var result = Views.Dialogs.TouchConfigDialog.Show(targets, screenshot, mobile.JoystickRadius);
        if (result is null)
            return;

        mobile.TouchTargets = result.Targets;
        mobile.JoystickRadius = result.JoystickRadius;
        _settingsService.Save();
        NotificationRequested?.Invoke("Posiciones táctiles guardadas.", false);
    }

    #endregion

    #region Polling

    private async Task PollAsync()
    {
        if (IsBusy)
            return;

        try
        {
            var devices = await _adb.ListDevicesAsync();
            var streaming = _session?.IsRunning == true;

            if (streaming)
            {
                var stillThere = devices.Any(d => d.Serial == SelectedDevice?.Serial && d.IsAuthorized);
                if (!stillThere)
                {
                    _missedPolls++;
                    StatusMessage = $"Buscando el teléfono… ({_missedPolls}/3)";
                    if (_missedPolls >= 3)
                    {
                        StopSession();
                        Status = MobileConnectionStatus.Error;
                        StatusMessage = "Se perdió la conexión con el teléfono.";
                        if (AutoReconnect)
                            NotificationRequested?.Invoke("Se perdió la conexión. Reconectando…", false);
                    }
                }
                else
                {
                    _missedPolls = 0;
                    if (StatusMessage.StartsWith("Buscando", StringComparison.Ordinal))
                        StatusMessage = "Transmitiendo " + (SelectedDevice?.DisplayName ?? "el teléfono") + ".";
                }

                return;
            }

            var previous = SelectedDevice?.Serial;
            Devices.Clear();
            foreach (var device in devices)
                Devices.Add(device);

            var selected = Devices.FirstOrDefault(d => d.Serial == previous);
            if (selected is not null)
            {
                SelectedDevice = selected;
                OnPropertyChanged(nameof(DeviceDetails));
            }
            else if (Devices.Count == 0 && _wasStreaming)
            {
                _wasStreaming = false;
                Status = MobileConnectionStatus.Idle;
                StatusMessage = "El teléfono se desconectó.";
            }
        }
        catch (Exception ex)
        {
            LogService.Warn("Error en la comprobación de dispositivos: " + ex.Message);
        }
    }

    private void OnFpsUpdated(double fps)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(() => Fps = fps);
            return;
        }

        Fps = fps;
    }

    private void OnScrcpyExited(int code)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(() => HandleScrcpyExit(code));
            return;
        }

        HandleScrcpyExit(code);
    }

    private void HandleScrcpyExit(int code)
    {
        var elapsed = DateTimeOffset.UtcNow - _sessionStartedAt;
        var audioEnabled = _settingsService.Settings.MobileConnect.AudioEnabled;

        // Some PCs have no audio output device; scrcpy then aborts the whole session.
        // Retry once without audio so the picture keeps working.
        if (code != 0 && audioEnabled && !_audioFallbackTried && elapsed < TimeSpan.FromSeconds(25))
        {
            _audioFallbackTried = true;
            LogService.Warn("scrcpy terminó con código " + code + "; se reintenta sin audio.");
            NotificationRequested?.Invoke(
                "Este equipo no pudo abrir el audio del teléfono. La transmisión continuará sin sonido.", false);
            _ = RestartWithoutAudioAsync();
            return;
        }

        StopSession();
        Status = SelectedDevice is { IsAuthorized: true }
            ? MobileConnectionStatus.Connected
            : MobileConnectionStatus.Idle;
        StatusMessage = "La transmisión se detuvo (scrcpy terminó).";
        if (code != 0)
            LogService.Warn("scrcpy terminó con código " + code + ".");
    }

    private async Task RestartWithoutAudioAsync()
    {
        var device = SelectedDevice;
        if (device is null || !device.IsAuthorized)
            return;

        try
        {
            StopSession();
            var options = BuildOptions(device);
            options.AudioEnabled = false;
            await StartSessionAsync(options, CancellationToken.None);
            Status = MobileConnectionStatus.Streaming;
            StatusMessage = $"Transmitiendo {device.DisplayName} (sin audio).";
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "No se pudo reiniciar la transmisión sin audio.");
            Status = MobileConnectionStatus.Error;
            StatusMessage = "No se pudo iniciar la transmisión: " + ex.Message;
            NotificationRequested?.Invoke(StatusMessage, true);
        }
    }

    #endregion

    private void RaiseCommandStates()
    {
        ConnectCommand.RaiseCanExecuteChanged();
        PlayCommand.RaiseCanExecuteChanged();
        ConnectUsbCommand.RaiseCanExecuteChanged();
        ConnectWifiCommand.RaiseCanExecuteChanged();
        StartStreamCommand.RaiseCanExecuteChanged();
        StopStreamCommand.RaiseCanExecuteChanged();
        ConfigureControlsCommand.RaiseCanExecuteChanged();
        ConfigureTouchCommand.RaiseCanExecuteChanged();
        DisconnectCommand.RaiseCanExecuteChanged();
        InstallToolsCommand.RaiseCanExecuteChanged();
        RefreshCommand.RaiseCanExecuteChanged();
    }

    public async ValueTask DisposeAsync()
    {
        _timer.Stop();
        StopSession();
        await _touch.ShutdownSessionAsync();
        _input?.Dispose();
    }
}
