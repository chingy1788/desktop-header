# Windows Virtual Desktop Header Overlay

A premium, modern Windows desktop overlay application that displays the names of your virtual desktops docked elegantly at the top of your screen, highlights the active desktop, allows switching virtual desktops with a click, and can be dragged anywhere on the screen.

Designed to enhance productivity on multi-desktop Windows setups by keeping your workspace names visible at a glance and easy to navigate.

---

## Features

- **Alt+Tab Switcher Exclusion**: Excludes itself from the Windows `Alt+Tab` switcher using both WPF properties (`ShowInTaskbar="False"`) and the native Win32 `WS_EX_TOOLWINDOW` extended style, ensuring the overlay operates as a completely seamless and unobtrusive background workspace utility.
- **System Tray Integration**: Features a zero-dependency, lightweight system tray icon that runs a context menu allowing the user to easily toggle **Launch at Startup** (written directly to the `HKCU\..\Run` registry), **Restore Desktops** layout, or safely shut down the application via **Exit Overlay**.
- **Layout Backup & Restore**: Automatically saves custom virtual desktop configurations to a local file (`desktops_backup.txt`) in real-time as you add/rename them. It includes reboot protection to safeguard your backup when Windows resets your desktops on startup, and provides a smart **Restore Desktops** tray option that automatically renames default desktops and recreates the rest to restore your entire layout seamlessly.
- **Windows AppBar Integration**: Registers natively as a Windows AppBar to reserve the exact screen height for the overlay, ensuring maximizing applications automatically dock below the overlay rather than behind it, leaving a clean workspace offset.
- **WndProc Callback Hooking**: Hooks directly into native window messages to handle the `ABN_POSCHANGED` Windows Shell callback, ensuring the overlay safely recalculates and re-triggers layouts upon resolution changes, DPI scaling shifts, or virtual desktop switches, preventing silent shell termination.
- **Virtual Desktop Naming**: Dynamically queries the actual custom names of your virtual desktops (e.g. `'tooling'`) using Windows COM APIs combined with a highly robust Registry reader fallback.
- **Topmost & Sticky (Pinning)**: Automatically registers and pins itself to the Windows Shell so that it persists and displays seamlessly across **all** virtual desktops.
- **Click-to-Switch Navigation**: Instantly switches active desktops when you click on a desktop name, complete with optimistic state-switching for a fluid, lag-free click feel.
- **Interactive Dragging**: Easily reposition the header overlay anywhere on your screen by clicking and dragging the handle or the main container.
- **Dynamic Polling & Auto-Update**: Runs a lightweight background poller (every 250ms) to detect desktop additions, removals, renamings, and external switches (like `Win+Ctrl+Left/Right`), keeping the overlay dynamically in sync without visual flickering.
- **Robust File Logging**: Writes real-time operations, desktop discoveries, active switches, and system exceptions to a thread-safe `debug.log` file in the application directory for seamless troubleshooting.

---

## Technical Architecture

Built with C# .NET 9.0 and Windows Presentation Foundation (WPF) for hardware-accelerated rendering, visual transparency, and native performance.

### Project Structure
- **DesktopHeader.App**: Main WPF GUI application.
  - `Models/DesktopItem.cs`: Core data model representing individual desktop properties and binding updates via `INotifyPropertyChanged`.
  - `Interop/VirtualDesktopWrapper.cs`: Undocumented COM API definitions specifically tailored to Windows 11 (24H2/25H2/Build 26200+), featuring a customized `.NET Core` marshaller for the `IApplicationView` shell interface.
  - `AppBarHelper.cs`: Helper class handling safe Shell AppBar registrations, workspace reservations, native WndProc hooks (`ABN_POSCHANGED`), and horizonal centering calculations.
  - `MainWindow.xaml`: XAML UI definition specifying the glassmorphic style tokens, capsule gradient hover effects, layout, and active state triggers.
  - `MainWindow.xaml.cs`: Visual code-behind handling load centering, manual dragging, optimistic switching, and self-healing background retry pinning.
  - `Logger.cs`: Thread-safe, file-based logger writing to `debug.log`.
- **DesktopHeader.Tests**: xUnit automated testing project using FlaUI.Core and FlaUI.UIA3 to perform automated UI inspections and unit validations.

---

## Installation & Build Instructions

### Prerequisites
- **Operating System**: Windows 11 (build 26100+ recommended)
- **SDK**: .NET SDK 9.0+

### Build from Source
Open PowerShell in the project directory and run:
```powershell
dotnet build -c Release
```
This compiles the application and outputs the binaries to `DesktopHeader.App\bin\Release\net9.0-windows\DesktopHeader.App.exe`.

### Running the Application
Double-click `DesktopHeader.App.exe` or launch it via PowerShell:
```powershell
Start-Process -FilePath ".\DesktopHeader.App\bin\Release\net9.0-windows\DesktopHeader.App.exe"
```

---

## Diagnostics & Logging

All events are logged directly to a `debug.log` file created in the application's startup directory. The log tracks key cycles:
- **WPF Initialization**: Overlay starting and parameter setups.
- **Active Desktops Discovery**: Registry and COM queries listing names and active indexes.
- **AppBar Sizing and Native Hooks**: Captures real-time `ABN_POSCHANGED` messages and bounds calculations.
- **Dynamic Retry Pinning**: Captures the self-healing cycles of pinning the window across Windows Virtual Desktops.
- **Navigation Triggers**: Capture click events and target indexes.

Example log output:
```text
[2026-05-24 11:47:56.307] [INFO] MainWindow Loaded. Setting up window parameters...
[2026-05-24 11:47:56.433] [INFO] Successfully pinned MainWindow (hWnd: 1771704) to all virtual desktops on attempt 1.
[2026-05-24 11:47:56.707] [INFO] AppBar resized successfully. WorkArea Reserved Height: 96px. Bounds: L=0, T=0, R=1645, B=96
[2026-05-24 11:47:56.708] [INFO] Successfully registered Window as Windows AppBar.
[2026-05-24 11:47:57.062] [INFO] AppBar received ABN_POSCHANGED from Windows Shell. Re-triggering layout...
[2026-05-24 11:47:57.066] [INFO] AppBar resized successfully. WorkArea Reserved Height: 96px. Bounds: L=0, T=0, R=1645, B=96
```

---

## Testing

### Running Tests
To run all unit tests:
```powershell
dotnet test --configuration Release
```

*Note: UI Automation tests (using FlaUI) require an active, interactive GUI session. In headless or CI environments (like GitHub Actions), the UI scenarios will automatically skip themselves programmatically using `SKIP_UI_TESTS=true` or GITHUB_ACTIONS checks to prevent builds from hanging, ensuring the CI pipeline always stays perfectly green.*

To explicitly run only unit tests (safe for all headless and non-interactive runs):
```powershell
dotnet test --filter "FullyQualifiedName!~UI_Scenario"
```
