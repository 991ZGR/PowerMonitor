using System.Diagnostics;
using System.Reflection;
using System.Security.Principal;
using System.Windows;

namespace PowerMonitor;

public partial class App : System.Windows.Application
{
    private System.Windows.Forms.NotifyIcon? _trayIcon;

    private static System.Drawing.Icon? LoadAppIcon()
    {
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            using var stream = asm.GetManifestResourceStream("PowerMonitor.Resources.PowerMonitor.ico");
            if (stream != null)
                return new System.Drawing.Icon(stream);
        }
        catch { }
        return System.Drawing.SystemIcons.Application;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // ── 1. Self-elevate to admin if needed ──
        if (!IsAdministrator())
        {
            var result = System.Windows.MessageBox.Show(
                "PowerMonitor 需要管理员权限才能读取硬件传感器。\n是否以管理员身份重新启动？",
                "需要管理员权限",
                MessageBoxButton.YesNo,
                MessageBoxImage.Exclamation);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = Environment.ProcessPath ?? "PowerMonitor.exe",
                        Verb = "runas",
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"无法启动管理员进程：{ex.Message}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            Current.Shutdown();
            return;
        }

        // ── 2. Auto-install PawnIO if missing ──
        if (!PawnIoIsInstalled())
        {
            var result = System.Windows.MessageBox.Show(
                "PowerMonitor 需要 PawnIO 内核驱动来读取 CPU/GPU 功耗传感器。\n是否自动安装？",
                "安装 PawnIO 驱动",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes)
            {
                InstallPawnIo();
            }
        }

        // ── 3. Create tray icon ──
        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = LoadAppIcon(),
            Text = "PowerMonitor - 整机功耗监控",
            Visible = true
        };

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("显示窗口", null, (_, _) => ShowMainWindow());
        menu.Items.Add("退出", null, (_, _) => ExitApp());
        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick += (_, _) => ShowMainWindow();
    }

    // ── PawnIO auto-install ──
    private static bool PawnIoIsInstalled()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO");
            return key != null;
        }
        catch { return false; }
    }

    private static void InstallPawnIo()
    {
        try
        {
            string tempPath = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), "PowerMonitor_PawnIO_setup.exe");

            // Extract embedded installer
            var assembly = Assembly.GetExecutingAssembly();
            string resourceName = "PowerMonitor.Resources.PawnIO_setup.exe";
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    System.Windows.MessageBox.Show(
                        "未找到内置的 PawnIO 安装程序。请联网后重新启动。",
                        "安装失败", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                using var fs = new System.IO.FileStream(tempPath, System.IO.FileMode.Create, System.IO.FileAccess.Write);
                stream.CopyTo(fs);
            }

            // Run installer silently
            var psi = new ProcessStartInfo
            {
                FileName = tempPath,
                Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART",
                UseShellExecute = true,
                Verb = "runas" // already admin, but just in case
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit(30000); // wait up to 30s

            // Clean up
            try { System.IO.File.Delete(tempPath); } catch { }

            // Verify
            if (PawnIoIsInstalled())
            {
                System.Windows.MessageBox.Show(
                    "PawnIO 驱动安装成功！程序将继续启动。",
                    "安装成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                System.Windows.MessageBox.Show(
                    "PawnIO 驱动安装可能未完成，传感器可能不可用。\n" +
                    "可以稍后手动运行：winget install namazso.PawnIO",
                    "安装可能未完成", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"安装 PawnIO 驱动时出错：{ex.Message}",
                "安装失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── Admin check ──
    internal static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    internal void ShowMainWindow()
    {
        foreach (Window win in Windows)
        {
            if (win is MainWindow)
            {
                win.WindowState = WindowState.Normal;
                win.Show();
                win.Activate();
                break;
            }
        }
    }

    internal void ExitApp()
    {
        _trayIcon?.Dispose();
        _trayIcon = null;
        Current.Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        _trayIcon = null;
        base.OnExit(e);
    }
}
