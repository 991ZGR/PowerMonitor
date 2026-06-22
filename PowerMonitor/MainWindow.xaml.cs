using PowerMonitor.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PowerMonitor;

public partial class MainWindow : Window
{
    private readonly SensorMonitor _monitor = new();
    private OverlayWindow? _overlay;

    public MainWindow()
    {
        InitializeComponent();
        _monitor.DataUpdated += OnDataUpdated;
        _monitor.Start();
    }

    private void OnDataUpdated(PowerData data)
    {
        Dispatcher.Invoke(() =>
        {
            UpdateTotal(data);
            UpdateGpu(data);
            UpdateCpu(data);
            UpdateOther(data);
            UpdateInfoBar(data);
            _overlay?.UpdatePower(data);
        });
    }

    private void UpdateTotal(PowerData data)
    {
        bool valid = data.TotalPower > 0;
        TotalPowerText.Text = valid ? $"{data.TotalPower:F0}" : "—";

        var color = data.TotalPower switch
        {
            < 50 => new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x22, 0xD6, 0x5E)),
            < 100 => new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0xF0, 0xFF)),
            < 200 => new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x4A, 0x90, 0xD9)),
            < 350 => new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0x6B, 0x35)),
            _ => new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0x44, 0x44))
        };
        TotalPowerText.Foreground = color;

        StatusText.Text = data.HasCpuData || data.HasGpuData || data.HasOtherData
            ? "实时监控中 · 关闭时最小化到托盘"
            : "未检测到传感器 · PawnIO 驱动可能未正确加载";
    }

    private void UpdateGpu(PowerData data)
    {
        GpuPowerText.Text = data.HasGpuData ? $"{data.GpuPower:F1}" : "—";
        GpuNameText.Text = data.GpuName;

        if (data.HasGpuData)
        {
            GpuStatus.Text = data.GpuName;
            GpuStatus.Foreground = (SolidColorBrush)FindResource("TextSecondaryBrush");
        }
        else
        {
            GpuStatus.Text = "未检测到 GPU 功率传感器";
            GpuStatus.Foreground = (SolidColorBrush)FindResource("TextDimBrush");
        }
    }

    private void UpdateCpu(PowerData data)
    {
        CpuPowerText.Text = data.HasCpuData ? $"{data.CpuPower:F1}" : "—";
        CpuNameText.Text = data.CpuName;

        if (data.HasCpuData)
        {
            CpuStatus.Text = data.CpuName;
            CpuStatus.Foreground = (SolidColorBrush)FindResource("TextSecondaryBrush");
        }
        else
        {
            CpuStatus.Text = "未检测到 CPU 功率传感器";
            CpuStatus.Foreground = (SolidColorBrush)FindResource("TextDimBrush");
        }
    }

    private void UpdateOther(PowerData data)
    {
        if (data.HasOtherData)
        {
            OtherPowerText.Text = $"{data.OtherPower:F1}";
            OtherStatus.Text = "主板 · 内存 · 硬盘";
            OtherStatus.Foreground = (SolidColorBrush)FindResource("TextSecondaryBrush");
        }
        else
        {
            OtherPowerText.Text = "—";
            OtherStatus.Text = "主板无功耗传感器";
            OtherStatus.Foreground = (SolidColorBrush)FindResource("TextDimBrush");
        }
    }

    private void UpdateInfoBar(PowerData data)
    {
        string info = "";
        if (!string.IsNullOrEmpty(data.CpuName))
            info += $"CPU: {data.CpuName}";
        if (!string.IsNullOrEmpty(data.GpuName))
            info += (info.Length > 0 ? "  |  " : "") + $"GPU: {data.GpuName}";
        InfoBar.Text = info;
    }

    // ── Close → system tray ──
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        WindowState = WindowState.Minimized;
        Hide();
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private void SpeedBtn_Click(object sender, RoutedEventArgs e)
    {
        SpeedBtn.ContextMenu.IsOpen = true;
    }

    private void SpeedMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem item) return;
        int level = int.Parse(item.Tag?.ToString() ?? "3");

        SpeedInsane.IsChecked = level == 0;
        SpeedUltra.IsChecked = level == 1;
        SpeedFast.IsChecked = level == 2;
        SpeedMedium.IsChecked = level == 3;
        SpeedSlow.IsChecked = level == 4;

        _monitor.SpeedLevel = level;

        string label = level switch { 0 => "疯狂", 1 => "极速", 2 => "快", 3 => "中", _ => "慢" };
        int ms = _monitor.GetPollIntervalMs();
        SpeedBtn.ToolTip = $"刷新速度：{label} ({ms}ms)";
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
        Hide();
    }

    private void OverlayToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_overlay is { IsVisible: true })
        {
            _overlay.Hide();
            OverlayToggle.Foreground = (SolidColorBrush)FindResource("TextSecondaryBrush");
        }
        else
        {
            _overlay ??= new OverlayWindow();
            _overlay.Show();
            OverlayToggle.Foreground = (SolidColorBrush)FindResource("AccentCyanBrush");
        }
    }
}
