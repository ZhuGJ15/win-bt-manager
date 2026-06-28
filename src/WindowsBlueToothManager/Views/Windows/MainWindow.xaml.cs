using System.Windows;
using System.ComponentModel;
using System.Windows.Threading;
using WindowsBlueToothManager.Infrastructure.Configuration;
using WindowsBlueToothManager.Models;
using WindowsBlueToothManager.ViewModels;
using WinForms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace WindowsBlueToothManager.Views.Windows;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel = new();
    private readonly DispatcherTimer _refreshTimer = new();
    private readonly WinForms.NotifyIcon _notifyIcon = new();
    private readonly WinForms.ToolStripMenuItem _openMenuItem = new();
    private readonly WinForms.ToolStripMenuItem _refreshMenuItem = new();
    private readonly WinForms.ToolStripMenuItem _exitMenuItem = new();
    private TaskbarOverlayWindow? _taskbarOverlayWindow;
    private bool _isExitRequested;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.Devices.CollectionChanged += OnDevicesCollectionChanged;
        _refreshTimer.Tick += OnRefreshTimerTick;
        InitializeTrayIcon();
        InitializeTaskbarOverlayWindow();
        ApplyRefreshTimerInterval();
        _refreshTimer.Start();
        ApplyColumnHeaders();
        _ = _viewModel.RefreshDevicesAsync();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_isExitRequested)
        {
            base.OnClosing(e);
            return;
        }

        if (_viewModel.CloseButtonBehavior == CloseButtonBehavior.Ask)
        {
            var result = ShowCloseBehaviorPrompt();
            if (result == CloseButtonBehavior.MinimizeToTray)
            {
                _viewModel.SetCloseButtonBehavior(CloseButtonBehavior.MinimizeToTray);
                e.Cancel = true;
                Hide();
                return;
            }

            _viewModel.SetCloseButtonBehavior(CloseButtonBehavior.ExitApplication);
            _isExitRequested = true;
            base.OnClosing(e);
            return;
        }

        if (_viewModel.CloseButtonBehavior == CloseButtonBehavior.MinimizeToTray)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        _isExitRequested = true;
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _refreshTimer.Stop();
        _refreshTimer.Tick -= OnRefreshTimerTick;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.Devices.CollectionChanged -= OnDevicesCollectionChanged;
        _taskbarOverlayWindow?.Close();
        _taskbarOverlayWindow = null;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        base.OnClosed(e);
    }

    private void ChineseLanguageMenuItem_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.SetLanguage(AppLanguage.Chinese);
    }

    private void EnglishLanguageMenuItem_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.SetLanguage(AppLanguage.English);
    }

    private void Refresh5SecondsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.SetRefreshInterval(TimeSpan.FromSeconds(5));
    }

    private void Refresh10SecondsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.SetRefreshInterval(TimeSpan.FromSeconds(10));
    }

    private void Refresh30SecondsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.SetRefreshInterval(TimeSpan.FromSeconds(30));
    }

    private void Refresh1MinuteMenuItem_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.SetRefreshInterval(TimeSpan.FromMinutes(1));
    }

    private async void WindowsBluetoothSourceMenuItem_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.SetDataSourceModeAsync(DeviceDataSourceMode.WindowsBluetooth);
    }

    private async void SimulatedSourceMenuItem_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.SetDataSourceModeAsync(DeviceDataSourceMode.Simulated);
    }

    private void AskCloseBehaviorMenuItem_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.SetCloseButtonBehavior(CloseButtonBehavior.Ask);
    }

    private void MinimizeToTrayCloseBehaviorMenuItem_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.SetCloseButtonBehavior(CloseButtonBehavior.MinimizeToTray);
    }

    private void ExitCloseBehaviorMenuItem_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.SetCloseButtonBehavior(CloseButtonBehavior.ExitApplication);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainWindowViewModel.DeviceHeaderText)
            or nameof(MainWindowViewModel.TypeHeaderText)
            or nameof(MainWindowViewModel.StatusHeaderText)
            or nameof(MainWindowViewModel.BatteryHeaderText)
            or nameof(MainWindowViewModel.BottomHeaderText)
            or nameof(MainWindowViewModel.TrayHeaderText)
            or nameof(MainWindowViewModel.DisplayHeaderText)
            or nameof(MainWindowViewModel.UpdatedHeaderText))
        {
            ApplyColumnHeaders();
        }

        if (e.PropertyName is nameof(MainWindowViewModel.SelectedRefreshInterval))
        {
            ApplyRefreshTimerInterval();
        }

        if (e.PropertyName is nameof(MainWindowViewModel.SummaryText)
            or nameof(MainWindowViewModel.LowBatteryDeviceCount)
            or nameof(MainWindowViewModel.ConnectedDeviceCount)
            or nameof(MainWindowViewModel.LastRefreshText)
            or nameof(MainWindowViewModel.SelectedLanguageOption))
        {
            UpdateTrayText();
            ApplyTrayMenuText();
        }
    }

    private void ApplyColumnHeaders()
    {
        DeviceColumn.Header = _viewModel.DeviceHeaderText;
        TypeColumn.Header = _viewModel.TypeHeaderText;
        StatusColumn.Header = _viewModel.StatusHeaderText;
        BatteryColumn.Header = _viewModel.BatteryHeaderText;
        BottomColumn.Header = _viewModel.BottomHeaderText;
        TrayColumn.Header = _viewModel.TrayHeaderText;
        DisplayColumn.Header = _viewModel.DisplayHeaderText;
        UpdatedColumn.Header = _viewModel.UpdatedHeaderText;
    }

    private void ApplyRefreshTimerInterval()
    {
        _refreshTimer.Interval = _viewModel.SelectedRefreshInterval;
    }

    private async void OnRefreshTimerTick(object? sender, EventArgs e)
    {
        await _viewModel.RefreshDevicesAsync();
        UpdateTrayText();
    }

    private void InitializeTaskbarOverlayWindow()
    {
        _taskbarOverlayWindow = new TaskbarOverlayWindow(_viewModel);
        _taskbarOverlayWindow.Show();
    }

    private void InitializeTrayIcon()
    {
        _openMenuItem.Click += (_, _) => ShowFromTray();
        _refreshMenuItem.Click += async (_, _) => await RefreshFromTrayAsync();
        _exitMenuItem.Click += (_, _) => ExitFromTray();

        _notifyIcon.Icon = Drawing.SystemIcons.Application;
        _notifyIcon.Visible = true;
        _notifyIcon.DoubleClick += (_, _) => ShowFromTray();
        _notifyIcon.ContextMenuStrip = new WinForms.ContextMenuStrip();
        _notifyIcon.ContextMenuStrip.Items.AddRange(new WinForms.ToolStripItem[]
        {
            _openMenuItem,
            _refreshMenuItem,
            new WinForms.ToolStripSeparator(),
            _exitMenuItem
        });

        ApplyTrayMenuText();
        UpdateTrayText();
    }

    private void ApplyTrayMenuText()
    {
        var isChinese = _viewModel.CurrentLanguage == AppLanguage.Chinese;
        _openMenuItem.Text = isChinese ? "打开主界面" : "Open";
        _refreshMenuItem.Text = isChinese ? "立即刷新" : "Refresh now";
        _exitMenuItem.Text = isChinese ? "退出" : "Exit";
    }

    private async Task RefreshFromTrayAsync()
    {
        await _viewModel.RefreshDevicesAsync();
        UpdateTrayText();
    }

    private void ShowFromTray()
    {
        Show();
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
    }

    private void ExitFromTray()
    {
        _isExitRequested = true;
        Close();
    }

    private CloseButtonBehavior ShowCloseBehaviorPrompt()
    {
        var isChinese = _viewModel.CurrentLanguage == AppLanguage.Chinese;
        var message = isChinese
            ? "关闭主窗口时，你希望应用后台运行还是直接退出？\n\n选择“是”：后台运行，并记住此选择。\n选择“否”：直接退出应用，并记住此选择。\n\n之后可在“设置 -> 关闭按钮行为”中修改。"
            : "When closing the main window, should the app keep running in the background or exit?\n\nChoose Yes: run in background and remember this choice.\nChoose No: exit the app and remember this choice.\n\nYou can change this later from Settings -> Close button behavior.";
        var title = isChinese ? "关闭按钮行为" : "Close button behavior";
        var result = System.Windows.MessageBox.Show(
            this,
            message,
            title,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        return result == MessageBoxResult.Yes
            ? CloseButtonBehavior.MinimizeToTray
            : CloseButtonBehavior.ExitApplication;
    }

    private void OnDevicesCollectionChanged(object? sender, EventArgs e)
    {
        UpdateTrayText();
    }

    private void UpdateTrayText()
    {
        var tooltipText = BuildTrayTooltipText();
        _notifyIcon.Text = tooltipText.Length > 63 ? tooltipText[..60] + "..." : tooltipText;
    }

    private string BuildTrayTooltipText()
    {
        var connectedCount = _viewModel.ConnectedDeviceCount;
        var lowBatteryCount = _viewModel.LowBatteryDeviceCount;
        var knownBatteryLevels = _viewModel.Devices
            .Where(device => device.BatteryLevel.HasValue)
            .Select(device => device.BatteryLevel.GetValueOrDefault())
            .ToList();
        var lowestBatteryText = knownBatteryLevels.Count > 0
            ? $"{knownBatteryLevels.Min()}%"
            : "-";

        return _viewModel.CurrentLanguage == AppLanguage.Chinese
            ? $"WindowsBlueToothManager：已连接 {connectedCount}，低电量 {lowBatteryCount}，最低 {lowestBatteryText}"
            : $"WindowsBlueToothManager: {connectedCount} connected, {lowBatteryCount} low, min {lowestBatteryText}";
    }
}
