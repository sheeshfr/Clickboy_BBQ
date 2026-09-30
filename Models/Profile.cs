using System;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using AutoClicker.Services.Native;

namespace AutoClicker.Models;

public partial class Profile : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    private string _name = "Default";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    private int _intervalMs = 50;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    private TriggerMode _triggerMode = TriggerMode.Hold;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    private int _hotkeyVkCode = 0x75; // F6

    [ObservableProperty]
    private MouseButtonType _mouseButton = MouseButtonType.Left;

    [ObservableProperty]
    private int _primaryVkCode = 0x01; // Left Click

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    private bool _isModifierEnabled = false;

    [ObservableProperty]
    private MouseButtonType _modifierButton = MouseButtonType.Right;

    [ObservableProperty]
    private int _modifierVkCode = 0x02; // Right Click

    [ObservableProperty]
    private ModifierAction _modifierAction = ModifierAction.Hold;

    [ObservableProperty]
    private ModifierOrder _modifierOrder = ModifierOrder.ModifierFirst;

    [ObservableProperty]
    private int _modifierDelayMs = 1;

    [ObservableProperty]
    private bool _blockHotkey = false;

    // Runtime UI state (not serialized to JSON)
    [JsonIgnore]
    [ObservableProperty]
    private bool _isActive = false;

    [JsonIgnore]
    [ObservableProperty]
    private bool _canDelete = false;

    [JsonIgnore]
    public string SummaryText
    {
        get
        {
            string key = KeyHelper.GetKeyName(HotkeyVkCode);
            string mode = TriggerMode == TriggerMode.Hold ? "Hold" : "Toggle";
            string mod = IsModifierEnabled ? " • Mod" : "";
            return $"{key} • {IntervalMs}ms • {mode}{mod}";
        }
    }

    public Profile Clone(string newName)
    {
        return new Profile
        {
            Name = newName,
            IntervalMs = this.IntervalMs,
            TriggerMode = this.TriggerMode,
            HotkeyVkCode = this.HotkeyVkCode,
            MouseButton = this.MouseButton,
            PrimaryVkCode = this.PrimaryVkCode,
            IsModifierEnabled = this.IsModifierEnabled,
            ModifierButton = this.ModifierButton,
            ModifierVkCode = this.ModifierVkCode,
            ModifierAction = this.ModifierAction,
            ModifierOrder = this.ModifierOrder,
            ModifierDelayMs = this.ModifierDelayMs,
            BlockHotkey = this.BlockHotkey
        };
    }
}
