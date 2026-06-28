using System.Collections.ObjectModel;
using System.Collections.Specialized;
using WindowsBlueToothManager.Models;

namespace WindowsBlueToothManager.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private readonly Random _random = new();
    private DateTime _lastRefreshAt;
    private string _statusText = string.Empty;

    public MainWindowViewModel()
    {
        Devices.CollectionChanged += OnDevicesChanged;
        RefreshDevices();
    }

    public ObservableCollection<DeviceListItemViewModel> Devices { get; } = new();

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
        ? "Not refreshed"
        : LastRefreshAt.ToString("yyyy-MM-dd HH:mm:ss");

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public int ConnectedDeviceCount => Devices.Count(device => device.IsConnected);

    public int OverlayDeviceCount => Devices.Count(device => device.ShowInTaskbarOverlay);

    public int LowBatteryDeviceCount => Devices.Count(device => device.BatteryLevel is < 20);

    public string SummaryText =>
        $"{ConnectedDeviceCount} connected, {OverlayDeviceCount} shown at bottom, {LowBatteryDeviceCount} low battery";

    public void RefreshDevices()
    {
        var now = DateTime.Now;
        Devices.Clear();

        foreach (var device in CreateSampleDevices(now))
        {
            var item = new DeviceListItemViewModel(device);
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
        StatusText = "Displaying simulated Bluetooth device data.";
        NotifySummaryChanged();
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
}
