namespace PowerMonitor.Services;

public class PowerData
{
    public float TotalPower { get; set; }
    public float CpuPower { get; set; }
    public float GpuPower { get; set; }
    public float OtherPower { get; set; }
    public bool HasCpuData { get; set; }
    public bool HasGpuData { get; set; }
    public bool HasOtherData { get; set; }
    public string CpuName { get; set; } = "";
    public string GpuName { get; set; } = "";
    public string DebugInfo { get; set; } = "";
}
