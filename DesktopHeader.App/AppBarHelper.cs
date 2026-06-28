using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace DesktopHeader.App
{
    public class AppBarHelper
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct APPBARDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public int uCallbackMessage;
            public int uEdge;
            public RECT rc;
            public IntPtr lParam;
        }

        private const int ABM_NEW = 0;
        private const int ABM_REMOVE = 1;
        private const int ABM_QUERYPOS = 2;
        private const int ABM_SETPOS = 3;

        private const int ABE_TOP = 1;

        [DllImport("shell32.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern uint SHAppBarMessage(int dwMessage, ref APPBARDATA pData);

        [DllImport("user32.dll")]
        private static extern int RegisterWindowMessage(string lpString);

        private int _appbarMessageId = 0;
        private bool _isRegistered = false;
        private Window? _window;
        private int _heightPixels;
        private HwndSource? _hwndSource;
        private const int ABN_POSCHANGED = 1;
        private readonly System.Windows.Forms.Screen _targetScreen;

        public AppBarHelper(System.Windows.Forms.Screen targetScreen)
        {
            _targetScreen = targetScreen ?? System.Windows.Forms.Screen.PrimaryScreen ?? throw new InvalidOperationException("No screen detected.");
        }

        public void RegisterAppBar(Window window, int heightPixels)
        {
            if (_isRegistered) return;

            try
            {
                var helper = new WindowInteropHelper(window);
                IntPtr hWnd = helper.Handle;
                if (hWnd == IntPtr.Zero) return;

                _window = window;
                _heightPixels = heightPixels;

                string uniqueName = "AppBarMessage_" + window.Name + "_" + _targetScreen.DeviceName.Replace("\\", "_").Replace(".", "_");
                _appbarMessageId = RegisterWindowMessage(uniqueName);

                APPBARDATA abd = new APPBARDATA
                {
                    cbSize = Marshal.SizeOf(typeof(APPBARDATA)),
                    hWnd = hWnd,
                    uCallbackMessage = _appbarMessageId
                };

                // Register new AppBar with Windows Shell
                SHAppBarMessage(ABM_NEW, ref abd);
                _isRegistered = true;

                // Add WndProc hook to handle ABN_POSCHANGED callback from Windows Shell
                _hwndSource = HwndSource.FromHwnd(hWnd);
                _hwndSource?.AddHook(WndProc);

                // Request top reservation and position window
                SizeAppBar(window, heightPixels);
                
                // Automatically unregister when window closes to avoid leaving a dead gap
                window.Closing += (s, e) => UnregisterAppBar(window);
                Logger.LogInfo($"Successfully registered Window as Windows AppBar on screen: {_targetScreen.DeviceName}.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to register Window as AppBar on screen: {_targetScreen.DeviceName}.", ex);
            }
        }

        public void UnregisterAppBar(Window window)
        {
            if (!_isRegistered) return;

            try
            {
                if (_hwndSource != null)
                {
                    _hwndSource.RemoveHook(WndProc);
                    _hwndSource = null;
                }

                var helper = new WindowInteropHelper(window);
                IntPtr hWnd = helper.Handle;
                if (hWnd == IntPtr.Zero) return;

                APPBARDATA abd = new APPBARDATA
                {
                    cbSize = Marshal.SizeOf(typeof(APPBARDATA)),
                    hWnd = hWnd
                };

                SHAppBarMessage(ABM_REMOVE, ref abd);
                _isRegistered = false;
                _window = null;
                Logger.LogInfo($"Successfully unregistered Window as Windows AppBar on screen: {_targetScreen.DeviceName}.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to unregister Window as AppBar on screen: {_targetScreen.DeviceName}.", ex);
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == _appbarMessageId)
            {
                switch (wParam.ToInt32())
                {
                    case ABN_POSCHANGED:
                        if (_window != null)
                        {
                            Logger.LogInfo($"AppBar on screen {_targetScreen.DeviceName} received ABN_POSCHANGED. Re-triggering layout...");
                            SizeAppBar(_window, _heightPixels);
                        }
                        handled = true;
                        break;
                }
            }
            return IntPtr.Zero;
        }

        public void SizeAppBar(Window window, int heightPixels)
        {
            var helper = new WindowInteropHelper(window);
            IntPtr hWnd = helper.Handle;
            if (hWnd == IntPtr.Zero) return;

            try
            {
                APPBARDATA abd = new APPBARDATA
                {
                    cbSize = Marshal.SizeOf(typeof(APPBARDATA)),
                    hWnd = hWnd,
                    uEdge = ABE_TOP
                };

                // Use the targeted screen dimensions for the AppBar bounds (in physical pixels)
                abd.rc.left = _targetScreen.Bounds.X;
                abd.rc.top = _targetScreen.Bounds.Y;
                abd.rc.right = _targetScreen.Bounds.X + _targetScreen.Bounds.Width;
                abd.rc.bottom = _targetScreen.Bounds.Y + heightPixels;

                // 1. Query Shell for safe top rect on this monitor
                SHAppBarMessage(ABM_QUERYPOS, ref abd);
                
                // 2. Adjust and Set Pos
                abd.rc.bottom = abd.rc.top + heightPixels;
                SHAppBarMessage(ABM_SETPOS, ref abd);

                // 3. Pin the window to the designated screen bounds.
                // The AppBar RECT tells Windows Shell to reserve the workspace; the WPF window stays its natural width.
                window.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        var dpi = VisualTreeHelper.GetDpi(window);
                        double dpiScaleX = dpi.DpiScaleX;
                        double dpiScaleY = dpi.DpiScaleY;

                        // Position window using WPF device-independent units (DIPs)
                        window.Top = abd.rc.top / dpiScaleY;
                        window.Left = abd.rc.left / dpiScaleX;
                        window.Width = (abd.rc.right - abd.rc.left) / dpiScaleX;
                    }
                    catch (Exception dispEx)
                    {
                        Logger.LogError($"Failed to reposition window on screen {_targetScreen.DeviceName} after AppBar registration.", dispEx);
                    }
                }), System.Windows.Threading.DispatcherPriority.Send);

                Logger.LogInfo($"AppBar on screen {_targetScreen.DeviceName} resized successfully. WorkArea Reserved Height: {heightPixels}px. Bounds: L={abd.rc.left}, T={abd.rc.top}, R={abd.rc.right}, B={abd.rc.bottom}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to size/dock AppBar on screen {_targetScreen.DeviceName}.", ex);
            }
        }
    }
}
