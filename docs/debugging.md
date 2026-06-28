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
| Visual Studio 无法加载项目 | 确认 Visual Studio 2022 安装了 `.NET desktop development` 工作负载 |
| 当前机器不是 Windows | WPF 应用不能在 macOS/Linux 上直接运行，请在 Windows 10/11 上验证 |

### 当前验证状态

| 项目 | 状态 | 备注 |
| --- | --- | --- |
| 文件结构检查 | 已完成 | 已在当前仓库确认解决方案和项目文件存在 |
| 本机命令行构建 | 未完成 | 当前环境没有 `dotnet` 命令，且不是 Windows WPF 调试环境 |
| 用户 Windows 调试确认 | 待确认 | 需要用户按本文档在 Windows 10/11 上验证 |

