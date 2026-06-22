using PowerMonitor.Services;
using System.Windows;
using System.Windows.Media;

namespace PowerMonitor;

public partial class OverlayWindow : Window
{
    public OverlayWindow()
    {
        InitializeComponent();

        var screenWidth = SystemParameters.PrimaryScreenWidth;
        Left = screenWidth - Width - 16;
        Top = 12;

        Loaded += (_, _) =>
        {
            Left = SystemParameters.PrimaryScreenWidth - Width - 16;
        };
    }

    public void UpdatePower(PowerData data)
    {
        Dispatcher.Invoke(() =>
        {
            TotalPowerText.Text = data.TotalPower > 0 ? $"{data.TotalPower:F0} W" : "— W";

            var color = data.TotalPower switch
            {
                < 50 => new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x22, 0xD6, 0x5E)),
                < 100 => new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0xF0, 0xFF)),
                < 200 => new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x4A, 0x90, 0xD9)),
                < 350 => new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0x6B, 0x35)),
                _ => new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0x44, 0x44))
            };
            TotalPowerText.Foreground = color;
        });
    }
}
