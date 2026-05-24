using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using DesktopHeader.App.Models;
using VirtualDesktop;

namespace DesktopHeader.App
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<DesktopItem> Desktops { get; } = new();
        private readonly DispatcherTimer _timer;
        private int _lastActiveIndex = -1;
        private bool _isPinned = false;
        private IntPtr _windowHandle = IntPtr.Zero;
        private int _pinRetryCount = 0;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            Logger.LogInfo("Desktop Header Overlay initializing...");

            // Initialize DispatcherTimer for highly lightweight 250ms polling
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            _timer.Tick += Timer_Tick;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Logger.LogInfo("MainWindow Loaded. Setting up window parameters...");

            // Pin to top-left; AppBar registration will set exact position and stretch to full width
            this.Left = 0;
            this.Top = 0;

            // Initial load of desktops
            UpdateDesktopsList();

            // Get window handle and attempt initial pinning
            var helper = new WindowInteropHelper(this);
            _windowHandle = helper.Handle;

            TryPinWindow();

            // Start the polling timer
            _timer.Start();
            Logger.LogInfo($"Polling timer started. Initially loaded {Desktops.Count} desktops.");
        }

        private void Window_ContentRendered(object? sender, EventArgs e)
        {
            // ContentRendered fires after the first render pass — ActualHeight is now accurate.
            // Convert device-independent height to physical pixels using DPI scale.
            var dpiScale = VisualTreeHelper.GetDpi(this);
            int heightPixels = (int)Math.Ceiling(this.ActualHeight * dpiScale.DpiScaleY);
            Logger.LogInfo($"ContentRendered: window height = {this.ActualHeight} DIPs -> {heightPixels}px physical (DPI Y scale: {dpiScale.DpiScaleY})");

            // Register as Windows AppBar — reserves exact header height so all windows dock below
            try
            {
                AppBarHelper.RegisterAppBar(this, heightPixels);
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to register WPF window as Windows AppBar.", ex);
            }
        }


        private void Timer_Tick(object? sender, EventArgs e)
        {
            // Self-healing retry for window pinning if it failed during initial load
            if (!_isPinned && _pinRetryCount < 10)
            {
                TryPinWindow();
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
            try
            {
                int count = Desktop.Count;
                if (count <= 0) return;

                Desktop currentActive = Desktop.Current;
                int currentActiveIndex = Desktop.FromDesktop(currentActive);

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
                }
                else
                {
                    // Otherwise, efficiently update properties individually to avoid flickering
                    bool activeIndexChanged = false;
                    for (int i = 0; i < count; i++)
                    {
                        string name = Desktop.DesktopNameFromIndex(i);
                        bool isCurrentActive = (i == currentActiveIndex);

                        if (Desktops[i].Name != name)
                        {
                            Logger.LogInfo($"Desktop [{i}] renamed from '{Desktops[i].Name}' to '{name}'.");
                            Desktops[i].Name = name;
                        }

                        if (Desktops[i].IsActive != isCurrentActive)
                        {
                            Desktops[i].IsActive = isCurrentActive;
                            activeIndexChanged = true;
                        }
                    }

                    if (activeIndexChanged && _lastActiveIndex != currentActiveIndex)
                    {
                        Logger.LogInfo($"Active virtual desktop switched to index {currentActiveIndex} ('{Desktops[currentActiveIndex].Name}').");
                        _lastActiveIndex = currentActiveIndex;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error occurred while polling/updating desktops list.", ex);
            }
        }

        private void DesktopButton_Click(object sender, RoutedEventArgs e)
        {
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
                    
                    targetDesktop.MakeVisible();
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to switch to virtual desktop index {clickedItem.Index}.", ex);
                    MessageBox.Show($"Failed to switch desktop: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}