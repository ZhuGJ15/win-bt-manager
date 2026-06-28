# WindowsBlueToothManager 调试说明

本文档记录每个已交付功能的调试方式。每完成一个功能后，都需要先按对应说明完成调试确认，再继续实现下一个功能。

## 功能 1：C# WPF 工程骨架

### 目标

验证项目已经具备标准 C# WPF 应用骨架，可以在 Windows 10/11 上还原、构建并启动主窗口。

### 前置条件

| 条件 | 说明 |
| --- | --- |
| 操作系统 | Windows 10 或 Windows 11 |
| SDK | .NET 8 SDK |
| IDE | Visual Studio 2022、Rider 或命令行均可 |

### 命令行调试方式

在项目根目录执行：

```powershell
dotnet restore WindowsBlueToothManager.sln
dotnet build WindowsBlueToothManager.sln
dotnet run --project src/WindowsBlueToothManager/WindowsBlueToothManager.csproj
```

### Visual Studio 调试方式

1. 使用 Visual Studio 2022 打开 `WindowsBlueToothManager.sln`。
2. 确认启动项目是 `WindowsBlueToothManager`。
3. 选择 `Debug` 和 `Any CPU`。
4. 点击运行按钮或按 `F5`。

### 预期结果

| 检查项 | 预期结果 |
| --- | --- |
| 还原 | `dotnet restore` 成功完成 |
| 构建 | `dotnet build` 无错误 |
| 启动 | 应用窗口居中打开 |
| 窗口标题 | 显示 `WindowsBlueToothManager` |
| 页面内容 | 能看到 `WindowsBlueToothManager` 和 `WPF project skeleton is ready.` |

### 常见问题排查

| 问题 | 处理方式 |
| --- | --- |
| `dotnet` 命令不存在 | 安装 .NET 8 SDK，并重新打开终端 |
| 找不到 WPF 相关目标 | 确认在 Windows 环境运行，且安装的是 .NET SDK 不是 Runtime |
| `error NETSDK1135: SupportedOSPlatformVersion 10.0.18362.0 不能高于 TargetPlatformVersion 7.0` | 已将项目目标框架修正为 `net8.0-windows10.0.18362.0`，请重新执行 `dotnet build WindowsBlueToothManager.sln` |
| Visual Studio 无法加载项目 | 确认 Visual Studio 2022 安装了 `.NET desktop development` 工作负载 |
| 当前机器不是 Windows | WPF 应用不能在 macOS/Linux 上直接运行，请在 Windows 10/11 上验证 |

### 发布兼容性说明

`TargetFramework` 中的 `net8.0-windows10.0.18362.0` 不是绑定到某一台电脑的 Windows 版本，而是声明应用面向的 Windows 平台版本。它主要影响构建时可用 API 和兼容性检查。

发布成 exe 后，用户运行时不会再触发 `NETSDK1135` 这种构建错误。用户是否能运行，取决于系统版本、发布方式以及是否安装了所需 .NET 运行时：

| 项目 | 说明 |
| --- | --- |
| 系统要求 | 当前骨架声明支持 Windows 10 1903，也就是 10.0.18362.0 及以上版本；Windows 11 满足该条件 |
| Framework-dependent 发布 | 用户机器需要安装对应版本的 .NET Desktop Runtime |
| Self-contained 发布 | exe 包含 .NET 运行时，用户通常不需要额外安装 Runtime，但包体更大 |
| 更低版本 Windows 10 | 如果后续明确要支持 1903 之前的 Windows 10，需要评估蓝牙 API 和 WPF/.NET 支持后再下调目标平台版本 |

### 当前验证状态

| 项目 | 状态 | 备注 |
| --- | --- | --- |
| 文件结构检查 | 已完成 | 已在当前仓库确认解决方案和项目文件存在 |
| NETSDK1135 修复 | 已完成 | 已将 `TargetFramework` 从 `net8.0-windows` 调整为 `net8.0-windows10.0.18362.0` |
| 本机命令行构建 | 未完成 | 当前环境没有 `dotnet` 命令，且不是 Windows WPF 调试环境 |
| 用户 Windows 调试确认 | 已完成 | 用户已确认命令行调试后可以打开窗口 |
