using System;
using System.Windows;
using System.Windows.Threading;

namespace DesktopHeader.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public App()
    {
        // Catch unhandled exceptions on the UI thread
        this.DispatcherUnhandledException += OnDispatcherUnhandledException;

        // Catch unhandled exceptions on background threads
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
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
