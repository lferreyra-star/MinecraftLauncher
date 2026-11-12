using System.Collections.Concurrent;

namespace XdLauncher.Services;

/// <summary>
/// Sends touch events to the phone using the official Android `input` command.
/// It is used only when the user enables the optional touch-mapping mode.
/// </summary>
public sealed class TouchInputService : IAsyncDisposable
{
    private readonly AdbService _adb;
    private readonly Func<Services.MobileConnectSettings> _settings;
    private readonly ConcurrentQueue<TouchJob> _jobs = new();
    private readonly SemaphoreSlim _signal = new(0);

    private CancellationTokenSource? _cts;
    private Task? _worker;
    private AdbShellSession? _shell;
    private string? _serial;
    private int _width;
    private int _height;
    private bool _motionSupported;
    private bool _joystickActive;
    private int _lastJoystickX;
    private int _lastJoystickY;
    private bool _warnedNoMotion;

    public TouchInputService(AdbService adb, Func<Services.MobileConnectSettings> settings)
    {
        _adb = adb;
        _settings = settings;
    }

    public bool IsConfigured => _serial is not null && _width > 0 && _height > 0;
    public string? Serial => _serial;
    public int ScreenWidth => _width;
    public int ScreenHeight => _height;
    public bool MotionEventsSupported => _motionSupported;

    public async Task<bool> ConfigureAsync(string serial, CancellationToken cancellationToken = default)
    {
        await ShutdownSessionAsync();

        var screenshot = await _adb.CaptureScreenshotAsync(serial, cancellationToken);
        if (screenshot is null)
            return false;

        _serial = serial;
        _width = screenshot.Value.Width;
        _height = screenshot.Value.Height;

        _motionSupported = await _adb.SupportsMotionEventsAsync(serial, cancellationToken);
        _shell?.Dispose();
        _shell = _adb.StartShellSession(serial);

        _cts = new CancellationTokenSource();
        _worker = Task.Run(() => WorkerAsync(_cts.Token), CancellationToken.None);
        LogService.Info($"TouchInput listo para {serial}: {_width}x{_height}, motionevent={_motionSupported}.");
        return true;
    }

    public bool IsTargetConfigured(string targetId)
    {
        var target = MobileInputCatalog.FindTarget(targetId);
        if (target is null)
            return false;

        var settings = _settings();
        if (!settings.TouchTargets.TryGetValue(targetId, out var point))
            return false;
        return point.X > 0.001 && point.Y > 0.001;
    }

    public void Tap(string targetId)
    {
        if (!IsConfigured || !IsTargetConfigured(targetId))
            return;

        var settings = _settings();
        var point = settings.TouchTargets[targetId];
        var x = Math.Clamp((int)Math.Round(point.X * _width), 0, Math.Max(0, _width - 1));
        var y = Math.Clamp((int)Math.Round(point.Y * _height), 0, Math.Max(0, _height - 1));
        Enqueue(new TouchJob(TouchJobKind.Tap, x, y));
    }

    /// <summary>dx/dy are normalized direction components in -1..1.</summary>
    public void SetJoystick(double dx, double dy)
    {
        if (!IsConfigured)
            return;

        var settings = _settings();
        if (!settings.TouchTargets.TryGetValue(MobileInputCatalog.Joystick, out var point) ||
            point.X <= 0.001 || point.Y <= 0.001)
            return;

        var magnitude = Math.Sqrt(dx * dx + dy * dy);
        if (magnitude < 0.01)
        {
            Enqueue(new TouchJob(TouchJobKind.Joystick, 0, 0));
            return;
        }

        if (magnitude > 1)
        {
            dx /= magnitude;
            dy /= magnitude;
        }

        var radius = Math.Clamp(settings.JoystickRadius, 0.04, 0.35) * _width;
        var centerX = (int)Math.Round(point.X * _width);
        var centerY = (int)Math.Round(point.Y * _height);
        var targetX = (int)Math.Clamp(centerX + dx * radius, 0, _width - 1);
        var targetY = (int)Math.Clamp(centerY + dy * radius, 0, _height - 1);
        Enqueue(new TouchJob(TouchJobKind.Joystick, targetX, targetY));
    }

    public void ReleaseJoystick()
    {
        if (!IsConfigured || !_joystickActive)
            return;
        Enqueue(new TouchJob(TouchJobKind.JoystickRelease, _lastJoystickX, _lastJoystickY));
    }

    private void Enqueue(TouchJob job)
    {
        if (_cts is null || _cts.IsCancellationRequested)
            return;
        _jobs.Enqueue(job);
        try { _signal.Release(); } catch { /* ignore */ }
    }

    private async Task WorkerAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await _signal.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            while (_jobs.TryDequeue(out var job))
            {
                if (!EnsureShell())
                    continue;

                try
                {
                    Apply(job);
                }
                catch (Exception ex)
                {
                    LogService.Warn("No se pudo enviar un toque al teléfono: " + ex.Message);
                    _shell?.Dispose();
                    _shell = null;
                }
            }
        }
    }

    private bool EnsureShell()
    {
        if (_shell is { IsAlive: true })
            return true;
        if (_serial is null)
            return false;
        _shell = _adb.StartShellSession(_serial);
        return _shell is not null;
    }

    private void Apply(TouchJob job)
    {
        switch (job.Kind)
        {
            case TouchJobKind.Tap:
                _shell!.TryWriteLine($"input tap {job.X} {job.Y}");
                break;

            case TouchJobKind.Joystick:
                if (!_motionSupported)
                {
                    if (!_warnedNoMotion)
                    {
                        _warnedNoMotion = true;
                        ToastHub.Warning("Tu teléfono no admite eventos de movimiento por ADB. Usa el modo teclado/ratón nativo.");
                    }
                    return;
                }

                if (job.X == 0 && job.Y == 0)
                {
                    if (_joystickActive)
                    {
                        _shell!.TryWriteLine($"input motionevent UP {_lastJoystickX} {_lastJoystickY}");
                        _joystickActive = false;
                    }
                    return;
                }

                var settings = _settings();
                if (!settings.TouchTargets.TryGetValue(MobileInputCatalog.Joystick, out var point))
                    return;
                var centerX = (int)Math.Round(point.X * _width);
                var centerY = (int)Math.Round(point.Y * _height);

                if (!_joystickActive)
                {
                    _shell!.TryWriteLine($"input motionevent DOWN {centerX} {centerY}");
                    _joystickActive = true;
                }
                _shell!.TryWriteLine($"input motionevent MOVE {job.X} {job.Y}");
                _lastJoystickX = job.X;
                _lastJoystickY = job.Y;
                break;

            case TouchJobKind.JoystickRelease:
                if (_joystickActive)
                {
                    _shell!.TryWriteLine($"input motionevent UP {job.X} {job.Y}");
                    _joystickActive = false;
                }
                break;
        }
    }

    public async Task ShutdownSessionAsync()
    {
        try
        {
            ReleaseJoystick();
            if (_cts is not null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }

            if (_worker is not null)
            {
                try { await _worker.WaitAsync(TimeSpan.FromSeconds(2)); } catch { /* ignore */ }
                _worker = null;
            }

            _shell?.Dispose();
            _shell = null;
            _serial = null;
            _width = 0;
            _height = 0;
            _motionSupported = false;
            _joystickActive = false;
        }
        catch (Exception ex)
        {
            LogService.Warn("No se pudo cerrar la sesión táctil: " + ex.Message);
        }
    }

    public async ValueTask DisposeAsync() => await ShutdownSessionAsync();

    private enum TouchJobKind
    {
        Tap,
        Joystick,
        JoystickRelease
    }

    private readonly record struct TouchJob(TouchJobKind Kind, int X, int Y);
}
