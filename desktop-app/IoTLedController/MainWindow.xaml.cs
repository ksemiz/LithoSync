using System.Windows;
using System.Windows.Media;
using IoTLedController.ViewModels;

namespace IoTLedController;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow()
    {
        InitializeComponent();
        _vm = (MainViewModel)DataContext;

        // İlk sayfa
        _vm.CurrentPage = "Connect";

        // Bağlantı durumu göstergesi renk güncelleme
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.IsConnected))
            {
                // UI thread'de çalıştır
                Dispatcher.Invoke(() =>
                {
                    // Bağlantı LED göstergesi
                    // Doğrudan XAML DataTrigger ile çözülemiyor, kod-behind yardımıyla
                    var dotBrush = (SolidColorBrush)FindName("connDotBrush")!;
                    if (dotBrush is not null)
                    {
                        // Freeze edilmiş brush ile değiştiremeyiz; yeni brush atayalım
                    }
                });
            }
        };

        Closed += (_, _) => _vm.Dispose();
    }
}
