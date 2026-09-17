using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using SennheiserMomentum4.Models;
using SennheiserMomentum4.Services.Device;
using SennheiserMomentum4.Services.Logging;

namespace SennheiserMomentum4.Services.Windows;

public interface ISystemTrayService : IDisposable
{
    void Initialize(IntPtr windowHandle, Action showAppAction, Action showSettingsAction, Action exitAction);
    void UpdateState(DeviceInfo deviceInfo, NoiseControlState noiseState);
}

public class SystemTrayService : ISystemTrayService
{
    private readonly IMomentum4DeviceService _deviceService;
    private readonly IMediaTransportService _mediaService;
    private readonly IAppLogger _logger;

    private IntPtr _hwnd = IntPtr.Zero;
    private HwndSource? _hwndSource;
    private bool _isInitialized = false;

    private Action? _showAppAction;
    private Action? _showSettingsAction;
    private Action? _exitAction;

    // Win32 Constants
    private const int WM_USER = 0x0400;
    private const int WM_TRAYICON = WM_USER + 100;
    private const int WM_RBUTTONUP = 0x0205;
    private const int WM_LBUTTONDBLCLK = 0x0203;

    private const uint NIM_ADD = 0x00000000;
    private const uint NIM_MODIFY = 0x00000001;
    private const uint NIM_DELETE = 0x00000002;

    private const uint NIF_MESSAGE = 0x00000001;
    private const uint NIF_ICON = 0x00000002;
    private const uint NIF_TIP = 0x00000004;

    private const uint MF_STRING = 0x00000000;
    private const uint MF_SEPARATOR = 0x00000800;
    private const uint MF_CHECKED = 0x00000008;
    private const uint MF_GRAYED = 0x00000001;
    private const uint MF_POPUP = 0x00000010;

    private const uint TPM_BOTTOMALIGN = 0x0020;
    private const uint TPM_LEFTALIGN = 0x0000;
    private const uint TPM_RETURNCMD = 0x0100;

    // Menu Command IDs
    private const int CMD_OPEN_APP = 1001;
    private const int CMD_SETTINGS = 1002;
    private const int CMD_EXIT = 1003;
    private const int CMD_ANC = 1004;
    private const int CMD_TRANSPARENCY = 1005;
    private const int CMD_OFF = 1006;
    private const int CMD_PLAYPAUSE = 1007;
    private const int CMD_PREV = 1008;
    private const int CMD_NEXT = 1009;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uID;
        public uint uFlags;
        public int uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public int dwState;
        public int dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public int uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public int dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpdata);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll")]
    private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, uint uIDNewItem, string lpNewItem);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    private static extern uint TrackPopupMenuEx(IntPtr hMenu, uint uFlags, int x, int y, IntPtr hWnd, IntPtr lptpm);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    public SystemTrayService(
        IMomentum4DeviceService deviceService,
        IMediaTransportService mediaService,
        IAppLogger logger)
    {
        _deviceService = deviceService;
        _mediaService = mediaService;
        _logger = logger;
    }

    public void Initialize(IntPtr windowHandle, Action showAppAction, Action showSettingsAction, Action exitAction)
    {
        _showAppAction = showAppAction;
        _showSettingsAction = showSettingsAction;
        _exitAction = exitAction;
        _hwnd = windowHandle;

        _hwndSource = HwndSource.FromHwnd(windowHandle);
        _hwndSource?.AddHook(WndProc);

        AddTrayIcon();
        _isInitialized = true;
        _logger.App(LogLevel.Info, "Native Win32 system tray icon initialized successfully.");
    }

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr ExtractIcon(IntPtr hInst, string lpszExeFileName, int nIconIndex);

    private IntPtr GetAppIconHandle()
    {
        try
        {
            var path = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path))
            {
                var hIcon = ExtractIcon(IntPtr.Zero, path, 0);
                if (hIcon != IntPtr.Zero) return hIcon;
            }
        }
        catch { }
        return LoadIcon(IntPtr.Zero, (IntPtr)32512); // Fallback IDI_APPLICATION
    }

    private void AddTrayIcon()
    {
        var nid = new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)),
            hWnd = _hwnd,
            uID = 1,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
            uCallbackMessage = WM_TRAYICON,
            hIcon = GetAppIconHandle(),
            szTip = "Sennheiser MOMENTUM 4"
        };

        Shell_NotifyIcon(NIM_ADD, ref nid);
    }

    public void UpdateState(DeviceInfo deviceInfo, NoiseControlState noiseState)
    {
        if (!_isInitialized || _hwnd == IntPtr.Zero) return;

        var statusText = deviceInfo.State == ConnectionState.Connected
            ? $"MOMENTUM 4: {deviceInfo.BatteryPercentage}%"
            : "MOMENTUM 4: Disconnected";

        var nid = new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)),
            hWnd = _hwnd,
            uID = 1,
            uFlags = NIF_TIP,
            szTip = statusText
        };

        Shell_NotifyIcon(NIM_MODIFY, ref nid);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_TRAYICON)
        {
            int eventId = lParam.ToInt32();
            if (eventId == WM_RBUTTONUP)
            {
                ShowContextMenu();
                handled = true;
            }
            else if (eventId == WM_LBUTTONDBLCLK)
            {
                _showAppAction?.Invoke();
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    private void ShowContextMenu()
    {
        GetCursorPos(out POINT pt);
        SetForegroundWindow(_hwnd);

        IntPtr hMenu = CreatePopupMenu();
        IntPtr hNoiseMenu = CreatePopupMenu();
        IntPtr hPlayMenu = CreatePopupMenu();

        var dev = _deviceService.DeviceInfo;
        var noise = _deviceService.NoiseControl;

        // Title and connection status
        AppendMenu(hMenu, MF_STRING | MF_GRAYED, 0, "MOMENTUM 4 Wireless");
        var statusStr = dev.State == ConnectionState.Connected
            ? $"● Connected ({dev.BatteryPercentage}%)"
            : "○ Disconnected";
        AppendMenu(hMenu, MF_STRING | MF_GRAYED, 0, statusStr);
        AppendMenu(hMenu, MF_SEPARATOR, 0, string.Empty);

        // Noise Control Submenu
        uint ancFlags = MF_STRING | (noise.Mode == NoiseControlMode.AdaptiveAnc ? MF_CHECKED : 0);
        uint transFlags = MF_STRING | (noise.Mode == NoiseControlMode.Transparency ? MF_CHECKED : 0);
        uint offFlags = MF_STRING | (noise.Mode == NoiseControlMode.Off ? MF_CHECKED : 0);

        AppendMenu(hNoiseMenu, ancFlags, CMD_ANC, "Adaptive ANC");
        AppendMenu(hNoiseMenu, transFlags, CMD_TRANSPARENCY, "Transparency");
        AppendMenu(hNoiseMenu, offFlags, CMD_OFF, "Off");
        AppendMenu(hMenu, MF_POPUP, (uint)hNoiseMenu.ToInt64(), "Noise Control");

        // Playback Submenu
        AppendMenu(hPlayMenu, MF_STRING, CMD_PLAYPAUSE, "Play / Pause");
        AppendMenu(hPlayMenu, MF_STRING, CMD_PREV, "Previous");
        AppendMenu(hPlayMenu, MF_STRING, CMD_NEXT, "Next");
        AppendMenu(hMenu, MF_POPUP, (uint)hPlayMenu.ToInt64(), "Playback");

        AppendMenu(hMenu, MF_SEPARATOR, 0, string.Empty);
        AppendMenu(hMenu, MF_STRING, CMD_OPEN_APP, "Open App");
        AppendMenu(hMenu, MF_STRING, CMD_SETTINGS, "Settings");
        AppendMenu(hMenu, MF_SEPARATOR, 0, string.Empty);
        AppendMenu(hMenu, MF_STRING, CMD_EXIT, "Exit");

        uint cmd = TrackPopupMenuEx(hMenu, TPM_RETURNCMD | TPM_BOTTOMALIGN | TPM_LEFTALIGN, pt.X, pt.Y, _hwnd, IntPtr.Zero);

        DestroyMenu(hPlayMenu);
        DestroyMenu(hNoiseMenu);
        DestroyMenu(hMenu);

        HandleCommand((int)cmd);
    }

    private async void HandleCommand(int cmdId)
    {
        switch (cmdId)
        {
            case CMD_OPEN_APP:
                _showAppAction?.Invoke();
                break;
            case CMD_SETTINGS:
                _showSettingsAction?.Invoke();
                break;
            case CMD_EXIT:
                _exitAction?.Invoke();
                break;
            case CMD_ANC:
                await SetNoiseMode(NoiseControlMode.AdaptiveAnc);
                break;
            case CMD_TRANSPARENCY:
                await SetNoiseMode(NoiseControlMode.Transparency);
                break;
            case CMD_OFF:
                await SetNoiseMode(NoiseControlMode.Off);
                break;
            case CMD_PLAYPAUSE:
                await _mediaService.TogglePlayPauseAsync();
                break;
            case CMD_PREV:
                await _mediaService.PreviousAsync();
                break;
            case CMD_NEXT:
                await _mediaService.NextAsync();
                break;
        }
    }

    private async System.Threading.Tasks.Task SetNoiseMode(NoiseControlMode mode)
    {
        var cur = _deviceService.NoiseControl;
        await _deviceService.SetNoiseControlAsync(new NoiseControlState
        {
            Mode = mode,
            AncIntensity = cur.AncIntensity,
            TransparencyIntensity = cur.TransparencyIntensity,
            WindNoiseReduction = cur.WindNoiseReduction
        });
    }

    public void Dispose()
    {
        if (_hwnd != IntPtr.Zero)
        {
            var nid = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)),
                hWnd = _hwnd,
                uID = 1
            };
            Shell_NotifyIcon(NIM_DELETE, ref nid);

            _hwndSource?.RemoveHook(WndProc);
            _hwndSource?.Dispose();
            _hwnd = IntPtr.Zero;
        }
    }
}
