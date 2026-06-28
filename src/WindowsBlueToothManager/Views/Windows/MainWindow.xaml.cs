using System.Windows;
using WindowsBlueToothManager.ViewModels;

namespace WindowsBlueToothManager.Views.Windows;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.RefreshDevices();
    }
}
