using System;
using WpfApp = System.Windows.Application;
using WpfMsg = System.Windows.MessageBox;

namespace IoTLedController
{
    public partial class App : WpfApp
    {
        public App()
        {
            this.DispatcherUnhandledException += (s, e) =>
            {
                WpfMsg.Show($"Beklenmeyen hata:\n{e.Exception.Message}",
                    "IoT LED Controller",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
                e.Handled = true;
            };
        }
    }
}
