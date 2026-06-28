using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using WindowsBlueToothManager.Bluetooth.Abstractions;
using WindowsBlueToothManager.Models;

namespace WindowsBlueToothManager.Bluetooth;

public sealed class WindowsBluetoothDeviceEnumerator : IBluetoothDeviceEnumerator
{
    private const string IsConnectedProperty = "System.Devices.Aep.IsConnected";

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

            devices[device.Id] = new BluetoothDeviceInfo
            {
                DeviceId = device.Id,
                Name = string.IsNullOrWhiteSpace(device.Name) ? "Unknown Bluetooth Device" : device.Name,
                DeviceType = deviceType,
                IsConnected = ReadBooleanProperty(device, IsConnectedProperty),
                BatteryLevel = null,
                ShowInTaskbarOverlay = false,
                ShowInTray = false,
                LastUpdatedAt = now,
                StatusMessage = "Battery reading is not implemented yet"
            };
        }
    }

    private static bool ReadBooleanProperty(DeviceInformation device, string propertyName)
    {
        return device.Properties.TryGetValue(propertyName, out var value)
            && value is bool boolValue
            && boolValue;
    }
}
