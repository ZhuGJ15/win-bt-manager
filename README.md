# WindowsBlueToothManager

[中文](#中文) | [English](#english)

---

## 中文

WindowsBlueToothManager 是一个面向 Windows 10 和 Windows 11 的蓝牙设备电量管理工具，目标是让用户无需反复进入系统设置，就能在桌面、系统托盘或任务栏附近快速查看蓝牙设备连接状态和电量。

> 当前项目处于设计与工程初始化阶段，核心功能尚未实现。

### 项目目标

- 支持 BLE 低功耗蓝牙设备和 BTC 经典蓝牙设备。
- 每 5 秒自动扫描蓝牙设备状态。
- 展示所有已连接蓝牙设备的连接状态和电量。
- 支持系统托盘常驻展示。
- 支持任务栏附近的贴边电量展示，便于不打开主界面也能查看电量。
- 支持按设备控制是否显示在底部展示区域。
- 支持低电量提醒，默认阈值为 20%。
- 支持扫描失败、电量读取失败等异常的详细日志记录。
- 支持桌面右上角错误提示弹窗，并在 10 秒后自动消失。

### 技术栈

| 类型 | 选型 |
| --- | --- |
| 开发语言 | C# |
| UI 框架 | WPF |
| 运行时 | .NET 8，必要时可兼容 .NET 6 |
| 目标系统 | Windows 10、Windows 11 |
| 蓝牙 API | Windows.Devices.Bluetooth、Windows.Devices.Enumeration、Windows Runtime API |
| 日志 | Microsoft.Extensions.Logging、Serilog |
| 配置存储 | JSON 配置文件 |

### 功能规划

| 功能 | 状态 | 说明 |
| --- | --- | --- |
| 中文 README | 已完成 | 当前文件 |
| 英文 README | 已完成 | 当前文件内置英文版本 |
| 系统设计文档 | 已完成 | 见 `docs/system-design-and-implementation.md` |
| Git 忽略规则 | 已完成 | 已添加 `.gitignore`，排除构建产物、IDE 缓存、日志和本地配置 |
| WPF 工程骨架 | 已完成 | 已创建解决方案、WPF 项目、应用入口和主窗口，用户已确认可打开窗口 |
| 蓝牙设备扫描 | 部分完成 | 已接入 Windows 蓝牙设备枚举，可枚举 BLE/BTC 设备、尝试读取连接状态，并按蓝牙地址/同名 BLE-BTC 合并同一物理设备 |
| 蓝牙电量读取 | 部分完成 | 已对 BLE 设备尝试读取标准 GATT Battery Service，并对 BTC 设备尝试读取 Windows 设备属性、蓝牙注册表缓存和 PnP 设备节点 |
| 主界面 | 部分完成 | 已用模拟数据和真实枚举展示设备列表、连接状态、电量占位、展示开关、中英文切换和可持久化刷新频率设置 |
| 系统托盘 | 部分完成 | 已实现托盘常驻图标、设备摘要 tooltip、打开/立即刷新/退出菜单，并支持配置关闭按钮后台运行或直接退出 |
| 任务栏底部展示 | 部分完成 | 已新增实验性任务栏嵌入窗口，以上方设备名、下方电量的形式展示用户勾选 Bottom 的设备；系统托盘 `NotifyIcon` 保留为兜底入口 |
| 低电量告警 | 未完成 | 电量低于 20% 时提醒 |
| 异常日志与弹窗 | 未完成 | 记录详细日志并展示 10 秒错误提示 |

### 任务栏展示说明

Windows 普通桌面应用直接向系统任务栏内部注入多个动态图标存在兼容性风险。当前版本参考 `SpLlry/SplusXBTMeter` 的任务栏窗口思路，采用：

- 系统托盘图标：展示整体状态、最低电量或低电量数量。
- 实验性任务栏嵌入窗口：使用轻量 WPF 窗口挂载到主任务栏，以上方设备名、下方电量的形式展示用户勾选 Bottom 的设备。

任务栏窗口当前最多展示 4 个设备，并为右侧通知区域预留安全宽度；多显示器、精确避让每个任务栏应用图标和高 DPI 细节仍需要后续调试确认。

### 文档

- [系统设计与实现文档](docs/system-design-and-implementation.md)
- [调试说明](docs/debugging.md)

### 当前目录结构

```text
WindowsBlueToothManager/
├── .gitignore
├── README.md
├── docs/
│   ├── debugging.md
│   └── system-design-and-implementation.md
├── WindowsBlueToothManager.sln
├── src/
│   └── WindowsBlueToothManager/
│       ├── WindowsBlueToothManager.csproj
│       ├── App.xaml
│       ├── App.xaml.cs
│       ├── App/
│       ├── Bluetooth/
│       │   ├── Abstractions/
│       │   ├── Ble/
│       │   └── Classic/
│       ├── Infrastructure/
│       │   ├── Configuration/
│       │   ├── Logging/
│       │   ├── Notifications/
│       │   ├── Persistence/
│       │   └── Scheduling/
│       ├── Models/
│       ├── Resources/
│       │   └── Icons/
│       ├── Services/
│       │   ├── Alerts/
│       │   ├── Display/
│       │   ├── Monitoring/
│       │   ├── Overlay/
│       │   └── Tray/
│       ├── ViewModels/
│       └── Views/
│           ├── Controls/
│           └── Windows/
├── tests/
│   └── WindowsBlueToothManager.Tests/
│       ├── Fakes/
│       └── Unit/
└── tools/
```

### 后续开发计划

1. 创建 C# WPF 解决方案和项目骨架。
2. 实现配置、日志、依赖注入和应用生命周期管理。
3. 使用模拟数据实现主界面、托盘和贴边展示。
4. 接入 Windows 蓝牙设备枚举能力。
5. 实现 BLE/BTC 电量读取策略。
6. 实现低电量提醒、异常弹窗和滚动日志。
7. 在 Windows 10 和 Windows 11 上进行真实设备测试。

### 开发与调试规则

- 每次只实现一个明确功能点，完成后先进入调试确认阶段。
- 每个功能交付时必须提供调试方式，包括运行命令、操作路径、预期结果和排查方式。
- 用户确认当前功能无误后，再继续实现下一个功能。
- 如果调试发现遗留问题、兼容性限制或待验证事项，必须同步记录到 `docs/system-design-and-implementation.md`。

---

## English

WindowsBlueToothManager is a Bluetooth battery monitoring tool for Windows 10 and Windows 11. It is designed to make Bluetooth device battery levels and connection status easier to access without repeatedly opening Windows Settings.

> The project is currently in the design and initialization stage. Core features have not been implemented yet.

### Goals

- Support BLE devices and BTC classic Bluetooth devices.
- Scan Bluetooth device status automatically every 5 seconds.
- Show all connected Bluetooth devices with connection status and battery level.
- Provide a persistent system tray entry.
- Provide a taskbar-adjacent battery overlay so users can check battery levels without opening the main window.
- Allow users to choose which devices are shown in the bottom overlay area.
- Show low-battery warnings with a default threshold of 20%.
- Record detailed logs for scan failures, battery read failures, and other runtime errors.
- Show a small error popup in the upper-right corner of the desktop, automatically dismissed after 10 seconds.

### Tech Stack

| Type | Choice |
| --- | --- |
| Language | C# |
| UI Framework | WPF |
| Runtime | .NET 8, with possible .NET 6 compatibility if needed |
| Target OS | Windows 10, Windows 11 |
| Bluetooth APIs | Windows.Devices.Bluetooth, Windows.Devices.Enumeration, Windows Runtime API |
| Logging | Microsoft.Extensions.Logging, Serilog |
| Configuration | JSON configuration files |

### Feature Roadmap

| Feature | Status | Notes |
| --- | --- | --- |
| Chinese README | Done | This file |
| English README | Done | Included in this file |
| System design document | Done | See `docs/system-design-and-implementation.md` |
| Git ignore rules | Done | Added `.gitignore` for build output, IDE caches, logs, and local settings |
| WPF project skeleton | Done | Added solution, WPF project, application entry, and main window; user confirmed the window opens |
| Bluetooth device scanning | Partially done | Windows Bluetooth enumeration is connected for BLE/BTC devices, connection status is attempted, and duplicate BLE/BTC records for the same physical device are merged |
| Bluetooth battery reading | Partially done | BLE devices are read via standard GATT Battery Service when available; BTC devices are attempted through Windows device properties, Bluetooth registry cache, and PnP device nodes |
| Main UI | Partially done | Simulated and enumerated data now drive device list, connection status, battery placeholders, display toggles, Chinese/English switching, and persisted refresh frequency settings |
| System tray | Partially done | Tray icon, battery summary tooltip, Open/Refresh/Exit menu, and configurable close-button behavior are available |
| Taskbar bottom display | Partially done | Added an experimental taskbar-embedded WPF window that shows each Bottom-selected device name above its battery level; the system tray `NotifyIcon` remains as a fallback entry point |
| Low-battery alerts | Not started | Warn when battery level is below 20% |
| Error logging and popup | Not started | Detailed logs plus 10-second error notification |

### Taskbar Display Notes

Injecting multiple dynamic custom icons directly into the internal Windows taskbar area has compatibility risks. The current version follows the taskbar-window approach used by `SpLlry/SplusXBTMeter` and uses:

- A system tray icon for overall status, lowest battery level, or low-battery count.
- An experimental taskbar-embedded WPF window that shows each selected device name above its battery level.

The taskbar window currently shows up to 4 selected devices and reserves space for the notification area. Multi-monitor behavior, exact avoidance of every taskbar app icon, and high-DPI details still need Windows-side validation.

### Documentation

- [System Design and Implementation Document](docs/system-design-and-implementation.md)
- [Debugging Guide](docs/debugging.md)

### Current Directory Structure

```text
WindowsBlueToothManager/
├── .gitignore
├── README.md
├── docs/
│   ├── debugging.md
│   └── system-design-and-implementation.md
├── WindowsBlueToothManager.sln
├── src/
│   └── WindowsBlueToothManager/
│       ├── WindowsBlueToothManager.csproj
│       ├── App.xaml
│       ├── App.xaml.cs
│       ├── App/
│       ├── Bluetooth/
│       │   ├── Abstractions/
│       │   ├── Ble/
│       │   └── Classic/
│       ├── Infrastructure/
│       │   ├── Configuration/
│       │   ├── Logging/
│       │   ├── Notifications/
│       │   ├── Persistence/
│       │   └── Scheduling/
│       ├── Models/
│       ├── Resources/
│       │   └── Icons/
│       ├── Services/
│       │   ├── Alerts/
│       │   ├── Display/
│       │   ├── Monitoring/
│       │   ├── Overlay/
│       │   └── Tray/
│       ├── ViewModels/
│       └── Views/
│           ├── Controls/
│           └── Windows/
├── tests/
│   └── WindowsBlueToothManager.Tests/
│       ├── Fakes/
│       └── Unit/
└── tools/
```

### Development Plan

1. Create the C# WPF solution and project skeleton.
2. Implement configuration, logging, dependency injection, and app lifecycle management.
3. Build the main UI, tray entry, and overlay using simulated device data.
4. Integrate Windows Bluetooth device enumeration.
5. Implement BLE/BTC battery reading strategies.
6. Implement low-battery alerts, error popups, and rolling logs.
7. Test with real devices on Windows 10 and Windows 11.

### Development and Debugging Rules

- Implement one clear feature at a time, then enter the debugging confirmation stage.
- Every feature delivery must include debugging instructions, including run commands, user actions, expected results, and troubleshooting notes.
- Do not move on to the next feature until the user confirms the current feature works correctly.
- Any remaining issue, compatibility limitation, or pending verification item found during debugging must be recorded in `docs/system-design-and-implementation.md`.
