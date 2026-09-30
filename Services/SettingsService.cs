using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using AutoClicker.Models;

namespace AutoClicker.Services;

public class AppConfig
{
    public int IntervalMs { get; set; } = 50;
    public TriggerMode TriggerMode { get; set; } = TriggerMode.Hold;
    public int HotkeyVkCode { get; set; } = 0x75; // F6
    public MouseButtonType MouseButton { get; set; } = MouseButtonType.Left;
    public int PrimaryVkCode { get; set; } = 0x01; // Left Click
    public bool BlockHotkey { get; set; } = false;
    public bool StopOnEscape { get; set; } = true;
    public bool CloseToTray { get; set; } = true;

    // Modifier Slot Options
    public bool IsModifierEnabled { get; set; } = false;
    public MouseButtonType ModifierButton { get; set; } = MouseButtonType.Right;
    public int ModifierVkCode { get; set; } = 0x02; // Right Click
    public ModifierAction ModifierAction { get; set; } = ModifierAction.Hold;
    public ModifierOrder ModifierOrder { get; set; } = ModifierOrder.ModifierFirst;
    public int ModifierDelayMs { get; set; } = 1;
}


[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppConfig))]
internal partial class AppConfigJsonContext : JsonSerializerContext
{
}

public static class SettingsService
{
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MacroBBQ");
    private static readonly string SettingsPath = Path.Combine(SettingsDirectory, "settings.json");
    private static readonly string OldSettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ClickboyBBQ",
        "settings.json");

    public static AppConfig Load()
    {
        try
        {
            if (!File.Exists(SettingsPath) && File.Exists(OldSettingsPath))
            {
                try
                {
                    Directory.CreateDirectory(SettingsDirectory);
                    File.Copy(OldSettingsPath, SettingsPath, true);
                }
                catch
                {
                    // Fall through to try loading old path directly if copy failed
                }
            }

            if (File.Exists(SettingsPath))
            {
                string json = File.ReadAllText(SettingsPath);
                return (AppConfig?)JsonSerializer.Deserialize(json, typeof(AppConfig), AppConfigJsonContext.Default) ?? new AppConfig();
            }
        }
        catch
        {
            // Fallback to defaults
        }
        return new AppConfig();
    }

    public static void Save(AppConfig config)
    {
        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            string json = JsonSerializer.Serialize(config, typeof(AppConfig), AppConfigJsonContext.Default);
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Silently ignore if disk/permission restricted
        }
    }
}
