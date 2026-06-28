using System;
using System.Windows;
using System.Windows.Threading;

namespace DesktopHeader.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private static System.Threading.Mutex? _mutex;

    public App()
    {
        // Catch unhandled exceptions on the UI thread
        this.DispatcherUnhandledException += OnDispatcherUnhandledException;

        // Catch unhandled exceptions on background threads
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            System.Windows.Forms.Application.SetHighDpiMode(System.Windows.Forms.HighDpiMode.PerMonitorV2);
        }
        catch (Exception ex)
        {
            Logger.LogWarning("Failed to set Windows Forms HighDpiMode to PerMonitorV2: " + ex.Message);
        }

        const string mutexName = "Global\\DesktopHeaderOverlay_SingleInstanceMutex";
        _mutex = new System.Threading.Mutex(true, mutexName, out bool createdNew);

        if (!createdNew)
        {
            Logger.LogWarning("Another instance of Desktop Header Overlay is already running. Exiting process to prevent visual conflicts.");
            System.Windows.Application.Current.Shutdown();
            return;
        }

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_mutex != null)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch { }
            _mutex.Dispose();
        }
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Logger.LogError("[CRASH] Unhandled UI thread exception", e.Exception);
        e.Handled = true; // Prevent process exit so we can see the log
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var ex = e.ExceptionObject as Exception;
        Logger.LogError($"[CRASH] Unhandled background thread exception (IsTerminating={e.IsTerminating})", ex);
    }
}
