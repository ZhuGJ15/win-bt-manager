using System.Windows;
using System.ComponentModel;
using System.Windows.Threading;
using WindowsBlueToothManager.Models;
using WindowsBlueToothManager.ViewModels;

namespace WindowsBlueToothManager.Views.Windows;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel = new();
    private readonly DispatcherTimer _refreshTimer = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _refreshTimer.Tick += OnRefreshTimerTick;
        ApplyRefreshTimerInterval();
        _refreshTimer.Start();
        ApplyColumnHeaders();
    }

    protected override void OnClosed(EventArgs e)
    {
        _refreshTimer.Stop();
        _refreshTimer.Tick -= OnRefreshTimerTick;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
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

    private void OnRefreshTimerTick(object? sender, EventArgs e)
    {
        _viewModel.RefreshDevices();
    }
}
