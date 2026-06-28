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
    private static readonly TimeSpan BatteryReadTimeout = TimeSpan.FromSeconds(3);

    private static readonly string[] RequestedProperties =
    {
        IsConnectedProperty
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
            var batteryLevel = isConnected && deviceType == DeviceType.Ble
                ? await ReadBleBatteryLevelWithTimeoutAsync(device.Id, cancellationToken)
                : null;

            devices[device.Id] = new BluetoothDeviceInfo
            {
                DeviceId = device.Id,
                Name = string.IsNullOrWhiteSpace(device.Name) ? "Unknown Bluetooth Device" : device.Name,
                DeviceType = deviceType,
                IsConnected = isConnected,
                BatteryLevel = batteryLevel,
                ShowInTaskbarOverlay = false,
                ShowInTray = false,
                LastUpdatedAt = now,
                StatusMessage = batteryLevel.HasValue
                    ? "Battery read from BLE Battery Service"
                    : "Battery unavailable"
            };
        }
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
            return Math.Clamp(reader.ReadByte(), 0, 100);
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
}
