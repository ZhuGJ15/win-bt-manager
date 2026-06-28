using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using WindowsBlueToothManager.ViewModels;

namespace WindowsBlueToothManager.Views.Windows;

public partial class TaskbarOverlayWindow : Window
{
    private const int GwlStyle = -16;
    private const int GwlExStyle = -20;
    private const long WsChild = 0x40000000L;
    private const long WsPopup = 0x80000000L;
    private const long WsVisible = 0x10000000L;
    private const long WsExNoParentNotify = 0x00000004L;
    private const long WsExTransparent = 0x00000020L;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    private const int HorizontalTaskbarReservedWidth = 360;
    private const int VerticalTaskbarReservedHeight = 160;

    private readonly DispatcherTimer _positionTimer = new();
    private IntPtr _windowHandle;
    private IntPtr _taskbarHandle;

    public TaskbarOverlayWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
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
        AttachAndPosition();
        _positionTimer.Start();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _positionTimer.Stop();
        SourceInitialized -= OnSourceInitialized;
        Loaded -= OnLoaded;
        Closed -= OnClosed;
    }

    private void AttachAndPosition()
    {
        if (_windowHandle == IntPtr.Zero)
        {
            _windowHandle = new WindowInteropHelper(this).Handle;
        }

        _taskbarHandle = FindWindow("Shell_TrayWnd", null);
        if (_windowHandle == IntPtr.Zero || _taskbarHandle == IntPtr.Zero)
        {
            return;
        }

        if (GetParent(_windowHandle) != _taskbarHandle)
        {
            var style = GetWindowLongPtr(_windowHandle, GwlStyle).ToInt64() & 0xFFFFFFFFL;
            style &= ~WsPopup;
            style |= WsChild | WsVisible;
            SetWindowLongPtr(_windowHandle, GwlStyle, new IntPtr(style));

            var extendedStyle = GetWindowLongPtr(_windowHandle, GwlExStyle).ToInt64() & 0xFFFFFFFFL;
            extendedStyle |= WsExTransparent | WsExNoParentNotify;
            SetWindowLongPtr(_windowHandle, GwlExStyle, new IntPtr(extendedStyle));

            SetParent(_windowHandle, _taskbarHandle);
        }

        if (!GetWindowRect(_taskbarHandle, out var rect))
        {
            return;
        }

        var taskbarWidth = Math.Max(1, rect.Right - rect.Left);
        var taskbarHeight = Math.Max(1, rect.Bottom - rect.Top);
        var isHorizontal = taskbarWidth >= taskbarHeight;

        if (isHorizontal)
        {
            var width = Math.Min(280, Math.Max(180, taskbarWidth / 4));
            var height = Math.Min(42, Math.Max(32, taskbarHeight - 2));
            var x = Math.Max(0, taskbarWidth - width - HorizontalTaskbarReservedWidth);
            var y = Math.Max(0, (taskbarHeight - height) / 2);
            Width = width;
            Height = height;
            SetWindowPos(_windowHandle, IntPtr.Zero, x, y, width, height, SwpNoZOrder | SwpNoActivate | SwpShowWindow);
            return;
        }

        var verticalWidth = Math.Min(96, Math.Max(48, taskbarWidth - 2));
        var verticalHeight = Math.Min(220, Math.Max(120, taskbarHeight / 4));
        var verticalX = Math.Max(0, (taskbarWidth - verticalWidth) / 2);
        var verticalY = Math.Max(0, taskbarHeight - verticalHeight - VerticalTaskbarReservedHeight);
        Width = verticalWidth;
        Height = verticalHeight;
        SetWindowPos(_windowHandle, IntPtr.Zero, verticalX, verticalY, verticalWidth, verticalHeight, SwpNoZOrder | SwpNoActivate | SwpShowWindow);
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetParent(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(IntPtr hWnd, out Rect lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

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
            : new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
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
