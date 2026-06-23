using PowerMonitor.Services;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace PowerMonitor;

public partial class OverlayWindow : Window
{
    // Win32 extended styles for non-activatable, click-through overlay
    private const int WS_EX_TRANSPARENT = 0x20;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_LAYERED = 0x80000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int GWL_EXSTYLE = -20;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    public OverlayWindow()
    {
        InitializeComponent();
        Width = 180;
        Height = 52;
        Opacity = 0.9;
        ApplyOverlayPosition();
    }

    // ── Non-activatable (crosshair-style) after source is initialized ──
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE,
                exStyle | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_LAYERED | WS_EX_TOOLWINDOW);
        }
        catch { }
    }

    // ── Positioning ──
    private double _offsetX = 16;    // from right edge
    private double _offsetY = 12;    // from top edge
    private double _scale = 1.0;     // 0.5 ~ 2.0

    public void ApplyOverlayPosition()
    {
        double screenW = SystemParameters.PrimaryScreenWidth;
        double screenH = SystemParameters.PrimaryScreenHeight;

        double w = 180 * _scale;
        double h = 52 * _scale;

        Width = w;
        Height = h;
        Left = screenW - w - _offsetX;
        Top = _offsetY;
    }

    // ── Public methods for main window to call ──
    public void SetOffsetX(double val) { _offsetX = val; ApplyOverlayPosition(); }
    public void SetOffsetY(double val) { _offsetY = val; ApplyOverlayPosition(); }
    public void SetScale(double val) { _scale = val; ApplyOverlayPosition(); }

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
