using Microsoft.Win32;
using System.IO;

namespace File_manager.Services
{
    // Керує автозапуском через реєстр Windows
    public static class StartupManager
    {
        private const string AppName = "AssetExplorer";
        private const string RegKey  = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

        public static bool IsEnabled()
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegKey, false);
            return key?.GetValue(AppName) != null;
        }

        public static void Enable()
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegKey, true);
            var exePath = System.Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "File-manager.exe");
            key?.SetValue(AppName, $"\"{exePath}\" --minimized");
        }

        public static void Disable()
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegKey, true);
            key?.DeleteValue(AppName, throwOnMissingValue: false);
        }
    }
}