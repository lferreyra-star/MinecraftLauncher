using System.Runtime.InteropServices;
using System.Windows.Input;
using XdLauncher.Models;
using XdLauncher.Services;

namespace XdLauncher.Services;

/// <summary>
/// Low-level keyboard/mouse hook used by the optional touch-mapping mode.
/// The hook is only active while a stream is running and the embedded scrcpy window
/// (or the launcher in full screen) has the focus, so the rest of Windows keeps working
/// normally. Keys that the launcher handles itself are swallowed; every other key is
/// forwarded untouched so scrcpy can deliver it to the phone.
/// </summary>
public sealed class InputMappingService : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WH_MOUSE_LL = 14;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;

    private readonly Dictionary<string, int> _bindings;
    private readonly HashSet<int> _pressedMovementKeys = new();
    private readonly object _gate = new();
    private IntPtr _keyboardHook;
    private IntPtr _mouseHook;
    private HookProc? _keyboardProc;
    private HookProc? _mouseProc;
    private bool _disposed;

    public InputMappingService(Dictionary<string, int> bindings)
    {
        _bindings = new Dictionary<string, int>(bindings, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Raised when a mapped action is pressed or released. (actionId, isDown)</summary>
    public event Action<string, bool>? ActionTriggered;

    /// <summary>Raised when the launcher should toggle its full screen mode.</summary>
    public event Action? FullscreenRequested;

    /// <summary>Raised when the user asks to switch between touch and native control.</summary>
    public event Action? TouchModeToggleRequested;

    /// <summary>Optional touch dispatcher. When null only the launcher shortcuts are handled.</summary>
    public TouchInputService? Touch { get; set; }

    /// <summary>Handle of the embedded scrcpy window. Zero means "any launcher window".</summary>
    public IntPtr TargetWindow { get; set; }

    /// <summary>Handle of the launcher window used when no scrcpy window is embedded yet.</summary>
    public IntPtr LauncherWindow { get; set; }

    /// <summary>When true the mapped keys are translated into touch actions.</summary>
    public bool TouchModeEnabled { get; set; }

    /// <summary>When true the launcher window itself is the focused surface.</summary>
    public bool LauncherFocused { get; set; }

    public bool IsRunning => _keyboardHook != IntPtr.Zero || _mouseHook != IntPtr.Zero;

    public void UpdateBindings(Dictionary<string, int> bindings)
    {
        lock (_gate)
        {
            _bindings.Clear();
            foreach (var pair in bindings)
                _bindings[pair.Key] = pair.Value;
        }
    }

    public void Start()
    {
        if (_disposed || IsRunning)
            return;

        _keyboardProc = OnKeyboardMessage;
        _mouseProc = OnMouseMessage;

        _keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, GetModuleHandle(null), 0);
        if (_keyboardHook == IntPtr.Zero)
            LogService.Warn("No se pudo instalar el hook de teclado (error " + Marshal.GetLastWin32Error() + ").");

        _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, GetModuleHandle(null), 0);
        if (_mouseHook == IntPtr.Zero)
            LogService.Warn("No se pudo instalar el hook de ratón (error " + Marshal.GetLastWin32Error() + ").");

        LogService.Info("Captura de teclas activada (táctil=" + TouchModeEnabled + ").");
    }

    public void Stop()
    {
        if (_keyboardHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = IntPtr.Zero;
        }

        if (_mouseHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_mouseHook);
            _mouseHook = IntPtr.Zero;
        }

        lock (_gate)
            _pressedMovementKeys.Clear();

        _keyboardProc = null;
        _mouseProc = null;
    }

    /// <summary>True when the embedded stream window (or the launcher) currently owns the keyboard focus.</summary>
    private bool HasFocus()
    {
        if (LauncherFocused && TargetWindow == IntPtr.Zero)
            return true;

        var focused = GetForegroundWindow();
        if (focused == IntPtr.Zero)
            return false;

        if (TargetWindow != IntPtr.Zero && focused == TargetWindow)
            return true;

        // The scrcpy window is a child of the launcher window once it is embedded.
        var owner = GetAncestor(focused, GA_ROOT);
        if (TargetWindow != IntPtr.Zero && owner == TargetWindow)
            return true;
        if (LauncherWindow != IntPtr.Zero && owner == LauncherWindow)
            return true;

        return false;
    }

    private IntPtr OnKeyboardMessage(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0)
        {
            try
            {
                var message = wParam.ToInt32();
                var isDown = message is WM_KEYDOWN or WM_SYSKEYDOWN;
                var isUp = message is WM_KEYUP or WM_SYSKEYUP;
                if (isDown || isUp)
                {
                    var key = (int)Marshal.ReadInt32(lParam);
                    var handled = HandleKey(key, isDown);
                    if (handled)
                        return new IntPtr(1);
                }
            }
            catch (Exception ex)
            {
                LogService.Error(ex, "Error en el hook de teclado.");
            }
        }

        return CallNextHookEx(_keyboardHook, code, wParam, lParam);
    }

    private bool HandleKey(int virtualKey, bool isDown)
    {
        if (!HasFocus())
            return false;

        // Launcher shortcuts always win.
        if (virtualKey == 0x7A) // F11
        {
            if (isDown)
                FullscreenRequested?.Invoke();
            return true;
        }

        if (virtualKey == 0x79) // F10
        {
            if (isDown)
                TouchModeToggleRequested?.Invoke();
            return true;
        }

        if (!TouchModeEnabled || Touch is null)
            return false;

        var action = FindAction(virtualKey);
        if (action is null)
            return false;

        if (action.IsMovement)
        {
            lock (_gate)
            {
                if (isDown)
                    _pressedMovementKeys.Add(virtualKey);
                else
                    _pressedMovementKeys.Remove(virtualKey);
            }

            UpdateJoystick();
            return true;
        }

        if (isDown)
        {
            // Key repeat guard: only fire once per press.
            if (action.TouchTargetId is { Length: > 0 })
                Touch.Tap(action.TouchTargetId);
            ActionTriggered?.Invoke(action.Id, true);
        }
        else
        {
            ActionTriggered?.Invoke(action.Id, false);
        }

        return true;
    }

    private InputActionInfo? FindAction(int virtualKey)
    {
        lock (_gate)
        {
            foreach (var pair in _bindings)
            {
                if (pair.Value != virtualKey)
                    continue;
                var action = MobileInputCatalog.FindAction(pair.Key);
                if (action is not null)
                    return action;
            }
        }

        return null;
    }

    private void UpdateJoystick()
    {
        if (Touch is null)
            return;

        double dx = 0;
        double dy = 0;

        lock (_gate)
        {
            if (_pressedMovementKeys.Contains(BindingValue("forward")))
                dy -= 1;
            if (_pressedMovementKeys.Contains(BindingValue("back")))
                dy += 1;
            if (_pressedMovementKeys.Contains(BindingValue("left")))
                dx -= 1;
            if (_pressedMovementKeys.Contains(BindingValue("right")))
                dx += 1;
        }

        if (dx == 0 && dy == 0)
            Touch.ReleaseJoystick();
        else
            Touch.SetJoystick(dx, dy);
    }

    private int BindingValue(string actionId)
    {
        lock (_gate)
            return _bindings.TryGetValue(actionId, out var value) ? value : 0;
    }

    private IntPtr OnMouseMessage(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0)
        {
            try
            {
                if (!TouchModeEnabled || Touch is null || !HasFocus())
                    return CallNextHookEx(_mouseHook, code, wParam, lParam);

                var message = wParam.ToInt32();
                if (message is 0x0201 or 0x0202) // WM_LBUTTONDOWN / WM_LBUTTONUP
                {
                    var target = MobileInputCatalog.FindAction("attack")?.TouchTargetId;
                    if (!string.IsNullOrEmpty(target))
                    {
                        Touch.Tap(target);
                        return new IntPtr(1);
                    }
                }
                else if (message is 0x0204 or 0x0205) // WM_RBUTTONDOWN / WM_RBUTTONUP
                {
                    var target = MobileInputCatalog.FindAction("place")?.TouchTargetId;
                    if (!string.IsNullOrEmpty(target))
                    {
                        Touch.Tap(target);
                        return new IntPtr(1);
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.Error(ex, "Error en el hook de ratón.");
            }
        }

        return CallNextHookEx(_mouseHook, code, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Stop();
    }

    #region Win32

    private delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);

    private const uint GA_ROOT = 2;

    #endregion
}
