# PowerMonitor - 整机功耗监控工具

## 📋 功能

- ✅ **实时功耗监控** — 读取 CPU (RAPL/SMU)、GPU (NVML/ADL) 等传感器
- ✅ **大号总功耗显示** — 96pt 大字 + 动态颜色（绿→青→蓝→橙→红）
- ✅ **GPU/CPU 型号显示** — 自动检测并显示硬件名称
- ✅ **主板/内存/硬盘功耗** — 其他组件功耗汇总（如有传感器）
- ✅ **屏幕右上角叠加层** — 透明悬浮条，类似 MSI Afterburner
- ✅ **系统托盘** — 关闭时最小化到托盘，双击恢复
- ✅ **极低后台占用** — 每秒 1 次轮询，CPU 占用 < 1%
- ✅ **自适应缩放 UI** — 窗口任意缩放，内容等比放大

## 🚀 首次使用

### 下载
- **`PowerMonitor.exe`** (5 MB) — 需安装 .NET 8.0 运行时
- **`PowerMonitor_SelfContained.exe`** (159 MB) — 单机运行，无依赖

### ⚠️ 重要：管理员权限
CPU 功率传感器需要读取硬件 MSR 寄存器，**必须以管理员身份运行**！
Windows 会弹出 UAC 提示，请点击"是"。

### 操作
| 操作 | 说明 |
|------|------|
| **拖拽标题栏** | 移动窗口 |
| **点击 [P]** | 开启/关闭右上角叠加层 |
| **点击 [—]** | 最小化 |
| **点击 [✕]** | 最小化到系统托盘 |
| **双击托盘图标** | 恢复窗口 |
| **右键托盘图标** | 显示窗口 / 退出 |

## 🖥️ 传感器兼容性

| 组件 | 功率传感器 |
|------|------------|
| Intel CPU (SandyBridge+) | ✅ CPU Package / Cores / DRAM |
| AMD CPU (Zen/Zen+) | ✅ Package / Core / SOC |
| AMD CPU (Zen 4) | ✅ PPT / Core / SOC / Total |
| NVIDIA GPU | ✅ GPU Power (NVML) |
| AMD GPU | ✅ GPU Power (ADL) |
| Intel Arc GPU | ✅ GPU Power |
| 主板 (Super I/O) | ⚠️ 部分主板支持 |
| 内存 (DDR5 RAPF) | ⚠️ 部分支持 |
| NVMe/SSD | ⚠️ 部分支持 |

> 如显示"未检测到 CPU/GPU 功率传感器"：
> 1. 确保以**管理员身份**运行
> 2. 检查 Windows 安全中心 → 内核隔离 → **内存完整性**是否关闭（AMD 用户常见）
> 3. 旧款 CPU (AMD K10/K8/推土机) 不支持功率 MSR

## 🛠️ 技术栈

- **C# WPF** (.NET 8.0, Windows-only)
- **LibreHardwareMonitorLib** 0.9.6
- **暗色玻璃态 GUI** + Viewbox 自适应缩放
- **系统托盘** (Windows Forms NotifyIcon)
- **单文件发布** (`dotnet publish -p:PublishSingleFile=true`)
