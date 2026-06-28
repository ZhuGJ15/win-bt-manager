using System.Windows;
using System.ComponentModel;
using WindowsBlueToothManager.ViewModels;

namespace WindowsBlueToothManager.Views.Windows;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        ApplyColumnHeaders();
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.RefreshDevices();
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
}
