using WindowsBlueToothManager.Models;

namespace WindowsBlueToothManager.ViewModels;

public sealed class DeviceListItemViewModel : ObservableObject
{
    private bool _showInTaskbarOverlay;
    private bool _showInTray;
    private AppLanguage _language;

    public DeviceListItemViewModel(BluetoothDeviceInfo device, AppLanguage language)
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
        _language = language;
    }

    public string DeviceId { get; }

    public string Name { get; }

    public DeviceType DeviceType { get; }

    public bool IsConnected { get; }

    public int? BatteryLevel { get; }

    public DateTime LastUpdatedAt { get; }

    public string? StatusMessage { get; }

    public AppLanguage Language
    {
        get => _language;
        set
        {
            if (SetProperty(ref _language, value))
            {
                NotifyLocalizedPropertiesChanged();
            }
        }
    }

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
        _ => Language == AppLanguage.Chinese ? "未知" : "Unknown"
    };

    public string ConnectionStatusText => IsConnected
        ? Translate("已连接", "Connected")
        : Translate("已断开", "Disconnected");

    public string BatteryText
    {
        get
        {
            if (BatteryLevel.HasValue)
            {
                return $"{BatteryLevel.Value}%";
            }

            return IsConnected ? Translate("待获取", "Pending") : "-1";
        }
    }

    public int BatteryProgressValue => BatteryLevel ?? 0;

    public string BatteryStateText
    {
        get
        {
            if (!BatteryLevel.HasValue)
            {
                return IsConnected
                    ? Translate("等待电量读取", "Waiting for battery reading")
                    : Translate("无法获取电量", "Battery unavailable");
            }

            return BatteryLevel.Value < 20 ? Translate("低电量", "Low battery") : Translate("正常", "Normal");
        }
    }

    public string DisplayTargetText
    {
        get
        {
            if (ShowInTray && ShowInTaskbarOverlay)
            {
                return Translate("托盘 + 底部", "Tray + Bottom");
            }

            if (ShowInTray)
            {
                return Translate("托盘", "Tray");
            }

            if (ShowInTaskbarOverlay)
            {
                return Translate("底部", "Bottom");
            }

            return Translate("隐藏", "Hidden");
        }
    }

    public string LastUpdatedText => LastUpdatedAt.ToString("HH:mm:ss");

    private string Translate(string chinese, string english)
    {
        return Language == AppLanguage.Chinese ? chinese : english;
    }

    private void NotifyLocalizedPropertiesChanged()
    {
        OnPropertyChanged(nameof(DeviceTypeText));
        OnPropertyChanged(nameof(ConnectionStatusText));
        OnPropertyChanged(nameof(BatteryText));
        OnPropertyChanged(nameof(BatteryStateText));
        OnPropertyChanged(nameof(DisplayTargetText));
    }
}
