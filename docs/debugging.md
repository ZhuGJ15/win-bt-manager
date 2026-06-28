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

## 功能 2：基础 UI 与模拟设备数据

### 目标

验证主窗口已经具备初步设备监控界面，可以展示设备列表、设备类型、连接状态、电量、底部展示开关、托盘展示开关，并支持通过设置菜单配置自动刷新频率。

### 前置条件

| 条件 | 说明 |
| --- | --- |
| 操作系统 | Windows 10 或 Windows 11 |
| SDK | .NET 8 SDK |
| 已完成前置功能 | 功能 1：C# WPF 工程骨架 |

### 命令行调试方式

在项目根目录执行：

```powershell
dotnet build WindowsBlueToothManager.sln
dotnet run --project src/WindowsBlueToothManager/WindowsBlueToothManager.csproj
```

### 预期结果

| 检查项 | 预期结果 |
| --- | --- |
| 主窗口布局 | 打开后不再是骨架提示页，而是设备监控界面 |
| 顶部菜单栏 | 显示更明显的顶部菜单栏，菜单项高度、宽度和字体应比之前更醒目 |
| 顶部区域 | 显示应用名和模拟数据状态，不再显示 `Refresh` 按钮 |
| 统计区域 | 显示 Connected、Shown at bottom、Low battery 三个统计值 |
| 设备列表 | 至少显示 4 条模拟设备数据 |
| 设备类型 | 列表中能看到 BLE、BTC、Unknown |
| 电量展示 | 有百分比、进度条、低电量提示；已断开设备显示 `-1` 和“无法获取电量/Battery unavailable”；已连接但暂未读取电量时显示“待获取/Pending” |
| 状态颜色 | 设备状态为“已连接/Connected”时文字为绿色，“已断开/Disconnected”时文字为灰色 |
| 电量条颜色 | 电量低于 50% 时进度条为黄色，低于 20% 时进度条为红色，正常电量为绿色 |
| 电量百分比位置 | 电量百分比不再贴近单元格右边缘，应靠近进度条右侧显示 |
| 行分隔线 | 设备列表上下行分隔线应为很淡的灰色，不应比整体浅色界面突兀 |
| 展示开关 | 勾选或取消 Bottom/Tray 后，统计值和 Display 列会更新 |
| 刷新频率 | 在顶部菜单栏打开 `设置/Settings` -> `刷新频率/Refresh frequency`，可选择 5s、10s、30s、1min |
| 自动刷新 | 选择刷新频率后，等待对应时间，更新时间变化，当前数据源会自动刷新 |
| 刷新频率持久化 | 修改刷新频率后关闭应用，再重新启动，菜单勾选项应保持为上次选择 |
| 中英文切换 | 在顶部菜单栏打开 `设置/Settings` -> `语言/Language`，切换 `中文` 和 `English` 后，按钮、统计卡片、表格列名、状态文案、底部提示和设备状态会切换语言 |

### 常见问题排查

| 问题 | 处理方式 |
| --- | --- |
| 窗口仍显示骨架提示页 | 确认已拉取或保存最新代码，并重新执行 `dotnet build` |
| 执行 `dotnet run` 后没有窗口也没有报错 | 已将启动方式改为显式创建主窗口，并增加启动异常弹窗；请重新构建后运行，如果仍失败，应出现 `WindowsBlueToothManager startup error` 弹窗 |
| `无法对 ... BatteryProgressValue 类型的只读属性进行 TwoWay 或 OneWayToSource 绑定` | 已将电量进度条的 `ProgressBar.Value` 绑定显式改为 `Mode=OneWay` |
| 表格没有设备数据 | 确认 `MainWindow.xaml.cs` 中设置了 `DataContext = _viewModel` |
| 勾选 Bottom/Tray 后统计不变 | 确认点击的是复选框本身，或切换单元格后观察统计区域 |
| 刷新后 Bottom/Tray 勾选被重置 | 已在刷新时保留设备展示偏好；如仍重置，检查 `RefreshDevicesAsync()` 是否保留旧设备偏好 |
| 刷新频率切换后没有自动刷新 | 确认 `MainWindow.xaml.cs` 中启动了 `DispatcherTimer`，且 `SelectedRefreshInterval` 改变时调用了 `ApplyRefreshTimerInterval()` |
| 重新启动后刷新频率没有保留 | 确认 `%AppData%/WindowsBlueToothManager/settings.json` 存在，并且 `RefreshIntervalSeconds` 是 5、10、30 或 60 |
| 切换语言后表格列名不变 | 确认 `MainWindow.xaml.cs` 中调用了 `ApplyColumnHeaders()`，DataGrid 列头需要通过代码同步更新 |
| 真实蓝牙设备没有出现 | 确认当前数据源是 `Windows 蓝牙/Windows Bluetooth`，并参考功能 3 的设备枚举排查项 |

### 当前验证状态

| 项目 | 状态 | 备注 |
| --- | --- | --- |
| 文件结构检查 | 已完成 | 已新增模型和 ViewModel，主窗口已绑定模拟数据 |
| 启动异常可见化 | 已完成 | 已改为代码显式创建主窗口，并为启动失败增加错误弹窗 |
| BatteryProgressValue 绑定修复 | 已完成 | 已将电量进度条只读属性绑定修正为单向绑定 |
| 中英文切换 | 已完成 | 已增加顶部菜单栏，并将语言切换放到 `设置/Settings` 菜单下 |
| 顶部菜单栏优化 | 已完成 | 已增大菜单栏和菜单项尺寸，提高可见性 |
| 刷新频率设置 | 已完成 | 已移除刷新按钮，改为 `设置/Settings` -> `刷新频率/Refresh frequency`，支持 5s、10s、30s、1min 自动刷新，并记住用户选择 |
| 本机静态检查 | 已完成 | 当前环境可做 XML/XAML 格式检查，但不能运行 WPF |
| 用户 Windows 调试确认 | 已完成 | 用户已确认该阶段优化暂时结束 |

## 功能 3：Windows 蓝牙设备枚举

### 目标

验证应用可以通过 Windows 蓝牙 API 枚举 BLE 和经典蓝牙设备，并将真实设备显示到主界面列表中。BLE/BTC 电量读取已在功能 4 接入。

### 前置条件

| 条件 | 说明 |
| --- | --- |
| 操作系统 | Windows 10 或 Windows 11 |
| SDK | .NET 8 SDK |
| 蓝牙状态 | Windows 蓝牙已开启 |
| 设备条件 | 至少有一个已配对或可被系统识别的蓝牙设备 |

### 命令行调试方式

在项目根目录执行：

```powershell
dotnet build WindowsBlueToothManager.sln
dotnet run --project src/WindowsBlueToothManager/WindowsBlueToothManager.csproj
```

### 预期结果

| 检查项 | 预期结果 |
| --- | --- |
| 默认数据源 | 启动后默认使用 `Windows 蓝牙/Windows Bluetooth` 数据源 |
| 数据源菜单 | 在 `设置/Settings` -> `数据源/Data source` 中能看到 `Windows 蓝牙/Windows Bluetooth` 和 `模拟数据/Simulated data` |
| 真实设备列表 | Windows 蓝牙数据源下，列表显示系统枚举到的蓝牙设备 |
| 设备类型 | BLE 设备显示为 `BLE`，经典蓝牙设备显示为 `BTC` |
| 同一设备去重 | 如果同一物理设备先被识别为 BLE、后又被识别为 BTC，列表中应只保留一条更可信记录 |
| 连接状态 | 如果设备/驱动暴露连接状态，列表显示 Connected/已连接 或 Disconnected/已断开 |
| 电量列 | 支持标准 Battery Service 的已连接 BLE 设备可显示百分比；其他暂未读取到电量的已连接设备显示“待获取/Pending”；已断开设备显示 `-1` |
| 模拟数据回退 | 切换到 `模拟数据/Simulated data` 后，列表回到 4 条模拟设备 |
| 自动刷新 | 按设置中的刷新频率自动重新枚举真实设备 |

### 常见问题排查

| 问题 | 处理方式 |
| --- | --- |
| 真实设备列表为空 | 确认 Windows 蓝牙已开启，并且系统设置中能看到已配对或已连接设备 |
| 只看到 BLE 或只看到 BTC | 取决于当前 Windows API 能枚举到的设备类型，先记录设备型号，后续适配时补充 |
| 同一设备出现 BLE 和 BTC 两条记录 | 已按蓝牙地址去重，地址缺失时按同名 BLE/BTC 谨慎合并；如果仍重复，请记录两条记录的设备名、类型和电量状态 |
| 连接状态不准确 | 当前读取 `System.Devices.Aep.IsConnected`，部分设备或驱动可能不暴露该属性，需要后续真实设备兼容性验证 |
| 已断开设备连接后电量仍是 `-1` | 等待一次自动刷新，或临时将刷新频率切到 5s；刷新后如果连接状态变为 Connected/已连接，电量应从 `-1` 变为“待获取/Pending” |
| 枚举失败或状态栏显示错误 | 将错误文本记录下来，用于后续补充异常日志和兼容性处理 |

### 当前验证状态

| 项目 | 状态 | 备注 |
| --- | --- | --- |
| Windows 蓝牙枚举服务 | 已完成 | 已新增 `WindowsBluetoothDeviceEnumerator` |
| BLE 枚举 | 已完成，待调试确认 | 使用 `BluetoothLEDevice.GetDeviceSelector()` |
| BTC 枚举 | 已完成，待调试确认 | 使用 `BluetoothDevice.GetDeviceSelector()` |
| 连接状态读取 | 部分完成，待调试确认 | 使用 `System.Devices.Aep.IsConnected`，真实设备兼容性待验证 |
| UI 数据源切换 | 已完成，待调试确认 | 可在设置菜单切换 Windows 蓝牙和模拟数据 |
| 同一物理设备去重 | 已完成，待调试确认 | 优先按蓝牙地址合并 BLE/BTC 双记录，地址缺失时合并同名 BLE/BTC，并优先保留有电量、已连接、BTC 类型的记录 |
| 本机静态检查 | 已完成 | 当前环境可做 XML/XAML 格式检查，但不能运行 Windows 蓝牙 API |
| 用户 Windows 调试确认 | 待确认 | 需要用户按本文档在 Windows 10/11 上验证 |

## 功能 4：蓝牙电量读取

### 目标

验证应用可以对已连接 BLE 设备读取标准 GATT Battery Service 中的 Battery Level 特征值，并对经典蓝牙 BTC 设备尝试读取 Windows 设备属性、蓝牙注册表缓存和 PnP 设备节点中暴露的电量百分比。

BLE 电量读取相对标准化；BTC 电量读取高度依赖设备驱动和 Windows 是否暴露电量属性，因此当前阶段属于“尝试读取并等待真实设备兼容性确认”。针对 sanag 耳机，已参考 `SpLlry/SplusXBTMeter` 的 Windows SetupAPI/CfgMgr32 读取思路，补充 `BTHENUM` devnode 电量属性读取。

### 前置条件

| 条件 | 说明 |
| --- | --- |
| 操作系统 | Windows 10 或 Windows 11 |
| SDK | .NET 8 SDK |
| 蓝牙状态 | Windows 蓝牙已开启 |
| 设备条件 | 至少有一个已连接 BLE 或经典蓝牙设备；BLE 设备最好支持标准 Battery Service，BTC 设备最好能在 Windows 设置中看到电量 |

### 命令行调试方式

在项目根目录执行：

```powershell
dotnet build WindowsBlueToothManager.sln
dotnet run --project src/WindowsBlueToothManager/WindowsBlueToothManager.csproj
```

### 预期结果

| 检查项 | 预期结果 |
| --- | --- |
| BLE 电量成功读取 | 支持标准 Battery Service 的已连接 BLE 设备显示 `xx%` |
| BLE 电量未读取到 | 不支持标准 Battery Service，或 `Uncached -> Cached` 双路径都读取失败时显示 `无法获取电量/Battery unavailable` |
| BTC 电量成功读取 | Windows 设备属性、蓝牙 devnode 属性、蓝牙注册表缓存或 PnP 设备节点暴露电量的经典蓝牙设备显示 `xx%` |
| BTC 电量未读取到 | 已连接但 Windows 未暴露电量的 BTC 设备显示 `无法获取电量/Battery unavailable` |
| 断开设备 | 断开设备显示 `-1` 和“无法获取电量/Battery unavailable” |
| 自动刷新 | 设备连接状态或电量变化后，等待当前刷新频率周期，列表会自动刷新 |

### 常见问题排查

| 问题 | 处理方式 |
| --- | --- |
| BLE 设备仍显示 `待获取/Pending` | 等待一次自动刷新；如果刷新后变为 `无法获取电量`，表示本轮 BLE GATT 和 Windows 设备属性读取均未命中 |
| BLE 设备显示已断开但实际已连接 | 已将 BLE 读取从连接状态判断中解耦；即使 `System.Devices.Aep.IsConnected` 不可靠，也会先尝试读取 BLE Battery Service |
| BTC 设备仍显示 `待获取/Pending` | 等待一次自动刷新；如果刷新后变为 `无法获取电量`，表示本轮读取策略均未命中 |
| Windows 设置里能看到电量，但应用显示无法获取电量 | 该设备可能通过厂商驱动或非标准接口暴露电量，需要后续补充 WMI 或厂商接口策略；请记录设备型号、Windows 设置页显示的电量、应用中的设备名称 |
| UI 显示“设备刷新失败：找不到元素”且列表空白 | 已将电量属性读取从全局枚举参数改为逐设备、逐属性尝试，并为基础设备枚举增加无属性降级；请重新构建运行，预期不再因单个属性不支持而清空设备列表 |
| `CS0121 Math.Clamp(byte, byte, byte) 和 Math.Clamp(int, int, int) 调用具有二义性` | 已将 `DataReader.ReadByte()` 的返回值显式转换为 `int` 后再调用 `Math.Clamp` |
| `CS0199 无法将静态只读字段用作 ref 或 out 值` | 已将传给 `SetupDiGetClassDevs` 的设备类 GUID 改为方法内局部变量，再以 `ref` 传入 |
| `CS0411 无法从用法中推断 SelectMany 类型参数` | 已将同一设备去重逻辑中的 `SelectMany` 改为显式列表累加，避免分支返回类型推断失败 |
| 读取时界面短暂显示刷新中 | BLE 电量读取有 3 秒单设备超时，这是为了避免单个设备卡住刷新 |
| BLE 设备没有电量 | 当前已尝试 BLE GATT Battery Service 的 `Uncached -> Cached` 双路径和 Windows 设备属性；如果仍没有电量，请确认 Windows 设置页是否显示该 BLE 设备电量 |
| sanag 耳机没有电量 | 已参考 `SpLlry/SplusXBTMeter` 补充 SetupAPI/CfgMgr32 的 `BTHENUM` devnode 电量属性读取；请重新构建运行并等待一次刷新，如果仍失败，请确认 Windows 设置页是否能显示 sanag 耳机电量 |
| BTC 设备没有电量 | 当前已尝试读取 Windows 设备属性、蓝牙 devnode 属性、`BTHPORT` 注册表缓存和 PnP 设备节点；如果仍没有电量，请记录设备型号和 Windows 蓝牙设置页是否显示电量 |

### 当前验证状态

| 项目 | 状态 | 备注 |
| --- | --- | --- |
| BLE GATT Battery Service 读取 | 已完成，待调试确认 | 使用 `GattServiceUuids.Battery` 和 `GattCharacteristicUuids.BatteryLevel`，按 `Uncached -> Cached` 双路径读取 |
| BLE 连接状态兼容 | 已完成，待调试确认 | BLE 电量读取不再因 `System.Devices.Aep.IsConnected` 缺失或误报 false 而直接跳过 |
| BLE 电量读取超时 | 已完成，待调试确认 | 单设备 3 秒超时 |
| BTC 电量属性读取 | 已完成，待调试确认 | 尝试读取 Windows 设备属性 `System.Devices.BatteryLife`、`BatteryLevel`、`BatteryPercentage`、`PowerLevel` |
| BTC devnode 读取 | 已完成，待调试确认 | 参考 `SpLlry/SplusXBTMeter` 思路，通过 SetupAPI 枚举 `BTHENUM` 设备节点，并通过 CfgMgr32 读取蓝牙电量 DEVPROPKEY |
| BTC 注册表缓存读取 | 已完成，待调试确认 | 通过经典蓝牙地址尝试读取 `BTHPORT` 设备缓存中的 Battery/PowerLevel 值 |
| BTC PnP 节点读取 | 已完成，待调试确认 | 通过蓝牙地址和设备名匹配 PnP AssociationEndpoint、DeviceContainer、Device 节点并读取电量属性 |
| 本机静态检查 | 已完成 | 当前环境可做 XML/XAML 格式检查，但不能运行 Windows 蓝牙 API |
| 用户 Windows 调试确认 | 待确认 | 需要用户使用真实 BLE/BTC 设备验证 |
