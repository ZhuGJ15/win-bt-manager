using System.Windows;
using System.Windows.Threading;
using WindowsBlueToothManager.Views.Windows;

namespace WindowsBlueToothManager;

public partial class App : System.Windows.Application
{
    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        try
        {
            MainWindow = new MainWindow();
            MainWindow.Show();
        }
        catch (Exception exception)
        {
            ShowStartupError(exception);
            Shutdown(1);
        }
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        ShowStartupError(e.Exception);
        e.Handled = true;
        Current.Shutdown(1);
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            ShowStartupError(exception);
        }
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        ShowStartupError(e.Exception);
        e.SetObserved();
    }

    private static void ShowStartupError(Exception exception)
    {
        System.Windows.MessageBox.Show(
            exception.ToString(),
            "WindowsBlueToothManager startup error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}
