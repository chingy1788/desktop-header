using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using DesktopHeader.App.Models;
using VirtualDesktop;
using Button = System.Windows.Controls.Button;
using MessageBox = System.Windows.MessageBox;

namespace DesktopHeader.App
{
    public partial class MainWindow : Window, System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }
        public ObservableCollection<DesktopItem> Desktops { get; } = new();
        public System.Windows.Forms.Screen TargetScreen { get; set; } = System.Windows.Forms.Screen.PrimaryScreen ?? throw new InvalidOperationException("No primary screen detected.");
        public bool IsPrimary { get; set; } = true;
        private AppBarHelper? _appBarHelper;
        private readonly DispatcherTimer _timer;
        private int _lastActiveIndex = -1;
        private bool _isPinned = false;
        private IntPtr _windowHandle = IntPtr.Zero;
        private int _pinRetryCount = 0;
        private NotifyIcon? _notifyIcon;
        private DesktopItem? _desktopItemBeingRenamed;
        private const string RegistryRunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppRegistryName = "DesktopHeaderOverlay";
        private bool _showNotesPreview = true;
        public bool ShowNotesPreview
        {
            get => _showNotesPreview;
            set
            {
                if (_showNotesPreview != value)
                {
                    _showNotesPreview = value;
                    OnPropertyChanged(nameof(ShowNotesPreview));
                    SaveSettings();
                    UpdateNotesVisibility();
                }
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            Logger.LogInfo("Desktop Header Overlay initializing...");

            // Load saved settings
            LoadSettings();

            // Initialize DispatcherTimer for highly lightweight 250ms polling
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            _timer.Tick += Timer_Tick;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Logger.LogInfo($"MainWindow Loaded on screen {(TargetScreen != null ? TargetScreen.DeviceName : "Primary")}. IsPrimary={IsPrimary}");

            if (Environment.GetEnvironmentVariable("RUNNING_UI_TESTS") != "true")
            {
                this.ResizeMode = ResizeMode.NoResize;
            }

            // Pin to target screen's bounds initially; AppBar registration will set exact position
            if (TargetScreen != null)
            {
                var dpi = VisualTreeHelper.GetDpi(this);
                this.Left = TargetScreen.Bounds.X / dpi.DpiScaleX;
                this.Top = TargetScreen.Bounds.Y / dpi.DpiScaleY;
                this.Width = TargetScreen.Bounds.Width / dpi.DpiScaleX;
            }
            else
            {
                this.Left = 0;
                this.Top = 0;
            }

            if (IsPrimary)
            {
                // Initial load of desktops
                UpdateDesktopsList();
            }

            // Get window handle and attempt initial pinning
            var helper = new WindowInteropHelper(this);
            _windowHandle = helper.Handle;

            // Hide from Alt+Tab by setting WS_EX_TOOLWINDOW extended style
            try
            {
                int exStyle = GetWindowLong(_windowHandle, GWL_EXSTYLE);
                SetWindowLong(_windowHandle, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW);
                Logger.LogInfo("Successfully set WS_EX_TOOLWINDOW style to hide overlay from Alt+Tab switcher.");
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Failed to set WS_EX_TOOLWINDOW style: {ex.Message}");
            }

            TryPinWindow();

            if (IsPrimary)
            {
                // Initialize system tray icon
                InitializeTrayIcon();

                // Start the polling timer
                _timer.Start();
                Logger.LogInfo($"Polling timer started. Initially loaded {Desktops.Count} desktops.");

                // Spawn secondary windows for other screens
                try
                {
                    var screens = System.Windows.Forms.Screen.AllScreens;
                    Logger.LogInfo($"Multi-monitor detection: found {screens.Length} screens.");
                    foreach (var s in screens)
                    {
                        if (!s.Primary)
                        {
                            Logger.LogInfo($"Spawning secondary overlay on screen: {s.DeviceName}");
                            var secondaryWin = new MainWindow
                            {
                                IsPrimary = false,
                                TargetScreen = s
                            };
                            secondaryWin.Show();
                            secondaryWin.SyncFromPrimary(this);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to initialize multi-monitor overlays.", ex);
                }
            }

            // Run initial responsive layout check after window loaded
            Dispatcher.BeginInvoke(new Action(() => UpdateNotesVisibility()), DispatcherPriority.Background);
        }

        private void Window_ContentRendered(object? sender, EventArgs e)
        {
            var dpiScale = VisualTreeHelper.GetDpi(this);
            int barHeight = 46; // The AppBar layout reservation is fixed to 46 DIPs (Slim height)
            int heightPixels = (int)Math.Ceiling(barHeight * dpiScale.DpiScaleY);
            Logger.LogInfo($"ContentRendered: AppBar reservation height = {barHeight} DIPs -> {heightPixels}px physical (DPI Y scale: {dpiScale.DpiScaleY})");

            // Register as Windows AppBar — reserves exact header height so all windows dock below
            try
            {
                _appBarHelper = new AppBarHelper(TargetScreen);
                _appBarHelper.RegisterAppBar(this, heightPixels);
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to register WPF window as Windows AppBar.", ex);
            }
        }

        private void InitializeTrayIcon()
        {
            try
            {
                _notifyIcon = new NotifyIcon();
                _notifyIcon.Text = "Virtual Desktop Header Overlay";

                // Load app icon dynamically from the current process
                System.Drawing.Icon? appIcon = null;
                try
                {
                    string? processPath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                    if (processPath != null)
                    {
                        appIcon = System.Drawing.Icon.ExtractAssociatedIcon(processPath);
                    }
                }
                catch { }

                _notifyIcon.Icon = appIcon ?? SystemIcons.Application;
                _notifyIcon.Visible = true;

                // Create context menu
                var contextMenu = new ContextMenuStrip();

                // "Launch at Startup" item
                var startupItem = new ToolStripMenuItem("Launch at Startup");
                startupItem.CheckOnClick = true;
                startupItem.Checked = IsLaunchAtStartupEnabled();
                startupItem.Click += (s, e) => {
                    SetLaunchAtStartup(startupItem.Checked);
                };
                contextMenu.Items.Add(startupItem);

                // "Show Notes Preview" item
                var previewItem = new ToolStripMenuItem("Show Notes Preview");
                previewItem.CheckOnClick = true;
                previewItem.Checked = ShowNotesPreview;
                previewItem.Click += (s, e) => {
                    ShowNotesPreview = previewItem.Checked;
                };
                contextMenu.Items.Add(previewItem);

                // "Restore Desktops" item
                var restoreItem = new ToolStripMenuItem("Restore Desktops");
                restoreItem.Click += (s, e) => {
                    RestoreDesktops();
                };

                // "Open Dev Environment" item with sub-menu options to open each tool
                var devEnvItem = new ToolStripMenuItem("Open Dev Environment");
                
                var openAllItem = new ToolStripMenuItem("Open All Tools");
                openAllItem.Click += (s, e) => {
                    try
                    {
                        Desktop currentActive = Desktop.Current;
                        int currentActiveIndex = Desktop.FromDesktop(currentActive);
                        string activeName = Desktop.DesktopNameFromIndex(currentActiveIndex);
                        SpawnDevTool(activeName, "All");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Failed to spawn all development tools from tray click.", ex);
                        MessageBox.Show($"Failed to spawn dev environment: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                };

                var openTerminalItem = new ToolStripMenuItem("Open Windows Terminal");
                openTerminalItem.Click += (s, e) => {
                    try
                    {
                        Desktop currentActive = Desktop.Current;
                        int currentActiveIndex = Desktop.FromDesktop(currentActive);
                        string activeName = Desktop.DesktopNameFromIndex(currentActiveIndex);
                        SpawnDevTool(activeName, "Terminal");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Failed to spawn Windows Terminal from tray click.", ex);
                    }
                };

                var openVSCodeItem = new ToolStripMenuItem("Open VS Code");
                openVSCodeItem.Click += (s, e) => {
                    try
                    {
                        Desktop currentActive = Desktop.Current;
                        int currentActiveIndex = Desktop.FromDesktop(currentActive);
                        string activeName = Desktop.DesktopNameFromIndex(currentActiveIndex);
                        SpawnDevTool(activeName, "VSCode");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Failed to spawn VS Code from tray click.", ex);
                    }
                };

                var openGitExItem = new ToolStripMenuItem("Open GitExtensions");
                openGitExItem.Click += (s, e) => {
                    try
                    {
                        Desktop currentActive = Desktop.Current;
                        int currentActiveIndex = Desktop.FromDesktop(currentActive);
                        string activeName = Desktop.DesktopNameFromIndex(currentActiveIndex);
                        SpawnDevTool(activeName, "GitExtensions");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Failed to spawn GitExtensions from tray click.", ex);
                    }
                };

                var openExplorerItem = new ToolStripMenuItem("Open File Explorer");
                openExplorerItem.Click += (s, e) => {
                    try
                    {
                        Desktop currentActive = Desktop.Current;
                        int currentActiveIndex = Desktop.FromDesktop(currentActive);
                        string activeName = Desktop.DesktopNameFromIndex(currentActiveIndex);
                        SpawnDevTool(activeName, "Explorer");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Failed to spawn File Explorer from tray click.", ex);
                    }
                };

                devEnvItem.DropDownItems.Add(openAllItem);
                devEnvItem.DropDownItems.Add(new ToolStripSeparator());
                devEnvItem.DropDownItems.Add(openTerminalItem);
                devEnvItem.DropDownItems.Add(openVSCodeItem);
                devEnvItem.DropDownItems.Add(openGitExItem);
                devEnvItem.DropDownItems.Add(openExplorerItem);
                
                // Dynamically populate sub-menu on open to show what desktops will be restored
                contextMenu.Opening += (s, e) => {
                    try
                    {
                        // Update Dev Environment label dynamically based on active desktop
                        try
                        {
                            Desktop currentActive = Desktop.Current;
                            int currentActiveIndex = Desktop.FromDesktop(currentActive);
                            string activeName = Desktop.DesktopNameFromIndex(currentActiveIndex);
                            devEnvItem.Text = $"Open Dev Environment (c:\\dev\\{SanitizeFolderName(activeName)})";
                        }
                        catch (Exception ex)
                        {
                            Logger.LogWarning($"Failed to dynamically update dev environment tray menu item: {ex.Message}");
                            devEnvItem.Text = "Open Dev Environment";
                        }

                        restoreItem.DropDownItems.Clear();
                        
                        string backupPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "desktops_backup.txt");
                        if (File.Exists(backupPath))
                        {
                            var backupNames = File.ReadAllLines(backupPath)
                                                  .Select(line => line.Trim())
                                                  .Where(line => !string.IsNullOrEmpty(line))
                                                  .ToList();

                            if (backupNames.Count > 0)
                            {
                                var titleItem = new ToolStripMenuItem("Will restore:");
                                titleItem.Enabled = false;
                                titleItem.Font = new Font(titleItem.Font, System.Drawing.FontStyle.Bold);
                                restoreItem.DropDownItems.Add(titleItem);
                                
                                restoreItem.DropDownItems.Add(new ToolStripSeparator());

                                foreach (var name in backupNames)
                                {
                                    var nameItem = new ToolStripMenuItem(name);
                                    nameItem.Enabled = false; // Purely informational
                                    restoreItem.DropDownItems.Add(nameItem);
                                }
                            }
                            else
                            {
                                var emptyItem = new ToolStripMenuItem("(Saved layout is empty)");
                                emptyItem.Enabled = false;
                                restoreItem.DropDownItems.Add(emptyItem);
                            }
                        }
                        else
                        {
                            var noBackupItem = new ToolStripMenuItem("(No saved layout found)");
                            noBackupItem.Enabled = false;
                            restoreItem.DropDownItems.Add(noBackupItem);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Failed to populate Restore Desktops sub-menu list.", ex);
                    }
                };

                contextMenu.Items.Add(restoreItem);
                contextMenu.Items.Add(devEnvItem);

                contextMenu.Items.Add(new ToolStripSeparator());

                // "Restart Overlay" item
                var restartItem = new ToolStripMenuItem("Restart Overlay");
                restartItem.Click += (s, e) => {
                    Logger.LogInfo("Restart selected from System Tray. Restarting application...");
                    try
                    {
                        string? processPath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                        if (processPath != null)
                        {
                            System.Diagnostics.Process.Start(processPath);
                            System.Windows.Application.Current.Shutdown();
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Failed to restart application.", ex);
                        MessageBox.Show($"Failed to restart overlay:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                };
                contextMenu.Items.Add(restartItem);

                // "Exit Overlay" item
                var exitItem = new ToolStripMenuItem("Exit Overlay");
                exitItem.Click += (s, e) => {
                    Logger.LogInfo("Exit selected from System Tray. Shutting down...");
                    System.Windows.Application.Current.Shutdown();
                };
                contextMenu.Items.Add(exitItem);

                _notifyIcon.ContextMenuStrip = contextMenu;

                // Cleanup on window closing
                this.Closing += (s, e) => {
                    if (_notifyIcon != null)
                    {
                        _notifyIcon.Visible = false;
                        _notifyIcon.Dispose();
                    }
                };
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to initialize system tray icon.", ex);
            }
        }

        private void SetWindowHeight(int height)
        {
            try
            {
                Logger.LogInfo($"Re-sizing overlay layout to height: {height}px...");
                this.Height = height;
                
                // Re-register AppBar to recalculate reservations
                var dpiScale = VisualTreeHelper.GetDpi(this);
                int heightPixels = (int)Math.Ceiling(height * dpiScale.DpiScaleY);
                
                // AppBarHelper.SizeAppBar will re-apply the bounds
                _appBarHelper?.SizeAppBar(this, heightPixels);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to change window height to {height}.", ex);
            }
        }

        private bool IsLaunchAtStartupEnabled()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RegistryRunKey))
                {
                    if (key != null)
                    {
                        object? val = key.GetValue(AppRegistryName);
                        return val != null;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to read startup registry key.", ex);
            }
            return false;
        }

        private void SetLaunchAtStartup(bool enable)
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RegistryRunKey, true))
                {
                    if (key != null)
                    {
                        if (enable)
                        {
                            string? execPath = Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                            if (execPath != null)
                            {
                                key.SetValue(AppRegistryName, $"\"{execPath}\"");
                                Logger.LogInfo($"Successfully enabled launch at startup pointing to: {execPath}");
                            }
                            else
                            {
                                Logger.LogError("Failed to determine executable path for startup registration.");
                            }
                        }
                        else
                        {
                            key.DeleteValue(AppRegistryName, false);
                            Logger.LogInfo("Successfully disabled launch at startup.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to modify startup registry key.", ex);
            }
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            // Enforce Topmost to prevent the AppBar from being covered by other windows
            try
            {
                if (!this.Topmost)
                {
                    this.Topmost = true;
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Failed to enforce Topmost z-order: {ex.Message}");
            }

            // Self-healing retry/enforcement for window pinning across all virtual desktops
            if (_windowHandle != IntPtr.Zero)
            {
                try
                {
                    bool isCurrentlyPinned = Desktop.IsWindowPinned(_windowHandle);
                    if (!isCurrentlyPinned)
                    {
                        Logger.LogInfo("Window is not pinned to all virtual desktops. Re-applying pin...");
                        Desktop.PinWindow(_windowHandle);
                        _isPinned = true;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Failed to verify or pin window to all desktops: {ex.Message}");
                }
            }

            UpdateDesktopsList();
        }

        private void TryPinWindow()
        {
            if (_windowHandle == IntPtr.Zero) return;
            
            _pinRetryCount++;
            try
            {
                Desktop.PinWindow(_windowHandle);
                _isPinned = true;
                Logger.LogInfo($"Successfully pinned MainWindow (hWnd: {_windowHandle}) to all virtual desktops on attempt {_pinRetryCount}.");
            }
            catch (Exception ex)
            {
                if (_pinRetryCount == 1)
                {
                    Logger.LogWarning($"Initial pin attempt failed (shell may not have registered window yet). Retrying on polling ticks. Error: {ex.Message}");
                }
                else if (_pinRetryCount >= 10)
                {
                    Logger.LogError($"Exceeded maximum retry attempts (10) to pin window. Overlay may only appear on Desktop 1. Error: {ex.Message}");
                }
            }
        }

        private void UpdateDesktopsList()
        {
            if (!IsPrimary)
            {
                // Find primary window and ask it to update and sync
                foreach (Window win in System.Windows.Application.Current.Windows)
                {
                    if (win is MainWindow mainWin && mainWin.IsPrimary)
                    {
                        mainWin.UpdateDesktopsList();
                        return;
                    }
                }
            }

            try
            {
                int count = Desktop.Count;
                if (count <= 0) return;

                Desktop currentActive = Desktop.Current;
                int currentActiveIndex = Desktop.FromDesktop(currentActive);

                // Handle notes load/save dynamically on active virtual desktop switches
                if (_lastActiveIndex != currentActiveIndex)
                {
                    if (_lastActiveIndex != -1)
                    {
                        Logger.LogInfo($"Active desktop switch detected from index {_lastActiveIndex} to {currentActiveIndex}. Auto-saving notes for previous desktop...");
                        SaveCurrentDesktopNote();
                    }

                    Logger.LogInfo($"Loading notes for active desktop: {currentActive.Id}");
                    LoadNoteForDesktop(currentActive.Id);
                }

                // If desktop count has changed, rebuild the list to keep in sync
                if (Desktops.Count != count)
                {
                    Logger.LogInfo($"Desktop count changed from {Desktops.Count} to {count}. Rebuilding list...");
                    Desktops.Clear();
                    for (int i = 0; i < count; i++)
                    {
                        string name = Desktop.DesktopNameFromIndex(i);
                        Desktops.Add(new DesktopItem
                        {
                            Index = i,
                            Name = name,
                            IsActive = (i == currentActiveIndex)
                        });
                        Logger.LogInfo($"Discovered Desktop [{i}]: '{name}' (Active: {i == currentActiveIndex})");
                    }

                    _lastActiveIndex = currentActiveIndex;
                    SaveDesktopsBackup();
                }
                else
                {
                    // Otherwise, efficiently update properties individually to avoid flickering
                    bool nameChanged = false;
                    for (int i = 0; i < count; i++)
                    {
                        string name = Desktop.DesktopNameFromIndex(i);
                        bool isCurrentActive = (i == currentActiveIndex);

                        if (Desktops[i].Name != name)
                        {
                            Logger.LogInfo($"Desktop [{i}] renamed from '{Desktops[i].Name}' to '{name}'.");
                            Desktops[i].Name = name;
                            nameChanged = true;
                        }

                        if (Desktops[i].IsActive != isCurrentActive)
                        {
                            Desktops[i].IsActive = isCurrentActive;
                        }
                    }

                    if (nameChanged)
                    {
                        SaveDesktopsBackup();
                    }

                    if (_lastActiveIndex != currentActiveIndex)
                    {
                        Logger.LogInfo($"Syncing active index to: {currentActiveIndex}");
                        _lastActiveIndex = currentActiveIndex;
                    }
                }
                UpdateNotesVisibility();
                if (IsPrimary)
                {
                    SyncSecondaryWindows();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error occurred while polling/updating desktops list.", ex);
            }
        }

        private void SaveDesktopsBackup()
        {
            try
            {
                int count = Desktops.Count;
                if (count <= 0) return;

                // Don't overwrite backup if we only have one desktop and it has a default name (e.g. after PC restart)
                if (count == 1 && IsDefaultDesktopName(Desktops[0].Name, 0))
                {
                    Logger.LogInfo("Skipping automatic backup save: only 1 desktop exists and it has a default name.");
                    return;
                }

                var names = Desktops.Select(d => d.Name).ToList();
                string backupPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "desktops_backup.txt");
                File.WriteAllLines(backupPath, names);
                Logger.LogInfo($"Successfully saved {names.Count} desktop names to backup: {backupPath}");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to save desktops backup.", ex);
            }
        }

        private bool IsDefaultDesktopName(string name, int index)
        {
            if (string.IsNullOrEmpty(name)) return true;
            return string.Equals(name, "Desktop", StringComparison.OrdinalIgnoreCase) || 
                   string.Equals(name, $"Desktop {index + 1}", StringComparison.OrdinalIgnoreCase);
        }

        private void RestoreDesktops()
        {
            try
            {
                string backupPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "desktops_backup.txt");
                if (!File.Exists(backupPath))
                {
                    MessageBox.Show("No saved virtual desktop layout found.\n\nCustom desktops are automatically backed up as you create or rename them.", "Restore Desktops", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var backupNames = File.ReadAllLines(backupPath)
                                      .Select(line => line.Trim())
                                      .Where(line => !string.IsNullOrEmpty(line))
                                      .ToList();

                if (backupNames.Count == 0)
                {
                    MessageBox.Show("The saved virtual desktop layout is empty.", "Restore Desktops", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                Logger.LogInfo($"Initiating desktop restoration. Backup contains {backupNames.Count} desktops.");

                int createdCount = 0;

                for (int i = 0; i < backupNames.Count; i++)
                {
                    string targetName = backupNames[i];

                    // Refresh current desktops in each iteration because we might modify them
                    int currentCount = Desktop.Count;
                    var currentNames = new List<string>();
                    for (int j = 0; j < currentCount; j++)
                    {
                        currentNames.Add(Desktop.DesktopNameFromIndex(j));
                    }

                    // Check if a desktop with this target name already exists (case-insensitive)
                    bool exists = currentNames.Any(c => string.Equals(c, targetName, StringComparison.OrdinalIgnoreCase));
                    if (exists)
                    {
                        Logger.LogInfo($"Desktop '{targetName}' already exists. Skipping.");
                        continue;
                    }

                    // Create a new desktop and set its name
                    Logger.LogInfo($"Creating new desktop for '{targetName}'...");
                    Desktop newDesktop = Desktop.Create();
                    newDesktop.SetName(targetName);
                    createdCount++;
                }

                // Force layout update immediately after restoring
                UpdateDesktopsList();

                if (createdCount > 0)
                {
                    MessageBox.Show($"Successfully restored virtual desktops!\n\n- Created {createdCount} new desktop(s).", "Restore Desktops", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("All backup desktops are already present on this PC.", "Restore Desktops", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to restore virtual desktops.", ex);
                MessageBox.Show($"An error occurred while restoring desktops:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DesktopButton_Click(object sender, RoutedEventArgs e)
        {
            if (DesktopsDropdownPopup != null && DesktopsDropdownPopup.IsOpen)
            {
                DesktopsDropdownPopup.IsOpen = false;
            }
            if (sender is Button button && button.CommandParameter is DesktopItem clickedItem)
            {
                try
                {
                    Logger.LogInfo($"Click detected on Desktop [{clickedItem.Index}] ('{clickedItem.Name}'). Initiating desktop switch...");
                    
                    // Switch to clicked desktop
                    Desktop targetDesktop = Desktop.FromIndex(clickedItem.Index);
                    
                    // Optimistically set active states instantly in the UI for seamless click feel
                    foreach (var d in Desktops)
                    {
                        d.IsActive = (d.Index == clickedItem.Index);
                    }

                    // Propagate optimistic switch to other windows
                    foreach (Window win in System.Windows.Application.Current.Windows)
                    {
                        if (win is MainWindow mainWin && mainWin != this)
                        {
                            foreach (var d in mainWin.Desktops)
                            {
                                d.IsActive = (d.Index == clickedItem.Index);
                            }
                        }
                    }
                    
                    targetDesktop.MakeVisible();
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to switch to virtual desktop index {clickedItem.Index}.", ex);
                    MessageBox.Show($"Failed to switch desktop: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        #region Sticky Notes System Features

        private string _currentNoteText = "";
        public string CurrentNoteText
        {
            get => _currentNoteText;
            set
            {
                if (_currentNoteText != value)
                {
                    _currentNoteText = value;
                    OnPropertyChanged(nameof(CurrentNoteText));
                    UpdateNotePreview(value);
                    SyncNotesToAll(value);
                }
            }
        }

        private string _currentNotePreview = "Click to add notes...";
        public string CurrentNotePreview
        {
            get => _currentNotePreview;
            set
            {
                if (_currentNotePreview != value)
                {
                    _currentNotePreview = value;
                    OnPropertyChanged(nameof(CurrentNotePreview));
                }
            }
        }

        private Guid _currentDesktopGuid = Guid.Empty;

        private void UpdateNotePreview(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                CurrentNotePreview = "Click to add notes...";
            }
            else
            {
                // Get the first line
                int lineBreakIndex = text.IndexOf('\n');
                string firstLine = lineBreakIndex >= 0 ? text.Substring(0, lineBreakIndex).Trim() : text.Trim();
                
                if (string.IsNullOrWhiteSpace(firstLine))
                {
                    CurrentNotePreview = "Click to add notes...";
                }
                else
                {
                    CurrentNotePreview = firstLine;
                }
            }
        }

        private void LoadSettings()
        {
            try
            {
                string settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.txt");
                if (File.Exists(settingsPath))
                {
                    string content = File.ReadAllText(settingsPath).Trim();
                    if (bool.TryParse(content, out bool val))
                    {
                        _showNotesPreview = val;
                        Logger.LogInfo($"Loaded ShowNotesPreview setting: {val}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to load settings from file.", ex);
            }
        }

        private void SaveSettings()
        {
            try
            {
                string settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.txt");
                File.WriteAllText(settingsPath, _showNotesPreview.ToString());
                Logger.LogInfo($"Saved ShowNotesPreview setting: {_showNotesPreview}");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to save settings to file.", ex);
            }
        }

        private string GetNotesDirectory()
        {
            string userDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string newDir = Path.Combine(userDir, ".desktop-header", "notes");

            try
            {
                string oldDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "notes");
                if (Directory.Exists(oldDir) && string.Compare(oldDir, newDir, StringComparison.OrdinalIgnoreCase) != 0)
                {
                    Logger.LogInfo($"Migration: Old notes folder found at '{oldDir}'. Migrating to '{newDir}'...");
                    if (!Directory.Exists(newDir))
                    {
                        Directory.CreateDirectory(newDir);
                    }

                    foreach (string oldFilePath in Directory.GetFiles(oldDir, "*.txt"))
                    {
                        string fileName = Path.GetFileName(oldFilePath);
                        string newFilePath = Path.Combine(newDir, fileName);
                        File.Copy(oldFilePath, newFilePath, true);
                        Logger.LogInfo($"Migration: Copied note file '{fileName}' successfully.");
                    }

                    Directory.Delete(oldDir, true);
                    Logger.LogInfo("Migration: Old notes folder deleted successfully.");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Migration: Error migrating old notes to user directory.", ex);
            }

            if (!Directory.Exists(newDir))
            {
                Directory.CreateDirectory(newDir);
            }
            return newDir;
        }

        private string GetNoteFilePath(Guid guid)
        {
            return Path.Combine(GetNotesDirectory(), $"note_{guid:N}.txt");
        }

        private void LoadNoteForDesktop(Guid guid)
        {
            if (guid == Guid.Empty) return;
            
            try
            {
                _currentDesktopGuid = guid;
                string filePath = GetNoteFilePath(guid);
                if (File.Exists(filePath))
                {
                    string content = File.ReadAllText(filePath);
                    _currentNoteText = content; // Set backing field directly to avoid triggering save during load
                    OnPropertyChanged(nameof(CurrentNoteText));
                    UpdateNotePreview(content);
                }
                else
                {
                    _currentNoteText = "";
                    OnPropertyChanged(nameof(CurrentNoteText));
                    CurrentNotePreview = "Click to add notes...";
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to load note for desktop GUID: {guid}", ex);
            }
        }

        private void SaveCurrentDesktopNote()
        {
            if (!IsPrimary)
            {
                foreach (Window win in System.Windows.Application.Current.Windows)
                {
                    if (win is MainWindow mainWin && mainWin.IsPrimary)
                    {
                        mainWin.SaveCurrentDesktopNote();
                        return;
                    }
                }
            }

            Guid guid = _currentDesktopGuid;
            if (guid == Guid.Empty) return;

            try
            {
                string filePath = GetNoteFilePath(guid);
                string text = CurrentNoteText;
                
                if (string.IsNullOrWhiteSpace(text))
                {
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                        Logger.LogInfo($"Deleted empty note file for desktop: {guid}");
                    }
                }
                else
                {
                    File.WriteAllText(filePath, text);
                    Logger.LogInfo($"Successfully saved note for desktop: {guid}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to save note for desktop GUID: {guid}", ex);
            }
        }


        private void NotesTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            // Auto-save on focus loss
            SaveCurrentDesktopNote();
        }

        private void NotesHeader_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (EditorRow.Height.Value == 0)
            {
                ExpandNotes();
            }
            else
            {
                CollapseNotes();
            }
        }

        private void DoneButton_Click(object sender, RoutedEventArgs e)
        {
            CollapseNotes();
        }

        private void DevEnvButton_Click(object sender, RoutedEventArgs e)
        {
            DevEnvPopup.IsOpen = !DevEnvPopup.IsOpen;
        }

        private void DevTool_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                if (button != null)
                {
                    string toolName = button.CommandParameter as string ?? "All";
                    Desktop currentActive = Desktop.Current;
                    int currentActiveIndex = Desktop.FromDesktop(currentActive);
                    string activeName = Desktop.DesktopNameFromIndex(currentActiveIndex);
                    
                    SpawnDevTool(activeName, toolName);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to spawn dev tool from dropdown click.", ex);
                MessageBox.Show($"Failed to spawn dev environment: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                DevEnvPopup.IsOpen = false;
            }
        }

        private void AddDesktopButton_Click(object sender, RoutedEventArgs e)
        {
            if (DesktopsDropdownPopup != null && DesktopsDropdownPopup.IsOpen)
            {
                DesktopsDropdownPopup.IsOpen = false;
            }
            try
            {
                Logger.LogInfo("Creating new virtual desktop...");
                Desktop.Create();
                UpdateDesktopsList();
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to create virtual desktop.", ex);
                MessageBox.Show($"Failed to create virtual desktop: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DesktopsDropdownButton_Click(object sender, RoutedEventArgs e)
        {
            if (DesktopsDropdownPopup != null)
            {
                DesktopsDropdownPopup.IsOpen = !DesktopsDropdownPopup.IsOpen;
            }
        }

        private void DesktopButton_RightClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var button = sender as Button;
            if (button != null)
            {
                var item = button.DataContext as DesktopItem;
                if (item != null)
                {
                    _desktopItemBeingRenamed = item;
                    RenameTextBox.Text = item.Name;
                    
                    RenamePopup.PlacementTarget = button;
                    RenamePopup.IsOpen = true;
                    
                    // Focus and select all text in the text box
                    RenameTextBox.Focus();
                    RenameTextBox.SelectAll();
                }
            }
        }

        private void RenameSave_Click(object sender, RoutedEventArgs e)
        {
            SaveRename();
        }

        private void RenameTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                SaveRename();
                e.Handled = true;
            }
            else if (e.Key == System.Windows.Input.Key.Escape)
            {
                RenamePopup.IsOpen = false;
                e.Handled = true;
            }
        }

        private void RenameCancel_Click(object sender, RoutedEventArgs e)
        {
            RenamePopup.IsOpen = false;
        }

        private void RenameDelete_Click(object sender, RoutedEventArgs e)
        {
            if (_desktopItemBeingRenamed != null)
            {
                // Close the popup first
                RenamePopup.IsOpen = false;

                int currentCount = Desktop.Count;
                if (currentCount <= 1)
                {
                    MessageBox.Show("You cannot delete the only remaining virtual desktop.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                string desktopName = _desktopItemBeingRenamed.Name;
                var result = MessageBox.Show(
                    $"Are you sure you want to delete the virtual desktop '{desktopName}'?{Environment.NewLine}Any open windows on this desktop will be moved to an adjacent workspace.",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning,
                    MessageBoxResult.No);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        Logger.LogInfo($"Deleting virtual desktop index {_desktopItemBeingRenamed.Index} ('{desktopName}')...");
                        var d = Desktop.FromIndex(_desktopItemBeingRenamed.Index);
                        d.Remove();
                        
                        // Immediately refresh UI list
                        UpdateDesktopsList();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to delete virtual desktop index {_desktopItemBeingRenamed.Index}.", ex);
                        MessageBox.Show($"Failed to delete virtual desktop: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void SaveRename()
        {
            if (_desktopItemBeingRenamed != null)
            {
                string newName = RenameTextBox.Text?.Trim() ?? "";
                if (!string.IsNullOrEmpty(newName))
                {
                    try
                    {
                        Logger.LogInfo($"Renaming desktop index {_desktopItemBeingRenamed.Index} to '{newName}'");
                        
                        var d = Desktop.FromIndex(_desktopItemBeingRenamed.Index);
                        d.SetName(newName);
                        
                        // Immediately update local item and UI
                        _desktopItemBeingRenamed.Name = newName;
                        UpdateDesktopsList();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to rename desktop index {_desktopItemBeingRenamed.Index}.", ex);
                    }
                }
            }
            RenamePopup.IsOpen = false;
        }

        private void ExpandNotes()
        {
            EditorRow.Height = new GridLength(234); // Expand row 1 height to fit TextBox + Done Button
            ToggleIndicator.Text = "▴";
            
            // Focus the text box
            NotesTextBox.Focus();
            NotesTextBox.SelectionStart = NotesTextBox.Text?.Length ?? 0;
            Logger.LogInfo("Expanded standard Notes panel.");
        }

        private void CollapseNotes()
        {
            EditorRow.Height = new GridLength(0); // Collapse row 1
            ToggleIndicator.Text = "▾";
            
            // Auto-save on collapse
            SaveCurrentDesktopNote();
            Logger.LogInfo("Collapsed standard Notes panel.");
        }

        private void LayoutRoot_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateNotesVisibility();
        }

        private void MainContainer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateNotesVisibility();
        }

        private void UpdateNotesVisibility()
        {
            try
            {
                double windowWidth = LayoutRoot.ActualWidth;
                if (windowWidth <= 0)
                {
                    windowWidth = this.Width;
                }

                // 1. Calculate estimated width of horizontal desktops
                double estimatedHorizontalDesktopsWidth = 0;
                foreach (var d in Desktops)
                {
                    double textWidth = (d.Name?.Length ?? 0) * 7.5;
                    estimatedHorizontalDesktopsWidth += textWidth + 30; // Button padding & margins
                }
                estimatedHorizontalDesktopsWidth += 35; // Plus button and margins

                // DevEnv width
                double devEnvWidth = DevEnvContainer.ActualWidth > 0 ? DevEnvContainer.ActualWidth : 75;

                // 2. Decide Layout State
                bool collapseDesktops = false;
                bool collapseNotes = false;
                bool hideNotesButton = false;

                if (ShowNotesPreview)
                {
                    // Required width for horizontal desktops + full notes panel
                    double widthForFullNotesAndDesktops = estimatedHorizontalDesktopsWidth + 320 + devEnvWidth + 60;
                    
                    if (windowWidth >= widthForFullNotesAndDesktops)
                    {
                        collapseDesktops = false;
                        collapseNotes = false;
                    }
                    else
                    {
                        // Try collapsing notes to button first
                        double widthForNotesButtonAndDesktops = estimatedHorizontalDesktopsWidth + 46 + devEnvWidth + 60;
                        if (windowWidth >= widthForNotesButtonAndDesktops)
                        {
                            collapseDesktops = false;
                            collapseNotes = true;
                        }
                        else
                        {
                            // Collapsing notes is not enough -> Collapse desktops to dropdown as well
                            collapseDesktops = true;
                            collapseNotes = true;
                            
                            // Check if even collapsed desktops + notes button is too tight
                            double widthForCollapsedDesktopsAndNotesButton = 95 + 46 + devEnvWidth + 60;
                            if (windowWidth < widthForCollapsedDesktopsAndNotesButton)
                            {
                                hideNotesButton = true;
                            }
                        }
                    }
                }
                else
                {
                    // Button-only mode: standard notes panel is always collapsed
                    collapseNotes = true;
                    
                    double widthForNotesButtonAndDesktops = estimatedHorizontalDesktopsWidth + 46 + devEnvWidth + 60;
                    if (windowWidth >= widthForNotesButtonAndDesktops)
                    {
                        collapseDesktops = false;
                    }
                    else
                    {
                        collapseDesktops = true;
                        
                        double widthForCollapsedDesktopsAndNotesButton = 95 + 46 + devEnvWidth + 60;
                        if (windowWidth < widthForCollapsedDesktopsAndNotesButton)
                        {
                            hideNotesButton = true;
                        }
                    }
                }

                // 3. Apply Desktops Layout
                if (collapseDesktops)
                {
                    if (HorizontalDesktopsPanel.Visibility != Visibility.Collapsed)
                    {
                        Logger.LogInfo("Collapsing desktop buttons list to dropdown menu.");
                        HorizontalDesktopsPanel.Visibility = Visibility.Collapsed;
                        CollapsedDesktopsPanel.Visibility = Visibility.Visible;
                    }
                }
                else
                {
                    if (HorizontalDesktopsPanel.Visibility != Visibility.Visible)
                    {
                        Logger.LogInfo("Restoring horizontal desktop buttons list.");
                        HorizontalDesktopsPanel.Visibility = Visibility.Visible;
                        CollapsedDesktopsPanel.Visibility = Visibility.Collapsed;
                        if (DesktopsDropdownPopup != null)
                        {
                            DesktopsDropdownPopup.IsOpen = false;
                        }
                    }
                }

                // 4. Apply Notes Layout
                if (ShowNotesPreview && !collapseNotes)
                {
                    if (NotesContainer.Visibility != Visibility.Visible)
                    {
                        Logger.LogInfo("Restoring standard Notes panel.");
                        NotesContainer.Visibility = Visibility.Visible;
                        NotesButton.Visibility = Visibility.Collapsed;
                        NotesPopup.IsOpen = false;
                    }
                }
                else
                {
                    if (NotesContainer.Visibility != Visibility.Collapsed)
                    {
                        Logger.LogInfo("Collapsing standard Notes panel.");
                        NotesContainer.Visibility = Visibility.Collapsed;
                        CollapseNotes();
                    }

                    if (collapseNotes && !hideNotesButton)
                    {
                        if (NotesButton.Visibility != Visibility.Visible)
                        {
                            Logger.LogInfo("Showing Notes button.");
                            NotesButton.Visibility = Visibility.Visible;
                        }
                    }
                    else
                    {
                        if (NotesButton.Visibility != Visibility.Collapsed)
                        {
                            Logger.LogInfo("Hiding Notes button due to extreme space restriction.");
                            NotesButton.Visibility = Visibility.Collapsed;
                            NotesPopup.IsOpen = false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error during UpdateNotesVisibility evaluation.", ex);
            }
        }

        private void NotesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                NotesPopup.IsOpen = !NotesPopup.IsOpen;
                if (NotesPopup.IsOpen)
                {
                    Logger.LogInfo("Opened Notes Popup editor.");
                    // Focus the text box in popup and position caret at end
                    PopupNotesTextBox.Focus();
                    PopupNotesTextBox.SelectionStart = PopupNotesTextBox.Text?.Length ?? 0;
                }
                else
                {
                    Logger.LogInfo("Closed Notes Popup editor.");
                    SaveCurrentDesktopNote();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to toggle Notes Popup.", ex);
            }
        }

        private void PopupDoneButton_Click(object sender, RoutedEventArgs e)
        {
            NotesPopup.IsOpen = false;
            SaveCurrentDesktopNote();
            Logger.LogInfo("Closed Notes Popup editor via Done button click.");
        }

        #endregion

        #region Dev Environment Launcher

        public static string SanitizeFolderName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Default";
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = new string(name.Where(c => !invalidChars.Contains(c)).ToArray()).Trim();
            return string.IsNullOrEmpty(sanitized) ? "Default" : sanitized;
        }

        private void SpawnDevTool(string desktopName, string toolName)
        {
            string sanitized = SanitizeFolderName(desktopName);
            string folderPath = Path.Combine(@"C:\dev", sanitized);

            try
            {
                Logger.LogInfo($"Preparing dev tool '{toolName}' in: {folderPath}");
                if (!Directory.Exists(folderPath))
                {
                    Logger.LogInfo($"Directory does not exist. Creating directory: {folderPath}");
                    Directory.CreateDirectory(folderPath);
                }

                // 1. Spawn Windows Terminal
                if (toolName == "Terminal" || toolName == "All")
                {
                    try
                    {
                        Logger.LogInfo("Spawning Windows Terminal...");
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "wt.exe",
                            Arguments = $"-d \"{folderPath}\"",
                            UseShellExecute = true
                        });
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Failed to spawn Windows Terminal.", ex);
                    }
                }

                // 2. Spawn VS Code
                if (toolName == "VSCode" || toolName == "All")
                {
                    try
                    {
                        Logger.LogInfo("Spawning VS Code...");
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "code",
                            Arguments = $"\"{folderPath}\"",
                            UseShellExecute = true,
                            CreateNoWindow = true
                        });
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"Failed to spawn VS Code (is VS Code installed?): {ex.Message}");
                    }
                }

                // 3. Spawn GitExtensions
                if (toolName == "GitExtensions" || toolName == "All")
                {
                    try
                    {
                        Logger.LogInfo("Spawning GitExtensions...");
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "gitex",
                            Arguments = $"\"{folderPath}\"",
                            UseShellExecute = true
                        });
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"Failed to spawn GitExtensions (is GitExtensions installed?): {ex.Message}");
                    }
                }

                // 4. Spawn File Explorer
                if (toolName == "Explorer" || toolName == "All")
                {
                    try
                    {
                        Logger.LogInfo("Spawning File Explorer...");
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "explorer.exe",
                            Arguments = $"\"{folderPath}\"",
                            UseShellExecute = true
                        });
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Failed to spawn File Explorer.", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to prepare or spawn dev tool '{toolName}' for '{desktopName}':", ex);
                throw;
            }
        }

        #endregion

        #region Multi-Monitor Synchronization

        private void SyncNotesToAll(string text)
        {
            foreach (Window win in System.Windows.Application.Current.Windows)
            {
                if (win is MainWindow mainWin && mainWin != this)
                {
                    if (mainWin._currentNoteText != text)
                    {
                        mainWin._currentNoteText = text;
                        mainWin.OnPropertyChanged(nameof(CurrentNoteText));
                        mainWin.UpdateNotePreview(text);
                    }
                }
            }
        }

        private void SyncSecondaryWindows()
        {
            foreach (Window win in System.Windows.Application.Current.Windows)
            {
                if (win is MainWindow mainWin && !mainWin.IsPrimary)
                {
                    mainWin.SyncFromPrimary(this);
                }
            }
        }

        public void SyncFromPrimary(MainWindow primary)
        {
            // 1. Sync Desktops list
            if (this.Desktops.Count != primary.Desktops.Count)
            {
                this.Desktops.Clear();
                foreach (var item in primary.Desktops)
                {
                    this.Desktops.Add(new DesktopItem
                    {
                        Index = item.Index,
                        Name = item.Name,
                        IsActive = item.IsActive
                    });
                }
            }
            else
            {
                for (int i = 0; i < primary.Desktops.Count; i++)
                {
                    if (this.Desktops[i].Name != primary.Desktops[i].Name)
                    {
                        this.Desktops[i].Name = primary.Desktops[i].Name;
                    }
                    if (this.Desktops[i].IsActive != primary.Desktops[i].IsActive)
                    {
                        this.Desktops[i].IsActive = primary.Desktops[i].IsActive;
                    }
                }
            }

            this._lastActiveIndex = primary._lastActiveIndex;
            this._currentDesktopGuid = primary._currentDesktopGuid;

            // 2. Sync Note Text
            if (this._currentNoteText != primary._currentNoteText)
            {
                this._currentNoteText = primary._currentNoteText;
                OnPropertyChanged(nameof(CurrentNoteText));
                UpdateNotePreview(primary._currentNoteText);
            }

            // 3. Sync Notes Preview settings
            if (this._showNotesPreview != primary._showNotesPreview)
            {
                this._showNotesPreview = primary._showNotesPreview;
                OnPropertyChanged(nameof(ShowNotesPreview));
                UpdateNotesVisibility();
            }
        }

        #endregion
    }
}