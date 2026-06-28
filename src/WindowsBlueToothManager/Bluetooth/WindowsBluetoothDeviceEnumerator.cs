using Microsoft.Win32;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
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
    private const string BluetoothDeviceRegistryPath = @"SYSTEM\CurrentControlSet\Services\BTHPORT\Parameters\Devices";
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
            var registryBatteryLevel = await ReadClassicBluetoothRegistryBatteryLevelAsync(device.Id, cancellationToken);
            if (registryBatteryLevel.HasValue)
            {
                return BatteryReadResult.Success(registryBatteryLevel.Value, "Battery read from Windows Bluetooth registry cache");
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

    private static async Task<int?> ReadClassicBluetoothRegistryBatteryLevelAsync(
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

            var address = bluetoothDevice.BluetoothAddress.ToString("x12");
            return ReadClassicBluetoothRegistryBatteryLevel(address);
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

    private static int? ReadClassicBluetoothRegistryBatteryLevel(string bluetoothAddress)
    {
        if (string.IsNullOrWhiteSpace(bluetoothAddress))
        {
            return null;
        }

        var normalizedAddress = bluetoothAddress.Replace(":", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();

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
}
