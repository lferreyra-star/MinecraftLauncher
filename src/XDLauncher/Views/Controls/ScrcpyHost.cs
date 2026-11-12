using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace XdLauncher.Views.Controls;

/// <summary>
/// Hosts the scrcpy window inside the launcher. scrcpy is an external process, so the
/// window is re-parented with SetParent and resized to match this host.
/// </summary>
public sealed class ScrcpyHost : HwndHost
{
    private const int GWL_STYLE = -16;
    private const int WS_CHILD = 0x40000000;
    private const int WS_VISIBLE = 0x10000000;
    private const int WS_POPUP = unchecked((int)0x80000000);
    private const int WS_CAPTION = 0x00C00000;
    private const int WS_THICKFRAME = 0x00040000;
    private const int WS_MINIMIZEBOX = 0x00020000;
    private const int WS_MAXIMIZEBOX = 0x00010000;
    private const int WS_SYSMENU = 0x00080000;

    private IntPtr _scrcpyWindow;
    private bool _styleAdjusted;

    /// <summary>Handle of the embedded scrcpy window.</summary>
    public IntPtr ScrcpyWindow => _scrcpyWindow;

    /// <summary>Attaches an existing scrcpy top level window to this host.</summary>
    public void AttachWindow(IntPtr handle)
    {
        _scrcpyWindow = handle;
        if (handle == IntPtr.Zero)
            return;

        try
        {
            if (Handle == IntPtr.Zero)
            {
                // The host window is not ready yet; the attach is retried in BuildWindowCore.
                return;
            }

            AttachToHost();
        }
        catch (Exception ex)
        {
            Services.LogService.Error(ex, "No se pudo incrustar la ventana de scrcpy.");
        }
    }

    /// <summary>Detaches the window and gives it back to the desktop.</summary>
    public void DetachWindow()
    {
        if (_scrcpyWindow == IntPtr.Zero)
            return;

        try
        {
            var style = GetWindowLong(_scrcpyWindow, GWL_STYLE);
            SetWindowLong(_scrcpyWindow, GWL_STYLE, (style & ~WS_CHILD) | WS_POPUP);
            SetWindowPos(_scrcpyWindow, IntPtr.Zero, 0, 0, 640, 360, 0x0040);
            SetParent(_scrcpyWindow, IntPtr.Zero);
        }
        catch
        {
            // The window may already be gone.
        }
        finally
        {
            _scrcpyWindow = IntPtr.Zero;
            _styleAdjusted = false;
        }
    }

    private void AttachToHost()
    {
        var host = Handle;
        if (host == IntPtr.Zero || _scrcpyWindow == IntPtr.Zero)
            return;

        var style = GetWindowLong(_scrcpyWindow, GWL_STYLE);
        SetWindowLong(_scrcpyWindow, GWL_STYLE, (style & ~WS_POPUP & ~WS_CAPTION & ~WS_THICKFRAME &
            ~WS_MINIMIZEBOX & ~WS_MAXIMIZEBOX & ~WS_SYSMENU) | WS_CHILD);
        _styleAdjusted = true;

        SetParent(_scrcpyWindow, host);
        SetWindowPos(_scrcpyWindow, IntPtr.Zero, 0, 0, (int)ActualWidth, (int)ActualHeight, 0x0040);
        ShowWindow(_scrcpyWindow, 5); // SW_SHOW
    }

    protected override HandleRef BuildWindowCore(HandleRef hwndParent)
    {
        // The hosted window must be a real child window of the WPF parent.
        var handle = CreateWindowEx(0, "static", "", WS_CHILD | WS_VISIBLE, 0, 0, 1, 1,
            hwndParent.Handle, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        var result = new HandleRef(this, handle);

        if (_scrcpyWindow != IntPtr.Zero)
            AttachToHost();

        return result;
    }

    protected override void DestroyWindowCore(HandleRef hwnd)
    {
        if (_scrcpyWindow != IntPtr.Zero)
        {
            try
            {
                if (_styleAdjusted)
                {
                    var style = GetWindowLong(_scrcpyWindow, GWL_STYLE);
                    SetWindowLong(_scrcpyWindow, GWL_STYLE, (style & ~WS_CHILD) | WS_POPUP);
                }
                SetParent(_scrcpyWindow, IntPtr.Zero);
            }
            catch
            {
                // ignore
            }
        }

        if (hwnd.Handle != IntPtr.Zero)
            DestroyWindow(hwnd.Handle);
    }

    protected override void OnWindowPositionChanged(Rect rcBoundingBox)
    {
        base.OnWindowPositionChanged(rcBoundingBox);
        ResizeChild();
    }

    protected override System.Windows.Size MeasureOverride(System.Windows.Size constraint)
    {
        var width = double.IsInfinity(constraint.Width) ? 640 : constraint.Width;
        var height = double.IsInfinity(constraint.Height) ? 360 : constraint.Height;
        return new System.Windows.Size(width, height);
    }

    protected override System.Windows.Size ArrangeOverride(System.Windows.Size finalSize)
    {
        ResizeChild(finalSize);
        return base.ArrangeOverride(finalSize);
    }

    private void ResizeChild()
    {
        if (ActualWidth > 0 && ActualHeight > 0)
            ResizeChild(new System.Windows.Size(ActualWidth, ActualHeight));
    }

    private void ResizeChild(System.Windows.Size size)
    {
        if (_scrcpyWindow == IntPtr.Zero || size.Width <= 0 || size.Height <= 0)
            return;

        try
        {
            SetWindowPos(_scrcpyWindow, IntPtr.Zero, 0, 0, (int)size.Width, (int)size.Height, 0x0040);
        }
        catch
        {
            // ignore
        }
    }

    #region Win32

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CreateWindowEx(int dwExStyle, string lpClassName, string lpWindowName,
        int dwStyle, int X, int Y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu,
        IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyWindow(IntPtr hWnd);

    #endregion
}
