using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace AutoClicker.Services;

public static class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "ClickboyBBQ";

    public static string GetExecutablePath()
    {
        return Environment.ProcessPath 
            ?? Process.GetCurrentProcess().MainModule?.FileName 
            ?? @"R:\sheeshfr\Clickboy BBQ\publish\ClickboyBBQ.exe";
    }

    public static bool IsStartupEnabled()
    {
        if (!OperatingSystem.IsWindows()) return false;

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
            return key?.GetValue(AppName) != null;
        }
        catch
        {
            return false;
        }
    }

    public static void ApplyStartup(bool enable, bool openMinimized)
    {
        if (!OperatingSystem.IsWindows()) return;

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
            if (key == null) return;

            if (enable)
            {
                string exePath = GetExecutablePath();
                string command = $"\"{exePath}\"";
                if (openMinimized)
                {
                    command += " --minimized";
                }
                key.SetValue(AppName, command);
            }
            else
            {
                key.DeleteValue(AppName, false);
            }
        }
        catch
        {
            // Silently handle if registry access is constrained in sandbox
        }
    }
}
