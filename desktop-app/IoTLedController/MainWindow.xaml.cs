using System.Windows;
using System.Windows.Media;
using IoTLedController.ViewModels;
using MediaColor = System.Windows.Media.Color;
using MediaColorConverter = System.Windows.Media.ColorConverter;

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
                Dispatcher.Invoke(() =>
                {
                    var dotBrush = FindName("connDotBrush") as SolidColorBrush;
                    if (dotBrush != null)
                    {
                        dotBrush.Color = _vm.IsConnected 
                            ? (MediaColor)MediaColorConverter.ConvertFromString("#2ECC71") 
                            : (MediaColor)MediaColorConverter.ConvertFromString("#E74C3C");
                    }
                });
            }
        };
    }
}
