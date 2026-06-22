# PowerMonitor

整机功耗监控工具，支持 CPU（RAPL）和 GPU（NVML）实时功耗显示，屏幕右上角叠加层，系统托盘运行。

## 下载

[GitHub Releases](https://github.com/991ZGR/PowerMonitor/releases) 页面下载 `PowerMonitor.exe`。

> 需安装 .NET 8.0 Desktop Runtime。首次运行会自动安装 PawnIO 内核驱动（弹窗确认即可），用于读取 CPU MSR 寄存器。

## 使用

**首次运行必须右键 → 以管理员身份运行**。

| 操作 | 说明 |
|------|------|
| 拖拽标题栏 | 移动窗口 |
| 点击 [⚡] | 切换刷新速度（极速/快/中/慢） |
| 点击 [P] | 切换右上角叠加层 |
| 点击 [—] | 最小化 |
| 点击 [✕] | 最小化到系统托盘 |
| 双击托盘图标 | 恢复窗口 |
| 右键托盘图标 | 显示窗口 / 退出 |


