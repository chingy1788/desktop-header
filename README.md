# Windows Virtual Desktop Header Overlay

A premium, modern Windows desktop overlay application that displays the names of your virtual desktops docked elegantly at the top of your screen, highlights the active desktop, allows switching virtual desktops with a click, and can be dragged anywhere on the screen.

Designed to enhance productivity on multi-desktop Windows setups by keeping your workspace names visible at a glance and easy to navigate.

---

## Features

- **Docked Header Bar**: Sits flush at the top edge of the primary screen, styled with a modern glassmorphic look (dark charcoal acrylic backdrop with bottom rounded corners, fine semi-transparent borders, and a soft drop shadow).
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
Start-Process -FilePath ".\DesktopHeader.App\bin\Debug\net9.0-windows\DesktopHeader.App.exe"
```

---

## Diagnostics & Logging

All events are logged directly to a `debug.log` file created in the application's startup directory. The log tracks key cycles:
- **WPF Initialization**: Overlay starting and parameter setups.
- **Active Desktops Discovery**: Registry and COM queries listing names and active indexes.
- **Dynamic Retry Pinning**: Captures the self-healing cycles of pinning the window across Windows Virtual Desktops.
- **Navigation Triggers**: Capture click events and target indexes.

Example log output:
```text
[2026-05-24 09:39:30.479] [INFO] Desktop Header Overlay initializing...
[2026-05-24 09:39:30.545] [INFO] MainWindow Loaded. Setting up window parameters...
[2026-05-24 09:39:30.559] [INFO] Desktop count changed from 0 to 2. Rebuilding list...
[2026-05-24 09:39:30.561] [INFO] Discovered Desktop [0]: 'Desktop 1' (Active: True)
[2026-05-24 09:39:30.562] [INFO] Discovered Desktop [1]: 'tooling' (Active: False)
[2026-05-24 09:39:30.564] [WARN] Initial pin attempt failed (shell may not have registered window yet)...
[2026-05-24 09:39:30.564] [INFO] Polling timer started. Initially loaded 2 desktops.
[2026-05-24 09:39:30.822] [INFO] Successfully pinned MainWindow (hWnd: 1640656) to all virtual desktops on attempt 2.
```

---

## Testing

### Automated Unit Tests
To run unit tests validating property change triggers, name query formats, and Registry bindings:
```powershell
dotnet test --filter "FullyQualifiedName!~UI_Scenario"
```
