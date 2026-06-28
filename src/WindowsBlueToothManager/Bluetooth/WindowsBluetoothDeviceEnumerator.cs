using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Text;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Devices.Enumeration.Pnp;
using Windows.Storage.Streams;
using WindowsBlueToothManager.Bluetooth.Abstractions;
using WindowsBlueToothManager.Models;

namespace WindowsBlueToothManager.Bluetooth;

public sealed class WindowsBluetoothDeviceEnumerator : IBluetoothDeviceEnumerator
{
    private const string IsConnectedProperty = "System.Devices.Aep.IsConnected";
    private const string BatteryLifeProperty = "System.Devices.BatteryLife";
    private const string BatteryLevelProperty = "System.Devices.BatteryLevel";
    private const string BatteryPercentageProperty = "System.Devices.BatteryPercentage";
    private const string PowerLevelProperty = "System.Devices.PowerLevel";
    private const string DisplayNameProperty = "System.ItemNameDisplay";
    private const string DeviceAddressProperty = "System.Devices.Aep.DeviceAddress";
    private const string DeviceInstanceIdProperty = "System.Devices.DeviceInstanceId";
    private const string ContainerIdProperty = "System.Devices.ContainerId";
    private const string BluetoothDeviceRegistryPath = @"SYSTEM\CurrentControlSet\Services\BTHPORT\Parameters\Devices";
    private const uint DigcfPresent = 0x00000002;
    private const uint CrSuccess = 0x00000000;
    private const uint SetupApiNoMoreItems = 259;
    private const int InvalidHandleValue = -1;
    private static readonly Guid SystemDeviceClassGuid = new("4d36e97d-e325-11ce-bfc1-08002be10318");
    private static readonly DevPropKey BluetoothBatteryLevelPropertyKey = new(
        new Guid("104EA319-6EE2-4701-BD47-8DDBF425BBE5"),
        2);
    private static readonly TimeSpan BatteryReadTimeout = TimeSpan.FromSeconds(3);

    private static readonly string[] EnumerationProperties =
    {
        IsConnectedProperty
    };

    private static readonly string[] BatteryProperties =
    {
        BatteryLifeProperty,
        BatteryLevelProperty,
        BatteryPercentageProperty,
        PowerLevelProperty
    };

    private static readonly string[] PnpBatteryProperties =
    {
        BatteryLifeProperty,
        BatteryLevelProperty,
        BatteryPercentageProperty,
        PowerLevelProperty,
        DisplayNameProperty,
        DeviceAddressProperty,
        DeviceInstanceIdProperty,
        ContainerIdProperty
    };

    private static readonly PnpObjectType[] BatteryPnpObjectTypes =
    {
        PnpObjectType.AssociationEndpoint,
        PnpObjectType.AssociationEndpointContainer,
        PnpObjectType.DeviceContainer,
        PnpObjectType.Device
    };

    public async Task<IReadOnlyList<BluetoothDeviceInfo>> EnumerateAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.Now;
        var devices = new Dictionary<string, BluetoothDeviceInfo>(StringComparer.OrdinalIgnoreCase);

        var enumerationErrors = new List<string>();

        await TryAddDevicesAsync(
            devices,
            BluetoothLEDevice.GetDeviceSelector(),
            DeviceType.Ble,
            now,
            enumerationErrors,
            cancellationToken);

        await TryAddDevicesAsync(
            devices,
            BluetoothDevice.GetDeviceSelector(),
            DeviceType.ClassicBluetooth,
            now,
            enumerationErrors,
            cancellationToken);

        if (devices.Count == 0 && enumerationErrors.Count > 0)
        {
            throw new InvalidOperationException(string.Join("; ", enumerationErrors));
        }

        return devices.Values
            .OrderByDescending(device => device.IsConnected)
            .ThenBy(device => device.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static async Task TryAddDevicesAsync(
        IDictionary<string, BluetoothDeviceInfo> devices,
        string selector,
        DeviceType deviceType,
        DateTime now,
        ICollection<string> enumerationErrors,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var foundDevices = await FindAllDevicesAsync(selector, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var device in foundDevices)
            {
                if (string.IsNullOrWhiteSpace(device.Id))
                {
                    continue;
                }

                var isConnected = ReadBooleanProperty(device, IsConnectedProperty);
                var batteryReadResult = await ReadBatteryLevelAsync(device, deviceType, isConnected, cancellationToken);

                devices[device.Id] = new BluetoothDeviceInfo
                {
                    DeviceId = device.Id,
                    Name = string.IsNullOrWhiteSpace(device.Name) ? "Unknown Bluetooth Device" : device.Name,
                    DeviceType = deviceType,
                    IsConnected = isConnected,
                    BatteryLevel = batteryReadResult.BatteryLevel,
                    ShowInTaskbarOverlay = false,
                    ShowInTray = false,
                    LastUpdatedAt = now,
                    StatusMessage = batteryReadResult.StatusMessage
                };
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            enumerationErrors.Add($"{deviceType}: {exception.Message}");
        }
    }

    private static async Task<IReadOnlyList<DeviceInformation>> FindAllDevicesAsync(
        string selector,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var devices = await DeviceInformation.FindAllAsync(selector, EnumerationProperties);
            return devices.ToList();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            cancellationToken.ThrowIfCancellationRequested();
            var devices = await DeviceInformation.FindAllAsync(selector);
            return devices.ToList();
        }
    }

    private static async Task<BatteryReadResult> ReadBatteryLevelAsync(
        DeviceInformation device,
        DeviceType deviceType,
        bool isConnected,
        CancellationToken cancellationToken)
    {
        if (!isConnected)
        {
            return BatteryReadResult.Unavailable("Battery unavailable");
        }

        if (deviceType == DeviceType.Ble)
        {
            var bleBatteryLevel = await ReadBleBatteryLevelWithTimeoutAsync(device.Id, cancellationToken);
            if (bleBatteryLevel.HasValue)
            {
                return BatteryReadResult.Success(bleBatteryLevel.Value, "Battery read from BLE Battery Service");
            }
        }

        var propertyBatteryLevel = await ReadBatteryLevelFromPropertiesAsync(device, cancellationToken);
        if (propertyBatteryLevel.HasValue)
        {
            return BatteryReadResult.Success(propertyBatteryLevel.Value, "Battery read from Windows device properties");
        }

        if (deviceType == DeviceType.ClassicBluetooth)
        {
            var classicBatteryReadResult = await ReadClassicBluetoothBatteryLevelAsync(device, cancellationToken);
            if (classicBatteryReadResult.BatteryLevel.HasValue)
            {
                return classicBatteryReadResult;
            }
        }

        return BatteryReadResult.Unavailable("Battery unavailable");
    }

    private static async Task<int?> ReadBleBatteryLevelWithTimeoutAsync(
        string deviceId,
        CancellationToken cancellationToken)
    {
        var readTask = ReadBleBatteryLevelAsync(deviceId, cancellationToken);
        var timeoutTask = Task.Delay(BatteryReadTimeout, cancellationToken);
        var completedTask = await Task.WhenAny(readTask, timeoutTask);

        if (completedTask != readTask)
        {
            return null;
        }

        return await readTask;
    }

    private static async Task<int?> ReadBleBatteryLevelAsync(
        string deviceId,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var bluetoothDevice = await BluetoothLEDevice.FromIdAsync(deviceId);
            if (bluetoothDevice is null)
            {
                return null;
            }

            var servicesResult = await bluetoothDevice.GetGattServicesForUuidAsync(
                GattServiceUuids.Battery,
                BluetoothCacheMode.Uncached);

            if (servicesResult.Status != GattCommunicationStatus.Success)
            {
                return null;
            }

            var batteryService = servicesResult.Services.FirstOrDefault();
            if (batteryService is null)
            {
                return null;
            }

            var characteristicsResult = await batteryService.GetCharacteristicsForUuidAsync(
                GattCharacteristicUuids.BatteryLevel,
                BluetoothCacheMode.Uncached);

            if (characteristicsResult.Status != GattCommunicationStatus.Success)
            {
                return null;
            }

            var batteryLevelCharacteristic = characteristicsResult.Characteristics.FirstOrDefault();
            if (batteryLevelCharacteristic is null)
            {
                return null;
            }

            var readResult = await batteryLevelCharacteristic.ReadValueAsync(BluetoothCacheMode.Uncached);
            if (readResult.Status != GattCommunicationStatus.Success || readResult.Value.Length == 0)
            {
                return null;
            }

            var reader = DataReader.FromBuffer(readResult.Value);
            var batteryLevel = (int)reader.ReadByte();
            return Math.Clamp(batteryLevel, 0, 100);
        }
        catch
        {
            return null;
        }
    }

    private static bool ReadBooleanProperty(DeviceInformation device, string propertyName)
    {
        return device.Properties.TryGetValue(propertyName, out var value)
            && value is bool boolValue
            && boolValue;
    }

    private static async Task<int?> ReadBatteryLevelFromPropertiesAsync(
        DeviceInformation device,
        CancellationToken cancellationToken)
    {
        var batteryLevel = ReadBatteryLevelFromProperties(device.Properties);
        if (batteryLevel.HasValue)
        {
            return batteryLevel;
        }

        foreach (var propertyName in BatteryProperties)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var enrichedDevice = await DeviceInformation.CreateFromIdAsync(device.Id, new[] { propertyName });
                cancellationToken.ThrowIfCancellationRequested();

                batteryLevel = ReadBatteryLevelFromProperties(enrichedDevice.Properties);
                if (batteryLevel.HasValue)
                {
                    return batteryLevel;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Some Windows builds or device drivers throw "Element not found" for
                // unsupported properties. Keep trying the remaining property names.
            }
        }

        return null;
    }

    private static int? ReadBatteryLevelFromProperties(IReadOnlyDictionary<string, object> properties)
    {
        foreach (var propertyName in BatteryProperties)
        {
            if (!properties.TryGetValue(propertyName, out var value))
            {
                continue;
            }

            var batteryLevel = NormalizeBatteryLevel(value);
            if (batteryLevel.HasValue)
            {
                return batteryLevel;
            }
        }

        return null;
    }

    private static async Task<BatteryReadResult> ReadClassicBluetoothBatteryLevelAsync(
        DeviceInformation device,
        CancellationToken cancellationToken)
    {
        var address = await ReadClassicBluetoothAddressAsync(device.Id, cancellationToken);

        if (!string.IsNullOrWhiteSpace(address))
        {
            var devNodeBatteryLevel = ReadClassicBluetoothDevNodeBatteryLevel(address);
            if (devNodeBatteryLevel.HasValue)
            {
                return BatteryReadResult.Success(
                    devNodeBatteryLevel.Value,
                    "Battery read from Windows Bluetooth devnode property");
            }

            var registryBatteryLevel = ReadClassicBluetoothRegistryBatteryLevel(address);
            if (registryBatteryLevel.HasValue)
            {
                return BatteryReadResult.Success(
                    registryBatteryLevel.Value,
                    "Battery read from Windows Bluetooth registry cache");
            }
        }

        var pnpBatteryLevel = await ReadClassicBluetoothPnpBatteryLevelAsync(
            device.Name,
            address,
            cancellationToken);

        return pnpBatteryLevel.HasValue
            ? BatteryReadResult.Success(pnpBatteryLevel.Value, "Battery read from Windows PnP battery properties")
            : BatteryReadResult.Unavailable("Battery unavailable");
    }

    private static async Task<string?> ReadClassicBluetoothAddressAsync(
        string deviceId,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var bluetoothDevice = await BluetoothDevice.FromIdAsync(deviceId);
            cancellationToken.ThrowIfCancellationRequested();

            if (bluetoothDevice is null)
            {
                return null;
            }

            return bluetoothDevice.BluetoothAddress.ToString("x12");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<int?> ReadClassicBluetoothPnpBatteryLevelAsync(
        string deviceName,
        string? bluetoothAddress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(deviceName) && string.IsNullOrWhiteSpace(bluetoothAddress))
        {
            return null;
        }

        foreach (var objectType in BatteryPnpObjectTypes)
        {
            IReadOnlyList<PnpObject> pnpObjects;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var foundObjects = await PnpObject.FindAllAsync(objectType, PnpBatteryProperties);
                pnpObjects = foundObjects.ToList();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                continue;
            }

            foreach (var pnpObject in pnpObjects)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!IsMatchingBluetoothPnpObject(pnpObject, deviceName, bluetoothAddress))
                {
                    continue;
                }

                var batteryLevel = ReadBatteryLevelFromProperties(pnpObject.Properties);
                if (batteryLevel.HasValue)
                {
                    return batteryLevel;
                }
            }
        }

        return null;
    }

    private static int? ReadClassicBluetoothDevNodeBatteryLevel(string bluetoothAddress)
    {
        var normalizedAddress = NormalizeBluetoothAddress(bluetoothAddress);
        if (normalizedAddress.Length != 12)
        {
            return null;
        }

        var systemDeviceClassGuid = SystemDeviceClassGuid;
        var infoSet = SetupDiGetClassDevs(
            ref systemDeviceClassGuid,
            null,
            IntPtr.Zero,
            DigcfPresent);

        if (infoSet == new IntPtr(InvalidHandleValue))
        {
            return null;
        }

        try
        {
            for (uint index = 0; ; index++)
            {
                var deviceInfoData = new SpDevInfoData
                {
                    CbSize = Marshal.SizeOf<SpDevInfoData>()
                };

                if (!SetupDiEnumDeviceInfo(infoSet, index, ref deviceInfoData))
                {
                    var error = Marshal.GetLastWin32Error();
                    if (error == SetupApiNoMoreItems)
                    {
                        break;
                    }

                    continue;
                }

                var instanceId = GetDeviceInstanceId(infoSet, ref deviceInfoData);
                if (!IsMatchingBluetoothDeviceInstance(instanceId, normalizedAddress))
                {
                    continue;
                }

                var batteryLevel = ReadDevNodeBatteryLevel(deviceInfoData.DevInst);
                if (batteryLevel.HasValue)
                {
                    return batteryLevel;
                }
            }
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(infoSet);
        }

        return null;
    }

    private static string? GetDeviceInstanceId(IntPtr infoSet, ref SpDevInfoData deviceInfoData)
    {
        var instanceId = new StringBuilder(512);
        return SetupDiGetDeviceInstanceId(
            infoSet,
            ref deviceInfoData,
            instanceId,
            instanceId.Capacity,
            out _)
            ? instanceId.ToString()
            : null;
    }

    private static bool IsMatchingBluetoothDeviceInstance(string? instanceId, string normalizedBluetoothAddress)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return false;
        }

        var normalizedInstanceId = NormalizeBluetoothAddress(instanceId);
        return normalizedInstanceId.Contains(normalizedBluetoothAddress, StringComparison.OrdinalIgnoreCase);
    }

    private static int? ReadDevNodeBatteryLevel(uint deviceInstance)
    {
        var bufferSize = 0u;
        var propertyType = 0u;
        var key = BluetoothBatteryLevelPropertyKey;
        _ = CM_Get_DevNode_Property(
            deviceInstance,
            ref key,
            out propertyType,
            null,
            ref bufferSize,
            0);

        if (bufferSize == 0)
        {
            return null;
        }

        var buffer = new byte[bufferSize];
        key = BluetoothBatteryLevelPropertyKey;
        var result = CM_Get_DevNode_Property(
            deviceInstance,
            ref key,
            out propertyType,
            buffer,
            ref bufferSize,
            0);

        return result == CrSuccess ? NormalizeBatteryLevel(buffer) : null;
    }

    private static bool IsMatchingBluetoothPnpObject(
        PnpObject pnpObject,
        string deviceName,
        string? bluetoothAddress)
    {
        if (!string.IsNullOrWhiteSpace(bluetoothAddress))
        {
            var normalizedAddress = NormalizeBluetoothAddress(bluetoothAddress);
            if (!string.IsNullOrWhiteSpace(normalizedAddress))
            {
                var objectText = $"{pnpObject.Id} {GetStringProperty(pnpObject.Properties, DeviceAddressProperty)} {GetStringProperty(pnpObject.Properties, DeviceInstanceIdProperty)}";
                var normalizedObjectText = NormalizeBluetoothAddress(objectText);

                if (normalizedObjectText.Contains(normalizedAddress, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(deviceName))
        {
            return false;
        }

        var displayName = GetStringProperty(pnpObject.Properties, DisplayNameProperty);
        return !string.IsNullOrWhiteSpace(displayName)
            && displayName.Contains(deviceName, StringComparison.CurrentCultureIgnoreCase);
    }

    private static string? GetStringProperty(IReadOnlyDictionary<string, object> properties, string propertyName)
    {
        return properties.TryGetValue(propertyName, out var value)
            ? value?.ToString()
            : null;
    }

    private static int? ReadClassicBluetoothRegistryBatteryLevel(string bluetoothAddress)
    {
        if (string.IsNullOrWhiteSpace(bluetoothAddress))
        {
            return null;
        }

        var normalizedAddress = NormalizeBluetoothAddress(bluetoothAddress);

        if (normalizedAddress.Length != 12)
        {
            return null;
        }

        using var devicesKey = Registry.LocalMachine.OpenSubKey(BluetoothDeviceRegistryPath);
        using var deviceKey = devicesKey?.OpenSubKey(normalizedAddress)
            ?? devicesKey?.OpenSubKey(normalizedAddress.ToUpperInvariant());

        if (deviceKey is null)
        {
            return null;
        }

        return ReadBatteryLevelFromRegistryKey(deviceKey);
    }

    private static string NormalizeBluetoothAddress(string value)
    {
        return new string(value
            .Where(Uri.IsHexDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }

    private static int? ReadBatteryLevelFromRegistryKey(RegistryKey registryKey)
    {
        var batteryLevel = ReadBatteryLevelFromRegistryValues(registryKey);
        if (batteryLevel.HasValue)
        {
            return batteryLevel;
        }

        foreach (var subKeyName in registryKey.GetSubKeyNames())
        {
            using var subKey = registryKey.OpenSubKey(subKeyName);
            if (subKey is null)
            {
                continue;
            }

            batteryLevel = ReadBatteryLevelFromRegistryValues(subKey);
            if (batteryLevel.HasValue)
            {
                return batteryLevel;
            }
        }

        return null;
    }

    private static int? ReadBatteryLevelFromRegistryValues(RegistryKey registryKey)
    {
        foreach (var valueName in registryKey.GetValueNames())
        {
            if (!IsBatteryValueName(valueName))
            {
                continue;
            }

            var batteryLevel = NormalizeBatteryLevel(registryKey.GetValue(valueName));
            if (batteryLevel.HasValue)
            {
                return batteryLevel;
            }
        }

        return null;
    }

    private static bool IsBatteryValueName(string valueName)
    {
        return valueName.Contains("Battery", StringComparison.OrdinalIgnoreCase)
            || valueName.Equals("PowerLevel", StringComparison.OrdinalIgnoreCase)
            || valueName.Equals("Power Level", StringComparison.OrdinalIgnoreCase);
    }

    private static int? NormalizeBatteryLevel(object? value)
    {
        if (value is null)
        {
            return null;
        }

        var numericValue = value switch
        {
            byte byteValue => byteValue,
            sbyte sbyteValue => sbyteValue,
            short shortValue => shortValue,
            ushort ushortValue => ushortValue,
            int intValue => intValue,
            uint uintValue when uintValue <= int.MaxValue => (int)uintValue,
            long longValue when longValue is >= int.MinValue and <= int.MaxValue => (int)longValue,
            ulong ulongValue when ulongValue <= int.MaxValue => (int)ulongValue,
            float floatValue when !float.IsNaN(floatValue) && !float.IsInfinity(floatValue) => (int)Math.Round(floatValue),
            double doubleValue when !double.IsNaN(doubleValue) && !double.IsInfinity(doubleValue) => (int)Math.Round(doubleValue),
            decimal decimalValue when decimalValue is >= int.MinValue and <= int.MaxValue => (int)Math.Round(decimalValue),
            string stringValue when int.TryParse(stringValue.TrimEnd('%'), out var parsedValue) => parsedValue,
            byte[] byteArrayValue => NormalizeBatteryLevel(byteArrayValue),
            _ => (int?)null
        };

        if (!numericValue.HasValue || numericValue.Value < 0 || numericValue.Value > 100)
        {
            return null;
        }

        return numericValue.Value;
    }

    private static int? NormalizeBatteryLevel(byte[] value)
    {
        return value.Length switch
        {
            0 => null,
            1 => NormalizeBatteryLevel(value[0]),
            >= 4 => NormalizeBatteryLevel(BitConverter.ToInt32(value, 0)),
            _ => null
        };
    }

    private sealed record BatteryReadResult(int? BatteryLevel, string StatusMessage)
    {
        public static BatteryReadResult Success(int batteryLevel, string statusMessage)
        {
            return new BatteryReadResult(batteryLevel, statusMessage);
        }

        public static BatteryReadResult Unavailable(string statusMessage)
        {
            return new BatteryReadResult(null, statusMessage);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DevPropKey
    {
        public DevPropKey(Guid fmtid, uint pid)
        {
            Fmtid = fmtid;
            Pid = pid;
        }

        public Guid Fmtid;

        public uint Pid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SpDevInfoData
    {
        public int CbSize;

        public Guid ClassGuid;

        public uint DevInst;

        public IntPtr Reserved;
    }

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SetupDiGetClassDevs(
        ref Guid classGuid,
        string? enumerator,
        IntPtr hwndParent,
        uint flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiEnumDeviceInfo(
        IntPtr deviceInfoSet,
        uint memberIndex,
        ref SpDevInfoData deviceInfoData);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetupDiGetDeviceInstanceId(
        IntPtr deviceInfoSet,
        ref SpDevInfoData deviceInfoData,
        StringBuilder deviceInstanceId,
        int deviceInstanceIdSize,
        out int requiredSize);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern uint CM_Get_DevNode_Property(
        uint deviceInstance,
        ref DevPropKey propertyKey,
        out uint propertyType,
        byte[]? propertyBuffer,
        ref uint propertyBufferSize,
        uint flags);
}
