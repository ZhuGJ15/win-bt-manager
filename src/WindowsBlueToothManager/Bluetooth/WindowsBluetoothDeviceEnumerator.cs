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
    private static readonly TimeSpan BatteryReadTimeout = TimeSpan.FromSeconds(3);

    private static readonly string[] RequestedProperties =
    {
        IsConnectedProperty,
        BatteryLifeProperty,
        BatteryLevelProperty,
        BatteryPercentageProperty,
        PowerLevelProperty
    };

    public async Task<IReadOnlyList<BluetoothDeviceInfo>> EnumerateAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.Now;
        var devices = new Dictionary<string, BluetoothDeviceInfo>(StringComparer.OrdinalIgnoreCase);

        await AddDevicesAsync(
            devices,
            BluetoothLEDevice.GetDeviceSelector(),
            DeviceType.Ble,
            now,
            cancellationToken);

        await AddDevicesAsync(
            devices,
            BluetoothDevice.GetDeviceSelector(),
            DeviceType.ClassicBluetooth,
            now,
            cancellationToken);

        return devices.Values
            .OrderByDescending(device => device.IsConnected)
            .ThenBy(device => device.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static async Task AddDevicesAsync(
        IDictionary<string, BluetoothDeviceInfo> devices,
        string selector,
        DeviceType deviceType,
        DateTime now,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var foundDevices = await DeviceInformation.FindAllAsync(selector, RequestedProperties);
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

        var propertyBatteryLevel = ReadBatteryLevelFromProperties(device);
        return propertyBatteryLevel.HasValue
            ? BatteryReadResult.Success(propertyBatteryLevel.Value, "Battery read from Windows device properties")
            : BatteryReadResult.Unavailable("Battery unavailable");
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

    private static int? ReadBatteryLevelFromProperties(DeviceInformation device)
    {
        foreach (var propertyName in RequestedProperties.Where(property => property != IsConnectedProperty))
        {
            if (!device.Properties.TryGetValue(propertyName, out var value))
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
            _ => (int?)null
        };

        if (!numericValue.HasValue || numericValue.Value < 0 || numericValue.Value > 100)
        {
            return null;
        }

        return numericValue.Value;
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
