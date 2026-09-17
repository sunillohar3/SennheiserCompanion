using System;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using SennheiserMomentum4.Services.Logging;

namespace SennheiserMomentum4.Services.Windows;

public interface IGlobalHotkeyService
{
    event Action? ToggleAncRequested;
    event Action? ToggleTransparencyRequested;
    event Action? PlayPauseRequested;

    void Initialize(IntPtr windowHandle);
    void RegisterHotkeys(string toggleAnc, string toggleTransparency, string playPause);
    void UnregisterHotkeys();
}

public class GlobalHotkeyService : IGlobalHotkeyService, IDisposable
{
    private readonly IAppLogger _logger;
    private IntPtr _windowHandle = IntPtr.Zero;
    private HwndSource? _hwndSource;

    private const int HOTKEY_ID_ANC = 9001;
    private const int HOTKEY_ID_TRANSPARENCY = 9002;
    private const int HOTKEY_ID_PLAYPAUSE = 9003;

    private const int WM_HOTKEY = 0x0312;

    // Modifiers
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;
    private const uint MOD_NOREPEAT = 0x4000;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public event Action? ToggleAncRequested;
    public event Action? ToggleTransparencyRequested;
    public event Action? PlayPauseRequested;

    public GlobalHotkeyService(IAppLogger logger)
    {
        _logger = logger;
    }

    public void Initialize(IntPtr windowHandle)
    {
        _windowHandle = windowHandle;
        _hwndSource = HwndSource.FromHwnd(windowHandle);
        _hwndSource?.AddHook(HwndHook);
        _logger.App(LogLevel.Info, "Global hotkey window hook installed.");
    }

    public void RegisterHotkeys(string toggleAnc, string toggleTransparency, string playPause)
    {
        if (_windowHandle == IntPtr.Zero) return;

        UnregisterHotkeys();

        RegisterSingleHotkey(HOTKEY_ID_ANC, toggleAnc, "Toggle ANC");
        RegisterSingleHotkey(HOTKEY_ID_TRANSPARENCY, toggleTransparency, "Toggle Transparency");
        RegisterSingleHotkey(HOTKEY_ID_PLAYPAUSE, playPause, "Play/Pause");
    }

    private void RegisterSingleHotkey(int id, string hotkeyString, string label)
    {
        if (string.IsNullOrWhiteSpace(hotkeyString)) return;

        if (TryParseHotkey(hotkeyString, out uint modifiers, out uint vk))
        {
            bool success = RegisterHotKey(_windowHandle, id, modifiers | MOD_NOREPEAT, vk);
            if (success)
            {
                _logger.App(LogLevel.Info, $"Registered global hotkey {hotkeyString} for {label}");
            }
            else
            {
                int error = Marshal.GetLastWin32Error();
                _logger.App(LogLevel.Warn, $"Failed to register global hotkey {hotkeyString} for {label} (Error: {error})");
            }
        }
    }

    public void UnregisterHotkeys()
    {
        if (_windowHandle != IntPtr.Zero)
        {
            UnregisterHotKey(_windowHandle, HOTKEY_ID_ANC);
            UnregisterHotKey(_windowHandle, HOTKEY_ID_TRANSPARENCY);
            UnregisterHotKey(_windowHandle, HOTKEY_ID_PLAYPAUSE);
        }
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            switch (id)
            {
                case HOTKEY_ID_ANC:
                    ToggleAncRequested?.Invoke();
                    handled = true;
                    break;
                case HOTKEY_ID_TRANSPARENCY:
                    ToggleTransparencyRequested?.Invoke();
                    handled = true;
                    break;
                case HOTKEY_ID_PLAYPAUSE:
                    PlayPauseRequested?.Invoke();
                    handled = true;
                    break;
            }
        }
        return IntPtr.Zero;
    }

    private static bool TryParseHotkey(string hotkey, out uint modifiers, out uint vk)
    {
        modifiers = 0;
        vk = 0;
        var parts = hotkey.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return false;

        for (int i = 0; i < parts.Length - 1; i++)
        {
            var mod = parts[i].ToLowerInvariant();
            if (mod == "ctrl" || mod == "control") modifiers |= MOD_CONTROL;
            else if (mod == "alt") modifiers |= MOD_ALT;
            else if (mod == "shift") modifiers |= MOD_SHIFT;
            else if (mod == "win" || mod == "windows") modifiers |= MOD_WIN;
        }

        var keyString = parts[^1];
        if (Enum.TryParse<Key>(keyString, true, out var key))
        {
            vk = (uint)KeyInterop.VirtualKeyFromKey(key);
            return vk != 0;
        }

        return false;
    }

    public void Dispose()
    {
        UnregisterHotkeys();
        _hwndSource?.RemoveHook(HwndHook);
    }
}
