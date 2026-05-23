using System;
using System.IO;

namespace DesktopHeader.App
{
    public static class Logger
    {
        private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug.log");
        private static readonly object LockObj = new();

        public static void LogInfo(string message) => Log("INFO", message);
        public static void LogWarning(string message) => Log("WARN", message);
        public static void LogError(string message, Exception? ex = null) => Log("ERROR", $"{message}{(ex != null ? $" - {ex.Message}\n{ex.StackTrace}" : "")}");

        private static void Log(string level, string message)
        {
            try
            {
                lock (LockObj)
                {
                    string logLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}{Environment.NewLine}";
                    File.AppendAllText(LogPath, logLine);
                }
            }
            catch
            {
                // Fallback to diagnostics debug console if log writing fails
                System.Diagnostics.Debug.WriteLine($"[FAILED LOG WRITE] [{level}] {message}");
            }
        }
    }
}
