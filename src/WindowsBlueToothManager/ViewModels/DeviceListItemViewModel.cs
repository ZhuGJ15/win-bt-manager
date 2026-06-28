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
        Address = device.Address;
        DeviceType = device.DeviceType;
        IsConnected = device.IsConnected;
        BatteryLevel = device.BatteryLevel;
        LastUpdatedAt = device.LastUpdatedAt;
        StatusMessage = device.StatusMessage;
        _language = language;
        _showInTaskbarOverlay = CanSelectDisplayTargets && device.ShowInTaskbarOverlay;
        _showInTray = CanSelectDisplayTargets && device.ShowInTray;
    }

    public string DeviceId { get; }

    public string Name { get; }

    public string? Address { get; }

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
            var normalizedValue = CanSelectDisplayTargets && value;
            if (SetProperty(ref _showInTaskbarOverlay, normalizedValue))
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
            var normalizedValue = CanSelectDisplayTargets && value;
            if (SetProperty(ref _showInTray, normalizedValue))
            {
                OnPropertyChanged(nameof(DisplayTargetText));
            }
        }
    }

    public bool CanSelectDisplayTargets =>
        IsConnected
        && DeviceType != DeviceType.Unknown
        && BatteryLevel.HasValue;

    public string DisplaySelectionTooltip
    {
        get
        {
            if (CanSelectDisplayTargets)
            {
                return Translate("可显示在托盘或底部", "Can be shown in tray or bottom area");
            }

            if (!IsConnected)
            {
                return Translate("未连接设备不能显示在托盘或底部", "Disconnected devices cannot be shown in tray or bottom area");
            }

            if (DeviceType == DeviceType.Unknown)
            {
                return Translate("无法识别的设备不能显示在托盘或底部", "Unrecognized devices cannot be shown in tray or bottom area");
            }

            return Translate("未获取到电量的设备不能显示在托盘或底部", "Devices without battery data cannot be shown in tray or bottom area");
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

    public string ConnectionStatusBrush => IsConnected ? "#15803D" : "#94A3B8";

    public string BatteryText
    {
        get
        {
            if (BatteryLevel.HasValue)
            {
                return $"{BatteryLevel.Value}%";
            }

            return IsConnected && !IsBatteryUnavailable ? Translate("待获取", "Pending") : "-1";
        }
    }

    public int BatteryProgressValue => BatteryLevel ?? 0;

    public string BatteryProgressBrush
    {
        get
        {
            if (!BatteryLevel.HasValue)
            {
                return "#CBD5E1";
            }

            if (BatteryLevel.Value < 20)
            {
                return "#DC2626";
            }

            return BatteryLevel.Value < 50 ? "#EAB308" : "#16A34A";
        }
    }

    public string BatteryStateText
    {
        get
        {
            if (!BatteryLevel.HasValue)
            {
                return IsConnected && !IsBatteryUnavailable
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

    private bool IsBatteryUnavailable => string.Equals(
        StatusMessage,
        "Battery unavailable",
        StringComparison.OrdinalIgnoreCase);

    private string Translate(string chinese, string english)
    {
        return Language == AppLanguage.Chinese ? chinese : english;
    }

    private void NotifyLocalizedPropertiesChanged()
    {
        OnPropertyChanged(nameof(DeviceTypeText));
        OnPropertyChanged(nameof(ConnectionStatusText));
        OnPropertyChanged(nameof(ConnectionStatusBrush));
        OnPropertyChanged(nameof(BatteryText));
        OnPropertyChanged(nameof(BatteryStateText));
        OnPropertyChanged(nameof(BatteryProgressBrush));
        OnPropertyChanged(nameof(DisplayTargetText));
        OnPropertyChanged(nameof(DisplaySelectionTooltip));
    }
}
