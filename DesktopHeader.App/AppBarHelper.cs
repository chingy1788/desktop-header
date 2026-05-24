using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DesktopHeader.App
{
    public static class AppBarHelper
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

        private static int _appbarMessageId = 0;
        private static bool _isRegistered = false;
        private static Window? _window;
        private static int _heightPixels;
        private static HwndSource? _hwndSource;
        private const int ABN_POSCHANGED = 1;

        public static void RegisterAppBar(Window window, int heightPixels)
        {
            if (_isRegistered) return;

            try
            {
                var helper = new WindowInteropHelper(window);
                IntPtr hWnd = helper.Handle;
                if (hWnd == IntPtr.Zero) return;

                _window = window;
                _heightPixels = heightPixels;

                _appbarMessageId = RegisterWindowMessage("AppBarMessage_" + window.Name);

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
                Logger.LogInfo("Successfully registered Window as Windows AppBar.");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to register Window as AppBar.", ex);
            }
        }

        public static void UnregisterAppBar(Window window)
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
                Logger.LogInfo("Successfully unregistered Window as Windows AppBar.");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to unregister Window as AppBar.", ex);
            }
        }

        private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == _appbarMessageId)
            {
                switch (wParam.ToInt32())
                {
                    case ABN_POSCHANGED:
                        if (_window != null)
                        {
                            Logger.LogInfo("AppBar received ABN_POSCHANGED from Windows Shell. Re-triggering layout...");
                            SizeAppBar(_window, _heightPixels);
                        }
                        handled = true;
                        break;
                }
            }
            return IntPtr.Zero;
        }

        public static void SizeAppBar(Window window, int heightPixels)
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

                // Query and Set pos based on primary monitor dimensions
                double screenWidth = SystemParameters.PrimaryScreenWidth;

                abd.rc.left = 0;
                abd.rc.top = 0;
                abd.rc.right = (int)screenWidth;
                abd.rc.bottom = heightPixels;

                // 1. Query Shell for safe top rect
                SHAppBarMessage(ABM_QUERYPOS, ref abd);
                
                // 2. Adjust and Set Pos
                abd.rc.bottom = abd.rc.top + heightPixels;
                SHAppBarMessage(ABM_SETPOS, ref abd);

                // 3. Only pin the window to Y=0 — do NOT resize width, that causes a fatal layout crash.
                // The AppBar RECT tells Windows Shell to reserve the workspace; the WPF window stays its natural width.
                window.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        window.Top = abd.rc.top;
                        // Left-align to the screen (with a sleek 16px safe margin)
                        window.Left = 16;
                    }
                    catch (Exception dispEx)
                    {
                        Logger.LogError("Failed to reposition window after AppBar registration.", dispEx);
                    }
                }), System.Windows.Threading.DispatcherPriority.Send);

                Logger.LogInfo($"AppBar resized successfully. WorkArea Reserved Height: {heightPixels}px. Bounds: L={abd.rc.left}, T={abd.rc.top}, R={abd.rc.right}, B={abd.rc.bottom}");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to size/dock AppBar.", ex);
            }
        }
    }
}
