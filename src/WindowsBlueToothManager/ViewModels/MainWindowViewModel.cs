using System.Collections.ObjectModel;
using System.Collections.Specialized;
using WindowsBlueToothManager.Models;

namespace WindowsBlueToothManager.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private readonly Random _random = new();
    private DateTime _lastRefreshAt;
    private LanguageOption _selectedLanguageOption;
    private TimeSpan _selectedRefreshInterval = TimeSpan.FromSeconds(5);

    public MainWindowViewModel()
    {
        LanguageOptions = new ObservableCollection<LanguageOption>
        {
            new(AppLanguage.Chinese, "中文"),
            new(AppLanguage.English, "English")
        };
        _selectedLanguageOption = LanguageOptions[0];
        Devices.CollectionChanged += OnDevicesChanged;
        RefreshDevices();
    }

    public ObservableCollection<LanguageOption> LanguageOptions { get; }

    public ObservableCollection<DeviceListItemViewModel> Devices { get; } = new();

    public LanguageOption SelectedLanguageOption
    {
        get => _selectedLanguageOption;
        set
        {
            if (value is null)
            {
                return;
            }

            if (SetProperty(ref _selectedLanguageOption, value))
            {
                foreach (var device in Devices)
                {
                    device.Language = CurrentLanguage;
                }

                NotifyLocalizedTextChanged();
                NotifySummaryChanged();
            }
        }
    }

    public AppLanguage CurrentLanguage => SelectedLanguageOption.Language;

    public TimeSpan SelectedRefreshInterval
    {
        get => _selectedRefreshInterval;
        private set
        {
            if (SetProperty(ref _selectedRefreshInterval, value))
            {
                NotifyRefreshIntervalPropertiesChanged();
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    public DateTime LastRefreshAt
    {
        get => _lastRefreshAt;
        private set
        {
            if (SetProperty(ref _lastRefreshAt, value))
            {
                OnPropertyChanged(nameof(LastRefreshText));
            }
        }
    }

    public string LastRefreshText => LastRefreshAt == default
        ? Translate("未刷新", "Not refreshed")
        : $"{Translate("最后刷新", "Last refresh")}: {LastRefreshAt:yyyy-MM-dd HH:mm:ss}";

    public string StatusText => CurrentLanguage == AppLanguage.Chinese
        ? $"正在显示模拟蓝牙设备数据。自动刷新频率：{RefreshIntervalText}"
        : $"Displaying simulated Bluetooth device data. Auto refresh: {RefreshIntervalText}";

    public int ConnectedDeviceCount => Devices.Count(device => device.IsConnected);

    public int OverlayDeviceCount => Devices.Count(device => device.ShowInTaskbarOverlay);

    public int LowBatteryDeviceCount => Devices.Count(device => device.BatteryLevel is < 20);

    public string SummaryText => CurrentLanguage == AppLanguage.Chinese
        ? $"{ConnectedDeviceCount} 个已连接，{OverlayDeviceCount} 个显示在底部，{LowBatteryDeviceCount} 个低电量"
        : $"{ConnectedDeviceCount} connected, {OverlayDeviceCount} shown at bottom, {LowBatteryDeviceCount} low battery";

    public string LanguageLabelText => Translate("语言", "Language");

    public string SettingsMenuText => Translate("设置", "Settings");

    public string LanguageMenuText => Translate("语言", "Language");

    public string RefreshFrequencyMenuText => Translate("刷新频率", "Refresh frequency");

    public string ChineseLanguageText => "中文";

    public string EnglishLanguageText => "English";

    public bool IsChineseLanguageSelected => CurrentLanguage == AppLanguage.Chinese;

    public bool IsEnglishLanguageSelected => CurrentLanguage == AppLanguage.English;

    public string Refresh5SecondsText => Translate("5 秒", "5 seconds");

    public string Refresh10SecondsText => Translate("10 秒", "10 seconds");

    public string Refresh30SecondsText => Translate("30 秒", "30 seconds");

    public string Refresh1MinuteText => Translate("1 分钟", "1 minute");

    public bool IsRefresh5SecondsSelected => SelectedRefreshInterval == TimeSpan.FromSeconds(5);

    public bool IsRefresh10SecondsSelected => SelectedRefreshInterval == TimeSpan.FromSeconds(10);

    public bool IsRefresh30SecondsSelected => SelectedRefreshInterval == TimeSpan.FromSeconds(30);

    public bool IsRefresh1MinuteSelected => SelectedRefreshInterval == TimeSpan.FromMinutes(1);

    public string RefreshIntervalText => SelectedRefreshInterval.TotalSeconds switch
    {
        5 => Refresh5SecondsText,
        10 => Refresh10SecondsText,
        30 => Refresh30SecondsText,
        60 => Refresh1MinuteText,
        _ => Translate($"{SelectedRefreshInterval.TotalSeconds:0} 秒", $"{SelectedRefreshInterval.TotalSeconds:0} seconds")
    };

    public string ConnectedLabelText => Translate("已连接", "Connected");

    public string ShownAtBottomLabelText => Translate("底部显示", "Shown at bottom");

    public string LowBatteryLabelText => Translate("低电量", "Low battery");

    public string DeviceListTitleText => Translate("已连接蓝牙设备", "Connected Bluetooth devices");

    public string FooterText => Translate(
        "模拟数据模式。真实蓝牙扫描将在后续硬件集成阶段接入。",
        "Simulated data mode. Real Bluetooth scanning will be connected in the next hardware integration phase.");

    public string DeviceHeaderText => Translate("设备", "Device");

    public string TypeHeaderText => Translate("类型", "Type");

    public string StatusHeaderText => Translate("状态", "Status");

    public string BatteryHeaderText => Translate("电量", "Battery");

    public string BottomHeaderText => Translate("底部", "Bottom");

    public string TrayHeaderText => Translate("托盘", "Tray");

    public string DisplayHeaderText => Translate("显示位置", "Display");

    public string UpdatedHeaderText => Translate("更新时间", "Updated");

    public void RefreshDevices()
    {
        var now = DateTime.Now;
        var displayPreferences = Devices.ToDictionary(
            device => device.DeviceId,
            device => (device.ShowInTaskbarOverlay, device.ShowInTray));
        Devices.Clear();

        foreach (var device in CreateSampleDevices(now))
        {
            var deviceToDisplay = device;
            if (displayPreferences.TryGetValue(device.DeviceId, out var preference))
            {
                deviceToDisplay = device with
                {
                    ShowInTaskbarOverlay = preference.ShowInTaskbarOverlay,
                    ShowInTray = preference.ShowInTray
                };
            }

            var item = new DeviceListItemViewModel(deviceToDisplay, CurrentLanguage);
            item.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName is nameof(DeviceListItemViewModel.ShowInTaskbarOverlay)
                    or nameof(DeviceListItemViewModel.ShowInTray))
                {
                    NotifySummaryChanged();
                }
            };
            Devices.Add(item);
        }

        LastRefreshAt = now;
        NotifySummaryChanged();
    }

    public void SetRefreshInterval(TimeSpan interval)
    {
        if (SelectedRefreshInterval == interval)
        {
            NotifyRefreshIntervalPropertiesChanged();
            return;
        }

        SelectedRefreshInterval = interval;
    }

    public void SetLanguage(AppLanguage language)
    {
        var languageOption = LanguageOptions.First(option => option.Language == language);
        if (SelectedLanguageOption.Language == language)
        {
            NotifyLocalizedTextChanged();
            return;
        }

        SelectedLanguageOption = languageOption;
    }

    private IReadOnlyList<BluetoothDeviceInfo> CreateSampleDevices(DateTime now)
    {
        return new List<BluetoothDeviceInfo>
        {
            new BluetoothDeviceInfo
            {
                DeviceId = "mock-ble-headset",
                Name = "Surface Headphones",
                DeviceType = DeviceType.Ble,
                IsConnected = true,
                BatteryLevel = NextBatteryLevel(78),
                ShowInTaskbarOverlay = true,
                ShowInTray = true,
                LastUpdatedAt = now,
                StatusMessage = "Battery read from simulated BLE data"
            },
            new BluetoothDeviceInfo
            {
                DeviceId = "mock-btc-mouse",
                Name = "Bluetooth Mouse",
                DeviceType = DeviceType.ClassicBluetooth,
                IsConnected = true,
                BatteryLevel = NextBatteryLevel(43),
                ShowInTaskbarOverlay = true,
                ShowInTray = false,
                LastUpdatedAt = now,
                StatusMessage = "Battery read from simulated BTC data"
            },
            new BluetoothDeviceInfo
            {
                DeviceId = "mock-ble-keyboard",
                Name = "Designer Keyboard",
                DeviceType = DeviceType.Ble,
                IsConnected = true,
                BatteryLevel = 16,
                ShowInTaskbarOverlay = false,
                ShowInTray = true,
                LastUpdatedAt = now,
                StatusMessage = "Below low-battery threshold"
            },
            new BluetoothDeviceInfo
            {
                DeviceId = "mock-unknown-gamepad",
                Name = "Wireless Controller",
                DeviceType = DeviceType.Unknown,
                IsConnected = true,
                BatteryLevel = null,
                ShowInTaskbarOverlay = false,
                ShowInTray = false,
                LastUpdatedAt = now,
                StatusMessage = "Battery read failed in simulated data"
            }
        };
    }

    private int NextBatteryLevel(int baseLevel)
    {
        return Math.Clamp(baseLevel + _random.Next(-3, 4), 1, 100);
    }

    private void OnDevicesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        NotifySummaryChanged();
    }

    private void NotifySummaryChanged()
    {
        OnPropertyChanged(nameof(ConnectedDeviceCount));
        OnPropertyChanged(nameof(OverlayDeviceCount));
        OnPropertyChanged(nameof(LowBatteryDeviceCount));
        OnPropertyChanged(nameof(SummaryText));
    }

    private void NotifyLocalizedTextChanged()
    {
        OnPropertyChanged(nameof(LastRefreshText));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(LanguageLabelText));
        OnPropertyChanged(nameof(SettingsMenuText));
        OnPropertyChanged(nameof(LanguageMenuText));
        OnPropertyChanged(nameof(RefreshFrequencyMenuText));
        OnPropertyChanged(nameof(ChineseLanguageText));
        OnPropertyChanged(nameof(EnglishLanguageText));
        OnPropertyChanged(nameof(IsChineseLanguageSelected));
        OnPropertyChanged(nameof(IsEnglishLanguageSelected));
        NotifyRefreshIntervalPropertiesChanged();
        OnPropertyChanged(nameof(ConnectedLabelText));
        OnPropertyChanged(nameof(ShownAtBottomLabelText));
        OnPropertyChanged(nameof(LowBatteryLabelText));
        OnPropertyChanged(nameof(DeviceListTitleText));
        OnPropertyChanged(nameof(FooterText));
        OnPropertyChanged(nameof(DeviceHeaderText));
        OnPropertyChanged(nameof(TypeHeaderText));
        OnPropertyChanged(nameof(StatusHeaderText));
        OnPropertyChanged(nameof(BatteryHeaderText));
        OnPropertyChanged(nameof(BottomHeaderText));
        OnPropertyChanged(nameof(TrayHeaderText));
        OnPropertyChanged(nameof(DisplayHeaderText));
        OnPropertyChanged(nameof(UpdatedHeaderText));
    }

    private string Translate(string chinese, string english)
    {
        return CurrentLanguage == AppLanguage.Chinese ? chinese : english;
    }

    private void NotifyRefreshIntervalPropertiesChanged()
    {
        OnPropertyChanged(nameof(Refresh5SecondsText));
        OnPropertyChanged(nameof(Refresh10SecondsText));
        OnPropertyChanged(nameof(Refresh30SecondsText));
        OnPropertyChanged(nameof(Refresh1MinuteText));
        OnPropertyChanged(nameof(IsRefresh5SecondsSelected));
        OnPropertyChanged(nameof(IsRefresh10SecondsSelected));
        OnPropertyChanged(nameof(IsRefresh30SecondsSelected));
        OnPropertyChanged(nameof(IsRefresh1MinuteSelected));
        OnPropertyChanged(nameof(RefreshIntervalText));
    }
}
