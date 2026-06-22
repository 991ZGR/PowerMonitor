using LibreHardwareMonitor.Hardware;
using System.Diagnostics;
using System.Text;

namespace PowerMonitor.Services;

/// <summary>
/// Sensor engine:
/// - Primary: LibreHardwareMonitor + PawnIO (CPU/GPU/Other via real HW sensors)
/// - Fallback: nvidia-smi for GPU if LHM returns no data
/// </summary>
public sealed class SensorMonitor : IDisposable
{
    private readonly Computer _computer;
    private readonly CancellationTokenSource _cts = new();
    private Task? _pollTask;
    private PowerData _latest = new();
    private readonly object _lock = new();
    private string _cpuName = "";
    private string _gpuName = "";

    public event Action<PowerData>? DataUpdated;

    public PowerData Latest
    {
        get { lock (_lock) return _latest; }
    }

    public SensorMonitor()
    {
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true,
            IsMotherboardEnabled = true,
            IsControllerEnabled = true,
            IsNetworkEnabled = false,
            IsStorageEnabled = true
        };
    }

    public void Start()
    {
        var debug = new StringBuilder();

        // Open LHM – PawnIO driver should be available now
        try
        {
            _computer.Open();
            debug.AppendLine("[LHM] Computer.Open() 成功");
        }
        catch (Exception ex)
        {
            debug.AppendLine($"[LHM] Open() 失败: {ex.Message}");
        }

        // Warm-up: multiple Update cycles for power delta computation
        var visitor = new UpdateVisitor();
        for (int i = 0; i < 4; i++)
        {
            try { _computer.Accept(visitor); }
            catch { }
            Thread.Sleep(250);
        }

        // Log hardware info + capture names (prefer dGPU over iGPU)
        foreach (var hw in _computer.Hardware)
        {
            debug.AppendLine($"[LHM] {hw.HardwareType}: {hw.Name}");
            if (hw.HardwareType == HardwareType.Cpu && string.IsNullOrEmpty(_cpuName))
                _cpuName = hw.Name;
            if (hw.HardwareType == HardwareType.GpuNvidia || hw.HardwareType == HardwareType.GpuAmd)
                _gpuName = hw.Name;  // dGPU takes priority
            else if (hw.HardwareType == HardwareType.GpuIntel && string.IsNullOrEmpty(_gpuName))
                _gpuName = hw.Name;  // iGPU only if no dGPU found
            foreach (var sub in hw.SubHardware)
            {
                debug.AppendLine($"  └─ {sub.HardwareType}: {sub.Name}");
                if (sub.HardwareType == HardwareType.GpuNvidia || sub.HardwareType == HardwareType.GpuAmd)
                    _gpuName = sub.Name;
                else if (sub.HardwareType == HardwareType.GpuIntel && string.IsNullOrEmpty(_gpuName))
                    _gpuName = sub.Name;
            }
        }
        if (string.IsNullOrEmpty(_gpuName)) _gpuName = GetGpuName();
        if (string.IsNullOrEmpty(_cpuName)) _cpuName = GetCpuName();

        var initData = new PowerData { DebugInfo = debug.ToString(), CpuName = _cpuName, GpuName = _gpuName };
        lock (_lock) _latest = initData;
        DataUpdated?.Invoke(initData);

        _pollTask = Task.Run(PollLoop, _cts.Token);
    }

    public void Stop()
    {
        _cts.Cancel();
        _pollTask?.Wait(2000);
    }

    private async Task PollLoop()
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                UpdateSensors();
                await Task.Delay(GetPollIntervalMs(), _cts.Token);
            }
            catch (OperationCanceledException) { break; }
            catch { }
        }
    }

    /// <summary>Refresh speed: 0=insane(10ms), 1=ultra(100ms), 2=fast(500ms), 3=medium(1000ms), 4=slow(2000ms)</summary>
    public int SpeedLevel { get; set; } = 3;

    public int GetPollIntervalMs() => SpeedLevel switch
    {
        0 => 10,
        1 => 100,
        2 => 500,
        3 => 1000,
        _ => 2000
    };

    private void UpdateSensors()
    {
        // --- LHM sensors ---
        float lhmCpu = 0f, lhmGpu = 0f, lhmOther = 0f;
        bool hasCpu = false, hasGpu = false, hasOther = false;
        // Separate GPU power by type: prefer dGPU over iGPU
        float gpuNvidia = 0f, gpuAmd = 0f, gpuIntel = 0f;
        bool hasNvidia = false, hasAmd = false, hasIntel = false;

        try
        {
            _computer.Accept(new UpdateVisitor());

            foreach (var hw in _computer.Hardware)
            {
                CollectPowerSeparate(hw, ref lhmCpu,
                    ref gpuNvidia, ref gpuAmd, ref gpuIntel,
                    ref lhmOther, ref hasCpu,
                    ref hasNvidia, ref hasAmd, ref hasIntel, ref hasOther);
                foreach (var sub in hw.SubHardware)
                    CollectPowerSeparate(sub, ref lhmCpu,
                        ref gpuNvidia, ref gpuAmd, ref gpuIntel,
                        ref lhmOther, ref hasCpu,
                        ref hasNvidia, ref hasAmd, ref hasIntel, ref hasOther);
            }
        }
        catch { }

        // Prefer dGPU: NVIDIA > AMD > Intel (iGPU)
        if (hasNvidia) { lhmGpu = gpuNvidia; hasGpu = true; }
        else if (hasAmd) { lhmGpu = gpuAmd; hasGpu = true; }
        else if (hasIntel) { lhmGpu = gpuIntel; hasGpu = true; }

        // --- nvidia-smi fallback for GPU ---
        if (!hasGpu)
        {
            try
            {
                string output = RunNvidiaSmi();
                if (!string.IsNullOrEmpty(output) &&
                    float.TryParse(output.Trim(), System.Globalization.NumberStyles.Float,
                                    System.Globalization.CultureInfo.InvariantCulture, out float gpuW))
                {
                    lhmGpu = gpuW;
                    hasGpu = true;
                }
            }
            catch { }
        }

        // --- Build data ---
        float total = (hasCpu || hasGpu || hasOther) ? lhmCpu + lhmGpu + lhmOther : 0;

        var data = new PowerData
        {
            TotalPower = total,
            CpuPower = lhmCpu,
            GpuPower = lhmGpu,
            OtherPower = lhmOther,
            HasCpuData = hasCpu,
            HasGpuData = hasGpu,
            HasOtherData = hasOther,
            CpuName = _cpuName,
            GpuName = _gpuName
        };

        lock (_lock) _latest = data;
        DataUpdated?.Invoke(data);
    }

    private static void CollectPowerSeparate(
        IHardware hw,
        ref float cpu,
        ref float gpuNvidia, ref float gpuAmd, ref float gpuIntel,
        ref float other,
        ref bool hasCpu,
        ref bool hasNvidia, ref bool hasAmd, ref bool hasIntel, ref bool hasOther)
    {
        // For CPU: only use Package sensor to avoid double-counting
        if (hw.HardwareType == HardwareType.Cpu)
        {
            bool hasPkg = false;
            foreach (var sensor in hw.Sensors)
            {
                if (sensor.SensorType != SensorType.Power || !sensor.Value.HasValue) continue;
                float val = sensor.Value.Value;
                if (val < 0) continue;
                if (sensor.Name.Contains("Package", StringComparison.OrdinalIgnoreCase))
                {
                    cpu += val; hasCpu = true; hasPkg = true;
                }
            }
            if (!hasPkg) // fallback: sum all CPU power sensors
            {
                foreach (var sensor in hw.Sensors)
                {
                    if (sensor.SensorType != SensorType.Power || !sensor.Value.HasValue) continue;
                    float val = sensor.Value.Value;
                    if (val < 0) continue;
                    cpu += val; hasCpu = true;
                }
            }
            return;
        }

        // For non-CPU: collect by type
        foreach (var sensor in hw.Sensors)
        {
            if (sensor.SensorType != SensorType.Power || !sensor.Value.HasValue) continue;
            float val = sensor.Value.Value;
            if (val < 0) continue;

            switch (hw.HardwareType)
            {
                case HardwareType.GpuNvidia:
                    gpuNvidia += val; hasNvidia = true; break;
                case HardwareType.GpuAmd:
                    gpuAmd += val; hasAmd = true; break;
                case HardwareType.GpuIntel:
                    gpuIntel += val; hasIntel = true; break;
                default:
                    other += val; hasOther = true; break;
            }
        }
    }

    private static string RunNvidiaSmi()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "nvidia-smi",
            Arguments = "--query-gpu=power.draw --format=csv,noheader,nounits",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        using var proc = Process.Start(psi);
        if (proc == null) return "";
        string output = proc.StandardOutput.ReadToEnd().Trim();
        proc.WaitForExit(3000);
        return output;
    }

    private static string GetCpuName()
    {
        try
        {
            using var searcher = new System.Management.ManagementObjectSearcher(
                "SELECT Name FROM Win32_Processor");
            foreach (var obj in searcher.Get())
                return obj["Name"]?.ToString() ?? "Intel CPU";
        }
        catch { }
        return "Intel CPU";
    }

    private static string GetGpuName()
    {
        // Try nvidia-smi first
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "nvidia-smi",
                Arguments = "--query-gpu=name --format=csv,noheader",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };
            using var proc = Process.Start(psi);
            if (proc != null)
            {
                string name = proc.StandardOutput.ReadToEnd().Trim();
                proc.WaitForExit(3000);
                if (!string.IsNullOrEmpty(name)) return name;
            }
        }
        catch { }
        // Fallback to WMI
        try
        {
            using var searcher = new System.Management.ManagementObjectSearcher(
                "SELECT Name FROM Win32_VideoController");
            foreach (var obj in searcher.Get())
                return obj["Name"]?.ToString() ?? "NVIDIA GPU";
        }
        catch { }
        return "NVIDIA GPU";
    }

    public void Dispose()
    {
        Stop();
        _computer.Close();
        _cts.Dispose();
    }

    private sealed class UpdateVisitor : IVisitor
    {
        public void VisitComputer(IComputer computer) => computer.Traverse(this);
        public void VisitHardware(IHardware hardware)
        {
            hardware.Update();
            foreach (var sub in hardware.SubHardware)
                VisitHardware(sub);
        }
        public void VisitSensor(ISensor sensor) { }
        public void VisitParameter(IParameter parameter) { }
    }
}
