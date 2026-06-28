namespace WindowsBlueToothManager.Models;

public sealed record BluetoothDeviceInfo
{
    public required string DeviceId { get; init; }

    public required string Name { get; init; }

    public required DeviceType DeviceType { get; init; }

    public required bool IsConnected { get; init; }

    public int? BatteryLevel { get; init; }

    public required bool ShowInTaskbarOverlay { get; init; }

    public required bool ShowInTray { get; init; }

    public required DateTime LastUpdatedAt { get; init; }

    public string? StatusMessage { get; init; }
}
