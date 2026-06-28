using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using WindowsBlueToothManager.ViewModels;

namespace WindowsBlueToothManager.Views.Windows;

public partial class TaskbarOverlayWindow : Window
{
    private const int GwlStyle = -16;
    private const int GwlExStyle = -20;
    private const long WsChild = 0x40000000L;
    private const long WsVisible = 0x10000000L;
    private const long WsExNoParentNotify = 0x00000004L;
    private const long WsExTransparent = 0x00000020L;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    private const int VerticalTaskbarReservedHeight = 160;

    private readonly MainWindowViewModel _viewModel;
    private readonly DispatcherTimer _positionTimer = new();
    private HwndSource? _hwndSource;
    private IntPtr _windowHandle;
    private IntPtr _taskbarHandle;
    private bool _isAdjustingWindow;

    public TaskbarOverlayWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
        _viewModel = viewModel;
        DataContext = viewModel;
        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        Closed += OnClosed;
        _positionTimer.Interval = TimeSpan.FromSeconds(2);
        _positionTimer.Tick += (_, _) => AttachAndPosition();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _windowHandle = new WindowInteropHelper(this).Handle;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!AttachAndPosition())
        {
            System.Windows.MessageBox.Show(
                "任务栏嵌入失败：未找到 Windows 任务栏容器。",
                "WindowsBlueToothManager",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        _hwndSource = HwndSource.FromHwnd(_windowHandle);
        _hwndSource?.AddHook(WpfWndProc);
        _positionTimer.Start();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _positionTimer.Stop();
        _hwndSource?.RemoveHook(WpfWndProc);
        _hwndSource?.Dispose();
        _hwndSource = null;
        if (_windowHandle != IntPtr.Zero)
        {
            SetParent(_windowHandle, IntPtr.Zero);
        }

        SourceInitialized -= OnSourceInitialized;
        Loaded -= OnLoaded;
        Closed -= OnClosed;
    }

    private bool AttachAndPosition()
    {
        if (_windowHandle == IntPtr.Zero)
        {
            _windowHandle = new WindowInteropHelper(this).EnsureHandle();
        }

        _taskbarHandle = FindWindow("Shell_TrayWnd", string.Empty);
        if (_windowHandle == IntPtr.Zero || _taskbarHandle == IntPtr.Zero)
        {
            return false;
        }

        if (GetParent(_windowHandle) != _taskbarHandle)
        {
            var style = GetWindowLongPtr(_windowHandle, GwlStyle).ToInt64() & 0xFFFFFFFFL;
            style |= WsChild | WsVisible;
            SetWindowLongPtr(_windowHandle, GwlStyle, new IntPtr(style));

            var extendedStyle = GetWindowLongPtr(_windowHandle, GwlExStyle).ToInt64() & 0xFFFFFFFFL;
            extendedStyle |= WsExTransparent | WsExNoParentNotify;
            SetWindowLongPtr(_windowHandle, GwlExStyle, new IntPtr(extendedStyle));

            SetParent(_windowHandle, _taskbarHandle);
        }

        AdjustWindowToTaskbar();
        return true;
    }

    private void AdjustWindowToTaskbar()
    {
        if (_isAdjustingWindow)
        {
            return;
        }

        if (_taskbarHandle == IntPtr.Zero || _windowHandle == IntPtr.Zero)
        {
            return;
        }

        try
        {
            _isAdjustingWindow = true;

            if (!GetWindowRect(_taskbarHandle, out var taskbarRect))
            {
                return;
            }

            var taskbarWidth = Math.Max(1, taskbarRect.Right - taskbarRect.Left);
            var taskbarHeight = Math.Max(1, taskbarRect.Bottom - taskbarRect.Top);
            var isHorizontal = taskbarWidth >= taskbarHeight;
            var displayDeviceCount = Math.Clamp(_viewModel.TaskbarOverlayDevices.Count, 1, 4);

            if (isHorizontal)
            {
                var alignment = GetTaskbarAlignment();
                var targetRect = GetTaskbarTargetRect(alignment);
                OverlayItems.HorizontalAlignment = alignment == 0
                    ? System.Windows.HorizontalAlignment.Right
                    : System.Windows.HorizontalAlignment.Left;

                Width = Math.Clamp(displayDeviceCount * 112, 120, 448);
                Height = Math.Min(46, Math.Max(38, taskbarHeight));

                var dpiAdjustedWidth = (int)Math.Ceiling(Width * GetDpiScale());
                var dpiAdjustedHeight = (int)Math.Ceiling(Height * GetDpiScale());
                var x = alignment == 0
                    ? Math.Max(0, targetRect.Left - taskbarRect.Left - dpiAdjustedWidth)
                    : Math.Max(0, targetRect.Left - taskbarRect.Left);
                var y = Math.Max(0, (taskbarHeight - dpiAdjustedHeight) / 2);

                SetWindowPos(
                    _windowHandle,
                    IntPtr.Zero,
                    x,
                    y,
                    dpiAdjustedWidth,
                    dpiAdjustedHeight,
                    SwpNoZOrder | SwpNoActivate | SwpShowWindow);
                return;
            }

            OverlayItems.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
            Width = Math.Min(96, Math.Max(48, taskbarWidth));
            Height = Math.Min(220, Math.Max(120, displayDeviceCount * 48));
            var dpiAdjustedVerticalWidth = (int)Math.Ceiling(Width * GetDpiScale());
            var dpiAdjustedVerticalHeight = (int)Math.Ceiling(Height * GetDpiScale());
            var verticalX = Math.Max(0, (taskbarWidth - dpiAdjustedVerticalWidth) / 2);
            var verticalY = Math.Max(0, taskbarHeight - dpiAdjustedVerticalHeight - VerticalTaskbarReservedHeight);
            SetWindowPos(
                _windowHandle,
                IntPtr.Zero,
                verticalX,
                verticalY,
                dpiAdjustedVerticalWidth,
                dpiAdjustedVerticalHeight,
                SwpNoZOrder | SwpNoActivate | SwpShowWindow);
        }
        finally
        {
            _isAdjustingWindow = false;
        }
    }

    private Rect GetTaskbarTargetRect(int alignment)
    {
        if (alignment == 0)
        {
            var trayNotifyHandle = FindWindowEx(_taskbarHandle, IntPtr.Zero, "TrayNotifyWnd", string.Empty);
            if (trayNotifyHandle != IntPtr.Zero && GetWindowRect(trayNotifyHandle, out var trayRect))
            {
                return trayRect;
            }
        }

        return GetWindowRect(_taskbarHandle, out var taskbarRect) ? taskbarRect : default;
    }

    private IntPtr WpfWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmWindowPosChanged)
        {
            AdjustWindowToTaskbar();
            handled = true;
        }

        return IntPtr.Zero;
    }

    private double GetDpiScale()
    {
        return VisualTreeHelper.GetDpi(this).DpiScaleX;
    }

    private static int GetTaskbarAlignment()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            return 0;
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced");
            return Convert.ToInt32(key?.GetValue("TaskbarAl", 0) ?? 0);
        }
        catch
        {
            return 0;
        }
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string? lpszWindow);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetParent(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(IntPtr hWnd, out Rect lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    private const int WmWindowPosChanged = 0x0047;

    private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
    {
        return IntPtr.Size == 8
            ? GetWindowLongPtr64(hWnd, nIndex)
            : new IntPtr(GetWindowLong32(hWnd, nIndex));
    }

    private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
    {
        return IntPtr.Size == 8
            ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong)
            : new IntPtr(SetWindowLong32(hWnd, nIndex, unchecked((int)dwNewLong.ToInt64())));
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct Rect
    {
        public readonly int Left;
        public readonly int Top;
        public readonly int Right;
        public readonly int Bottom;
    }
}
