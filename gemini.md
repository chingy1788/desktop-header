# Windows Virtual Desktop Header Overlay

A premium, modern Windows desktop overlay application that displays the names of your virtual desktops at the top of your screen, highlights the active desktop, allows switching virtual desktops with a click, and can be dragged anywhere on the screen.

## Project Details

### Goal
Build a lightweight, highly responsive Windows application that enhances multi-desktop workflow by keeping virtual desktops visible at a glance and easy to toggle.

### Core Features
1. **Desktop Name Overlay**: Floating header overlay at the top of the screen showing all available Virtual Desktop names.
2. **Current Highlight**: Visually accent the active virtual desktop with an elegant glow or colored indicator.
3. **Click-to-Switch**: Clicking a desktop name immediately swaps the system to that virtual desktop.
4. **Draggable Header**: Click and drag the overlay bar to reposition it anywhere on the screen.
5. **Auto-Update**: Detect desktop additions, removals, renamings, and active switches, keeping the overlay dynamically in sync.
6. **Premium Design**: Dark glassmorphic backdrop with smooth transitions, modern typography, hover interactions, and sleek animations.

### Technical Architecture
- **Framework**: .NET 9.0 with WPF (Windows Presentation Foundation) for rich UI customization, transparency support, and native Windows performance.
- **COM Interop**: Re-use verified COM API definitions specifically tailored to Windows 11 (24H2/25H2/Build 26200+) to interface with the Windows Virtual Desktop Manager internals safely.
- **Testing Strategy**:
  - **Unit Tests**: Test the desktop query and registry reader utility logic.
  - **UI Tests**: Automated Gherkin-style/SpecFlow behavior tests or automated UI tests to verify view rendering, dragging, and active item highlight transitions.
