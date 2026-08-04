using System.Windows;
using IoTLedController.ViewModels;

namespace IoTLedController;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // Global exception handler
        DispatcherUnhandledException += (_, ex) =>
        {
            MessageBox.Show($"Beklenmeyen hata:\n{ex.Exception.Message}",
                "IoT LED Controller", MessageBoxButton.OK, MessageBoxImage.Error);
            ex.Handled = true;
        };
    }
}
