# Sennheiser Companion

A native, high-performance Windows desktop companion application for the **Sennheiser MOMENTUM 4 Wireless** headphones, inspired by the minimalist and premium aesthetic of the official Sennheiser Smart Control mobile app, engineered specifically for Windows 10 and 11.

---

## 🎧 Overview

This application is **built specifically around the Sennheiser MOMENTUM 4 Wireless** and its capabilities. It delivers a fast, spacious, and uncluttered desktop experience—bringing essential headphone telemetry and controls directly to Windows without needing to reach for your smartphone.

### Key Philosophy
- **"Spotify-level simplicity + Microsoft Fluent Design + Sennheiser headphone control"**
- Mouse and keyboard first, fully keyboard-navigable, high-DPI Per-Monitor V2 aware.
- **Strict No-Fake-Functionality Rule**: Every UI interaction either communicates directly with headphone hardware or explicitly informs the user if a capability is unsupported on the active Windows interface.
- **Demo Mode**: Built-in developer simulation mode allowing full interactive testing of all MOMENTUM 4 states (Connected, 78% battery, ANC modes, 5-band EQ, firmware) without physical headphones attached.

---

## 🌟 Features

### 1. Main Dashboard
- **Instant Telemetry**: Connection status, exact battery percentage, active Noise Control mode, active EQ preset, and active audio codec (aptX Adaptive / AAC / SBC).
- **Quick Noise Control**: Large, tactile segmented switches for **Adaptive ANC**, **Transparency**, and **Off**.
- **Windows System Media Controls (GSMTC)**: Live display of currently playing desktop audio track, artist, album, with Play/Pause, Next, and Previous controls.
- **Master Audio Volume**: Seamless endpoint volume slider with one-click mute and quick shortcut to Windows Sound Settings.
- **Quick Shortcuts**: Fast navigation to full Noise Control, Equalizer, Device Info, and Windows Bluetooth settings.

### 2. Dedicated Noise Control
- **Modes**: Adaptive ANC, Transparency, Off.
- **Dual Intensity Sliders**: Continuous percentage adjustments for Noise Cancellation Intensity and Transparency Level.
- **Wind Noise Reduction**: Dedicated hardware switch for outdoor microphone wind suppression.
- **Interface Transparency**: Displays hardware capability indicator confirming whether BLE vendor GATT characteristics are active on the pairing.

### 3. 5-Band Hardware Equalizer
- **Supported MOMENTUM 4 Bands**:
  - **Bass**: 63 Hz (-6.0 dB to +6.0 dB)
  - **Low Mid**: 250 Hz (-6.0 dB to +6.0 dB)
  - **Mid**: 1 kHz (-6.0 dB to +6.0 dB)
  - **High Mid**: 4 kHz (-6.0 dB to +6.0 dB)
  - **Treble**: 8 kHz (-6.0 dB to +6.0 dB)
- **Presets**: Neutral, Rock, Pop, Movie, Podcast, Custom.
- **Preset Management**: Create, save, rename, delete custom presets, and reset to neutral.

### 4. Device Details & Firmware
- Displays verified hardware parameters: Device Name, Model Number (`M4AEBT`), Firmware Revision, Hardware Revision, Serial Number, Bluetooth MAC address, and Audio Codec.
- Actions: Reconnect, Disconnect, Open Windows Bluetooth Settings, Check for Firmware Updates.

### 5. Windows-Specific Desktop Features
- **Native System Tray**:
  - Resides in the Windows taskbar notification area.
  - Right-click context menu: Live status, Noise Control mode submenu, Playback controls, Open App, Settings, and Exit.
  - Double-click tray icon to instantly summon the window.
- **Configurable Global Hotkeys**:
  - Trigger headphone actions from any game, browser, or fullscreen app using Win32 `RegisterHotKey`:
    - `Ctrl + Alt + A`: Toggle ANC
    - `Ctrl + Alt + T`: Toggle Transparency
    - `Ctrl + Alt + P`: Play / Pause playback
  - Fully remappable in Settings.
- **Window Lifecycle**:
  - Minimize to System Tray.
  - Close to System Tray (keeps app active in background).
  - Start with Windows (via registry `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`).
  - Start Minimized.
- **Subtle Notifications**: Alerts for connection, disconnection, and low battery (< 20%).

### 6. Light & Dark Themes
- **Dark Theme (Default)**: Deep charcoal/near-black `#121212`, matte card backgrounds `#1E1E1E`, light grey text `#F4F4F4`, subtle borders, and minimal signature Sennheiser red accents (`#E2231A`).
- **Light Theme**: Crisp off-white `#F8F9FA`, white cards `#FFFFFF`, charcoal text `#1A1A1A`, subtle grey borders, and refined red accents (`#D82218`).
- **System Default**: Automatically matches your Windows personalization preference (`AppsUseLightTheme`).
- Quick toggle button on the header bar (`☀/◐ Theme`).

---

## 🏛️ Architecture & Project Structure

The project strictly follows the **MVVM (Model-View-ViewModel)** architectural pattern with clean separation between the presentation layer, device communication layer, and Windows system services.

```
f:\Projects\application\
├── SennheiserMomentum4.sln
├── README.md
├── src\
│   └── SennheiserMomentum4\
│       ├── SennheiserMomentum4.csproj      # .NET 8 WPF targeting net8.0-windows10.0.19041.0
│       ├── app.manifest                    # High-DPI PerMonitorV2 & Windows 10/11 compatibility
│       ├── App.xaml / App.xaml.cs          # Dependency Injection root & lifecycle handler
│       ├── Models\
│       │   ├── DeviceInfo.cs               # Headphone telemetry and hardware specs
│       │   ├── NoiseControlState.cs        # ANC modes and intensity sliders
│       │   ├── EqualizerBand.cs            # Frequency band definition (-6 to +6 dB)
│       │   ├── EqualizerPreset.cs          # Preset definitions & factory defaults
│       │   ├── AppSettings.cs              # Application configuration & hotkeys
│       │   └── DeviceCommandResult.cs      # Strict non-fake command results
│       ├── Services\
│       │   ├── Device\
│       │   │   ├── IMomentum4DeviceService.cs  # Unified device service contract
│       │   │   ├── MockMomentum4DeviceService.cs # Demo Mode simulated MOMENTUM 4
│       │   │   ├── Momentum4BleDeviceService.cs  # Windows Bluetooth LE GATT engine
│       │   │   └── Momentum4DeviceManager.cs     # Switcher between Live BLE and Demo
│       │   ├── Windows\
│       │   │   ├── ISystemTrayService.cs / SystemTrayService.cs # Win32 tray & menu
│       │   │   ├── IGlobalHotkeyService.cs / GlobalHotkeyService.cs # Win32 RegisterHotKey
│       │   │   ├── IMediaTransportService.cs / MediaTransportService.cs # GSMTC
│       │   │   ├── IWindowsAudioService.cs / WindowsAudioService.cs # CoreAudio master vol
│       │   │   ├── INotificationService.cs / NotificationService.cs # Alerts
│       │   │   └── IStartupService.cs / StartupService.cs # Windows Run registry
│       │   ├── Settings\
│       │   │   ├── ISettingsService.cs
│       │   │   └── JsonSettingsService.cs  # %LOCALAPPDATA%\SennheiserMomentum4\settings.json
│       │   ├── Theme\
│       │   │   ├── IThemeManager.cs
│       │   │   └── ThemeManager.cs         # Dynamic runtime Light/Dark palette injection
│       │   └── Logging\
│       │       ├── IAppLogger.cs
│       │       └── AppLogger.cs            # Structured logs to logs/app, device, bluetooth
│       ├── ViewModels\
│       │   ├── ViewModelBase.cs
│       │   ├── MainViewModel.cs            # Navigation and top bar
│       │   ├── DashboardViewModel.cs       # Overview telemetry & quick controls
│       │   ├── SoundViewModel.cs           # 5-band EQ & preset editor
│       │   ├── NoiseControlViewModel.cs    # ANC & Transparency modes
│       │   ├── DeviceViewModel.cs          # Hardware specs & connection actions
│       │   └── SettingsViewModel.cs        # Preferences, logs viewer, demo mode switch
│       ├── Views\
│       │   ├── MainWindow.xaml / .cs
│       │   ├── DashboardPage.xaml / .cs
│       │   ├── SoundPage.xaml / .cs
│       │   ├── NoiseControlPage.xaml / .cs
│       │   ├── DevicePage.xaml / .cs
│       │   └── SettingsPage.xaml / .cs
│       ├── Converters\
│       │   └── ValueConverters.cs          # BoolToVis and EqualityToBoolean
│       └── Resources\
│           ├── HeadphoneArtwork.xaml       # Clean vector illustration of MOMENTUM 4
│           └── Styles\
│               └── SennheiserTheme.xaml    # Curated dark and light design system
└── tests\
    └── SennheiserMomentum4.Tests\
        ├── SennheiserMomentum4.Tests.csproj
        ├── DeviceServiceTests.cs           # Mock & BLE service unit tests
        ├── EqualizerPresetTests.cs         # 5-band EQ and preset integrity tests
        └── SettingsTests.cs                # App settings and hotkey defaults
```

---

## 📡 Bluetooth Communication Architecture

### Standard Bluetooth vs. BLE GATT
1. **Classic Audio (A2DP / AVRCP)**:
   - Handled natively by Windows audio drivers.
   - Provides high-resolution audio streaming and basic transport controls (Play, Pause, Volume).
2. **Bluetooth Low Energy GATT (Sonova / Sennheiser Profile)**:
   - Standard BLE Battery Service (`0x180F` → `0x2A19`) and Device Information Service (`0x180A` → `0x2A24`, `0x2A26`, `0x2A29`) are queried by `Momentum4BleDeviceService`.
   - The MOMENTUM 4 proprietary DSP controls (ANC attenuation curves, custom hardware EQ coefficients) communicate over Sonova vendor-specific GATT characteristics.
3. **No Fake Functionality Policy**:
   - If the active Windows Bluetooth pairing does not expose the proprietary BLE GATT write characteristic, the application **does not simulate success**.
   - Instead, the UI displays:
     > *"Not available through the current Windows Bluetooth interface. Remote ANC control requires Sennheiser BLE GATT access."*

---

## 🧪 Demo Mode (Simulation)

To test and experience the full application without a pair of physical MOMENTUM 4 headphones connected:
1. Open the app and navigate to **Settings** (gear icon or sidebar).
2. Scroll to the **Developer & Diagnostics** card.
3. Toggle **Demo Mode (Simulation)** to **ON**.
4. The application immediately mounts `MockMomentum4DeviceService`:
   - Simulates MOMENTUM 4 connected at **78% battery**, **aptX Adaptive**, firmware **2.13.28**.
   - Enables real-time interactive adjustments of Adaptive ANC, Transparency sliders, 5-band EQ, and custom presets.
   - Displays a prominent red **`DEMO MODE (Simulated)`** badge in the header so simulated values are never confused with hardware values.

---

## 🛠️ Build & Run Instructions

### Prerequisites
- Windows 10 (Version 1809+) or Windows 11
- .NET 8.0 SDK or higher (`dotnet --info`)

### Building from Command Line
```powershell
# Navigate to application directory
cd f:\Projects\application

# Restore and build the solution
dotnet build -c Release

# Run the unit tests
dotnet test
```

### Running the Application
```powershell
dotnet run --project src\SennheiserMomentum4\SennheiserMomentum4.csproj
```

Or execute the compiled binary:
```powershell
.\src\SennheiserMomentum4\bin\Release\net8.0-windows10.0.19041.0\SennheiserMomentum4.exe
```

---

## 🪵 Diagnostics & Logging

Structured logs are automatically recorded with millisecond timestamps, severity levels, and category routing in `%LOCALAPPDATA%\SennheiserMomentum4\logs\`:
- `app.log`: Application startup, theme changes, UI events, hotkey registrations.
- `device.log`: Hardware telemetry updates, battery percentages, EQ preset applications.
- `bluetooth.log`: BLE device discovery, GATT service resolution, characteristic read/write attempts.

You can view these logs directly in **Settings → Developer & Diagnostics** or click **Open Logs Folder** to inspect the files in Windows Explorer.

---

## 🔧 Troubleshooting

| Symptom | Cause | Solution |
|---|---|---|
| *"MOMENTUM 4 not found"* | Headphones are turned off or in pairing mode with another device. | Ensure MOMENTUM 4 is powered on and paired in Windows Settings (`Bluetooth & devices`). |
| *"Feature not available through current Windows Bluetooth interface"* | Windows classic audio driver is active, but proprietary Sonova GATT profile is restricted. | Standard audio playback still works. Use Demo Mode to preview custom DSP controls on Windows. |
| Global hotkey fails to register | Another application (e.g. Discord, OBS) has already claimed `Ctrl+Alt+A`. | Go to **Settings → Keyboard Shortcuts** and customize the key combinations. |
| Window does not appear on launch | *"Start Minimized"* is enabled in Settings. | Double-click the MOMENTUM 4 icon in the Windows taskbar system tray. |
