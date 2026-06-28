using WindowsBlueToothManager.Models;

namespace WindowsBlueToothManager.Bluetooth.Abstractions;

public interface IBluetoothDeviceEnumerator
{
    Task<IReadOnlyList<BluetoothDeviceInfo>> EnumerateAsync(CancellationToken cancellationToken);
}
