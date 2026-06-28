using WindowsBlueToothManager.Models;

namespace WindowsBlueToothManager.ViewModels;

public sealed class DeviceListItemViewModel : ObservableObject
{
    private bool _showInTaskbarOverlay;
    private bool _showInTray;

    public DeviceListItemViewModel(BluetoothDeviceInfo device)
    {
        DeviceId = device.DeviceId;
        Name = device.Name;
        DeviceType = device.DeviceType;
        IsConnected = device.IsConnected;
        BatteryLevel = device.BatteryLevel;
        _showInTaskbarOverlay = device.ShowInTaskbarOverlay;
        _showInTray = device.ShowInTray;
        LastUpdatedAt = device.LastUpdatedAt;
        StatusMessage = device.StatusMessage;
    }

    public string DeviceId { get; }

    public string Name { get; }

    public DeviceType DeviceType { get; }

    public bool IsConnected { get; }

    public int? BatteryLevel { get; }

    public DateTime LastUpdatedAt { get; }

    public string? StatusMessage { get; }

    public bool ShowInTaskbarOverlay
    {
        get => _showInTaskbarOverlay;
        set
        {
            if (SetProperty(ref _showInTaskbarOverlay, value))
            {
                OnPropertyChanged(nameof(DisplayTargetText));
            }
        }
    }

    public bool ShowInTray
    {
        get => _showInTray;
        set
        {
            if (SetProperty(ref _showInTray, value))
            {
                OnPropertyChanged(nameof(DisplayTargetText));
            }
        }
    }

    public string DeviceTypeText => DeviceType switch
    {
        DeviceType.Ble => "BLE",
        DeviceType.ClassicBluetooth => "BTC",
        _ => "Unknown"
    };

    public string ConnectionStatusText => IsConnected ? "Connected" : "Disconnected";

    public string BatteryText => BatteryLevel.HasValue ? $"{BatteryLevel.Value}%" : "Unknown";

    public int BatteryProgressValue => BatteryLevel ?? 0;

    public string BatteryStateText
    {
        get
        {
            if (!BatteryLevel.HasValue)
            {
                return StatusMessage ?? "Battery unavailable";
            }

            return BatteryLevel.Value < 20 ? "Low battery" : "Normal";
        }
    }

    public string DisplayTargetText
    {
        get
        {
            if (ShowInTray && ShowInTaskbarOverlay)
            {
                return "Tray + Bottom";
            }

            if (ShowInTray)
            {
                return "Tray";
            }

            if (ShowInTaskbarOverlay)
            {
                return "Bottom";
            }

            return "Hidden";
        }
    }

    public string LastUpdatedText => LastUpdatedAt.ToString("HH:mm:ss");
}
