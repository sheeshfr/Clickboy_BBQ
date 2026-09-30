using System;
using System.Diagnostics;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AutoClicker.Models;
using AutoClicker.Services;
using AutoClicker.Services.Native;

namespace AutoClicker.ViewModels;

public partial class MainViewModel : ViewModelBase, IDisposable
{
    private readonly AutoClickerEngine _engine;
    private readonly GlobalKeyboardHook _hook;
    private DispatcherTimer? _countdownTimer;
    private RecordingSlot _activeRecordingSlot = RecordingSlot.None;
    private int _slotCountdownRemaining = 5;
    private long _lastClickCountUpdateTime = 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EstimatedCpsText))]
    private int _intervalMs = 50;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsToggleMode))]
    [NotifyPropertyChangedFor(nameof(IsHoldMode))]
    [NotifyPropertyChangedFor(nameof(StatusDescription))]
    private TriggerMode _selectedTriggerMode = TriggerMode.Hold;

    public bool IsToggleMode
    {
        get => SelectedTriggerMode == TriggerMode.Toggle;
        set => SelectedTriggerMode = value ? TriggerMode.Toggle : TriggerMode.Hold;
    }

    public bool IsHoldMode
    {
        get => SelectedTriggerMode == TriggerMode.Hold;
        set => SelectedTriggerMode = value ? TriggerMode.Hold : TriggerMode.Toggle;
    }

    // Primary Button Slot
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLeftButton))]
    [NotifyPropertyChangedFor(nameof(IsRightButton))]
    [NotifyPropertyChangedFor(nameof(IsMiddleButton))]
    private MouseButtonType _selectedMouseButton = MouseButtonType.Left;

    public bool IsLeftButton
    {
        get => SelectedMouseButton == MouseButtonType.Left;
        set { if (value) SelectedMouseButton = MouseButtonType.Left; }
    }

    public bool IsRightButton
    {
        get => SelectedMouseButton == MouseButtonType.Right;
        set { if (value) SelectedMouseButton = MouseButtonType.Right; }
    }

    public bool IsMiddleButton
    {
        get => SelectedMouseButton == MouseButtonType.Middle;
        set { if (value) SelectedMouseButton = MouseButtonType.Middle; }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PrimaryKeyButtonText))]
    private int _primaryVkCode = Win32Api.VK_LBUTTON;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PrimaryKeyButtonText))]
    private string _primaryButtonName = "M1 (Left)";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PrimaryKeyButtonText))]
    [NotifyPropertyChangedFor(nameof(StatusDisplay))]
    [NotifyPropertyChangedFor(nameof(StatusDescription))]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    [NotifyPropertyChangedFor(nameof(IsActiveRunning))]
    private bool _isRecordingPrimary = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PrimaryKeyButtonText))]
    private int _primaryCountdownSeconds = 5;

    public string PrimaryKeyButtonText => IsRecordingPrimary ? $"waiting...{PrimaryCountdownSeconds}" : PrimaryButtonName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EstimatedCpsText))]
    [NotifyPropertyChangedFor(nameof(IsSingleClick))]
    [NotifyPropertyChangedFor(nameof(IsDoubleClick))]
    private ClickType _selectedClickType = ClickType.Single;

    public bool IsSingleClick
    {
        get => SelectedClickType == ClickType.Single;
        set { if (value) SelectedClickType = ClickType.Single; }
    }

    public bool IsDoubleClick
    {
        get => SelectedClickType == ClickType.Double;
        set { if (value) SelectedClickType = ClickType.Double; }
    }

    // Hotkey Slot
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusDescription))]
    [NotifyPropertyChangedFor(nameof(ActionToggleText))]
    [NotifyPropertyChangedFor(nameof(KeyButtonText))]
    private string _hotkeyName = "F6";

    [ObservableProperty]
    private int _hotkeyVkCode = 0x75; // F6

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusDisplay))]
    [NotifyPropertyChangedFor(nameof(StatusDescription))]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    [NotifyPropertyChangedFor(nameof(IsActiveRunning))]
    [NotifyPropertyChangedFor(nameof(KeyButtonText))]
    private bool _isRecordingHotkey = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KeyButtonText))]
    private int _countdownSeconds = 5;

    public string KeyButtonText => IsRecordingHotkey ? $"waiting...{CountdownSeconds}" : HotkeyName;

    // Modifier / Secondary Slot
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ModifierBadgeText))]
    [NotifyPropertyChangedFor(nameof(ModifierStatusText))]
    private bool _isModifierEnabled = false;

    public string ModifierBadgeText => IsModifierEnabled ? "⚡ MOD" : "⚡ Mod";
    public string ModifierStatusText => IsModifierEnabled ? $"{ModifierButtonName} ({SelectedModifierAction})" : "Off";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ModifierKeyButtonText))]
    [NotifyPropertyChangedFor(nameof(ModifierStatusText))]
    private int _modifierVkCode = Win32Api.VK_RBUTTON;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ModifierKeyButtonText))]
    [NotifyPropertyChangedFor(nameof(ModifierStatusText))]
    private string _modifierButtonName = "M2 (Right)";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ModifierKeyButtonText))]
    [NotifyPropertyChangedFor(nameof(StatusDisplay))]
    [NotifyPropertyChangedFor(nameof(StatusDescription))]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    [NotifyPropertyChangedFor(nameof(IsActiveRunning))]
    private bool _isRecordingModifier = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ModifierKeyButtonText))]
    private int _modifierCountdownSeconds = 5;

    public string ModifierKeyButtonText => IsRecordingModifier ? $"waiting...{ModifierCountdownSeconds}" : ModifierButtonName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsModRightButton))]
    [NotifyPropertyChangedFor(nameof(IsModLeftButton))]
    [NotifyPropertyChangedFor(nameof(IsModMiddleButton))]
    [NotifyPropertyChangedFor(nameof(ModifierStatusText))]
    private MouseButtonType _selectedModifierButton = MouseButtonType.Right;

    public bool IsModRightButton
    {
        get => SelectedModifierButton == MouseButtonType.Right;
        set { if (value) SelectedModifierButton = MouseButtonType.Right; }
    }

    public bool IsModLeftButton
    {
        get => SelectedModifierButton == MouseButtonType.Left;
        set { if (value) SelectedModifierButton = MouseButtonType.Left; }
    }

    public bool IsModMiddleButton
    {
        get => SelectedModifierButton == MouseButtonType.Middle;
        set { if (value) SelectedModifierButton = MouseButtonType.Middle; }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsModHoldAction))]
    [NotifyPropertyChangedFor(nameof(IsModSpamAction))]
    [NotifyPropertyChangedFor(nameof(ModifierStatusText))]
    private ModifierAction _selectedModifierAction = ModifierAction.Hold;

    public bool IsModHoldAction
    {
        get => SelectedModifierAction == ModifierAction.Hold;
        set { if (value) SelectedModifierAction = ModifierAction.Hold; }
    }

    public bool IsModSpamAction
    {
        get => SelectedModifierAction == ModifierAction.Spam;
        set { if (value) SelectedModifierAction = ModifierAction.Spam; }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsModFirstOrder))]
    [NotifyPropertyChangedFor(nameof(IsPrimaryFirstOrder))]
    private ModifierOrder _selectedModifierOrder = ModifierOrder.ModifierFirst;

    public bool IsModFirstOrder
    {
        get => SelectedModifierOrder == ModifierOrder.ModifierFirst;
        set { if (value) SelectedModifierOrder = ModifierOrder.ModifierFirst; }
    }

    public bool IsPrimaryFirstOrder
    {
        get => SelectedModifierOrder == ModifierOrder.PrimaryFirst;
        set { if (value) SelectedModifierOrder = ModifierOrder.PrimaryFirst; }
    }

    [ObservableProperty]
    private int _modifierDelayMs = 1;

    // Window & Settings Options
    [ObservableProperty]
    private bool _isTopmost = true;

    [ObservableProperty]
    private bool _blockHotkey = false;

    [ObservableProperty]
    private bool _stopOnEscape = true;

    [ObservableProperty]
    private bool _closeToTray = true;

    [ObservableProperty]
    private bool _isSettingsOpen = false;

    [ObservableProperty]
    private bool _isModifierPopupOpen = false;

    // Engine & Runtime State
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusDisplay))]
    [NotifyPropertyChangedFor(nameof(StatusDescription))]
    [NotifyPropertyChangedFor(nameof(ActionToggleText))]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    [NotifyPropertyChangedFor(nameof(IsActiveRunning))]
    private bool _isRunning = false;

    public bool IsRecordingAny => IsRecordingHotkey || IsRecordingPrimary || IsRecordingModifier;
    public bool IsIdle => !IsRunning && !IsRecordingAny;
    public bool IsActiveRunning => IsRunning && !IsRecordingAny;

    [ObservableProperty]
    private long _totalClicks = 0;

    public string StatusDisplay
    {
        get
        {
            if (IsRecordingAny) return "RECORDING";
            return IsRunning ? "CLICKING" : "IDLE";
        }
    }

    public string StatusDescription
    {
        get
        {
            if (IsRecordingHotkey) return "Recording HOTKEY: Press any key or mouse button • Esc cancels";
            if (IsRecordingPrimary) return "Recording PRIMARY BUTTON: Press any mouse button or key • Esc cancels";
            if (IsRecordingModifier) return "Recording SECONDARY BUTTON: Press any mouse button or key • Esc cancels";

            if (IsRunning)
            {
                return SelectedTriggerMode == TriggerMode.Hold
                    ? $"Holding [{HotkeyName}] to click..."
                    : $"Press [{HotkeyName}] or Esc to stop clicking";
            }
            return SelectedTriggerMode == TriggerMode.Hold
                ? $"Hold down [{HotkeyName}] anywhere to click"
                : $"Press [{HotkeyName}] anywhere to start clicking";
        }
    }

    public string ActionToggleText => IsRunning ? $"⏹ Stop Clicking ({HotkeyName})" : $"▶ Start Clicking ({HotkeyName})";

    public string EstimatedCpsText
    {
        get
        {
            if (IntervalMs <= 0) return "0 CPS";
            double clicksPerInterval = SelectedClickType == ClickType.Double ? 2.0 : 1.0;
            double cps = (1000.0 / IntervalMs) * clicksPerInterval;
            return cps >= 100 ? $"{cps:F0} CPS" : $"{cps:F1} CPS";
        }
    }

    public MainViewModel(AppConfig? initialConfig = null)
    {
        var config = initialConfig ?? SettingsService.Load();
        _intervalMs = config.IntervalMs;
        _selectedTriggerMode = config.TriggerMode;
        _hotkeyVkCode = config.HotkeyVkCode;
        _hotkeyName = KeyHelper.GetKeyName(config.HotkeyVkCode);
        _selectedMouseButton = config.MouseButton;
        _primaryVkCode = config.PrimaryVkCode != 0 ? config.PrimaryVkCode : AutoClickerEngine.MouseButtonToVkCode(config.MouseButton);
        _primaryButtonName = KeyHelper.GetKeyName(_primaryVkCode);
        _blockHotkey = config.BlockHotkey;
        _stopOnEscape = config.StopOnEscape;
        _closeToTray = config.CloseToTray;

        _isModifierEnabled = config.IsModifierEnabled;
        _selectedModifierButton = config.ModifierButton;
        _modifierVkCode = config.ModifierVkCode != 0 ? config.ModifierVkCode : AutoClickerEngine.MouseButtonToVkCode(config.ModifierButton);
        _modifierButtonName = KeyHelper.GetKeyName(_modifierVkCode);
        _selectedModifierAction = config.ModifierAction;
        _selectedModifierOrder = config.ModifierOrder;
        _modifierDelayMs = Math.Max(1, config.ModifierDelayMs);

        _engine = new AutoClickerEngine();
        _hook = new GlobalKeyboardHook();

        _hook.HotkeyVkCode = _hotkeyVkCode;
        _hook.BlockHotkey = _blockHotkey;
        _hook.StopOnEscape = _stopOnEscape;

        _engine.StateChanged += OnEngineStateChanged;
        _engine.ClickCountUpdated += OnClickCountUpdated;

        _hook.HotkeyDown += OnHotkeyDown;
        _hook.HotkeyUp += OnHotkeyUp;
        _hook.SlotRecorded += OnSlotRecorded;
        _hook.SlotCancelled += OnSlotCancelled;
        _hook.EmergencyStopTriggered += OnEmergencyStopTriggered;

        SyncSettingsToEngine();
    }

    private void SaveConfig()
    {
        SettingsService.Save(new AppConfig
        {
            IntervalMs = IntervalMs,
            TriggerMode = SelectedTriggerMode,
            HotkeyVkCode = HotkeyVkCode,
            MouseButton = SelectedMouseButton,
            PrimaryVkCode = PrimaryVkCode,
            BlockHotkey = BlockHotkey,
            StopOnEscape = StopOnEscape,
            CloseToTray = CloseToTray,
            IsModifierEnabled = IsModifierEnabled,
            ModifierButton = SelectedModifierButton,
            ModifierVkCode = ModifierVkCode,
            ModifierAction = SelectedModifierAction,
            ModifierOrder = SelectedModifierOrder,
            ModifierDelayMs = ModifierDelayMs
        });
    }

    partial void OnBlockHotkeyChanged(bool value)
    {
        _hook.BlockHotkey = value;
        SaveConfig();
    }

    partial void OnStopOnEscapeChanged(bool value)
    {
        _hook.StopOnEscape = value;
        SaveConfig();
    }

    partial void OnCloseToTrayChanged(bool value)
    {
        SaveConfig();
    }

    partial void OnIsModifierEnabledChanged(bool value)
    {
        _engine.IsModifierEnabled = value;
        SaveConfig();
    }

    partial void OnSelectedModifierButtonChanged(MouseButtonType value)
    {
        int targetVk = AutoClickerEngine.MouseButtonToVkCode(value);
        if (ModifierVkCode != targetVk && (ModifierVkCode is 1 or 2 or 4))
        {
            ModifierVkCode = targetVk;
            ModifierButtonName = KeyHelper.GetKeyName(ModifierVkCode);
            _engine.ModifierVkCode = ModifierVkCode;
            OnPropertyChanged(nameof(ModifierKeyButtonText));
            OnPropertyChanged(nameof(ModifierStatusText));
            SaveConfig();
        }
    }

    partial void OnSelectedModifierActionChanged(ModifierAction value)
    {
        _engine.ModifierAction = value;
        SaveConfig();
    }

    partial void OnSelectedModifierOrderChanged(ModifierOrder value)
    {
        _engine.ModifierOrder = value;
        SaveConfig();
    }

    partial void OnModifierDelayMsChanged(int value)
    {
        if (value < 1) ModifierDelayMs = 1;
        _engine.ModifierDelayMs = ModifierDelayMs;
        SaveConfig();
    }

    partial void OnIntervalMsChanged(int value)
    {
        if (value < 1) IntervalMs = 1;
        _engine.IntervalMs = IntervalMs;
        SaveConfig();
    }

    partial void OnSelectedMouseButtonChanged(MouseButtonType value)
    {
        int targetVk = AutoClickerEngine.MouseButtonToVkCode(value);
        if (PrimaryVkCode != targetVk && (PrimaryVkCode is 1 or 2 or 4))
        {
            PrimaryVkCode = targetVk;
            PrimaryButtonName = KeyHelper.GetKeyName(PrimaryVkCode);
            _engine.PrimaryVkCode = PrimaryVkCode;
            OnPropertyChanged(nameof(PrimaryKeyButtonText));
            SaveConfig();
        }
    }

    partial void OnSelectedClickTypeChanged(ClickType value)
    {
        _engine.ClickType = value;
    }

    partial void OnSelectedTriggerModeChanged(TriggerMode value)
    {
        if (IsRunning)
        {
            _engine.Stop();
        }
        _hook.ResetKeyState();
        OnPropertyChanged(nameof(StatusDescription));
        SaveConfig();
    }

    private void SyncSettingsToEngine()
    {
        _engine.IntervalMs = Math.Max(1, IntervalMs);
        _engine.PrimaryVkCode = PrimaryVkCode;
        _engine.ClickType = SelectedClickType;
        _engine.IsModifierEnabled = IsModifierEnabled;
        _engine.ModifierVkCode = ModifierVkCode;
        _engine.ModifierAction = SelectedModifierAction;
        _engine.ModifierOrder = SelectedModifierOrder;
        _engine.ModifierDelayMs = Math.Max(1, ModifierDelayMs);
    }

    private void OnEmergencyStopTriggered()
    {
        if (IsRunning)
        {
            _engine.Stop();
        }
        _hook.ResetKeyState();
    }

    private void OnHotkeyDown()
    {
        if (_activeRecordingSlot != RecordingSlot.None) return;

        if (SelectedTriggerMode == TriggerMode.Toggle)
        {
            if (IsRunning)
            {
                _engine.Stop();
            }
            else
            {
                SyncSettingsToEngine();
                _engine.Start();
            }
        }
        else // Hold Mode
        {
            if (!IsRunning)
            {
                SyncSettingsToEngine();
                _engine.Start();
            }
        }
    }

    private void OnHotkeyUp()
    {
        if (_activeRecordingSlot != RecordingSlot.None) return;

        if (SelectedTriggerMode == TriggerMode.Hold)
        {
            if (IsRunning)
            {
                // Synchronous immediate stop on hook thread - zero latency secondary/primary release!
                _engine.Stop();
            }
        }
    }

    private void StartRecordingSlot(RecordingSlot slot)
    {
        if (IsRunning)
        {
            _engine.Stop();
        }

        CancelRecording();

        _activeRecordingSlot = slot;
        _slotCountdownRemaining = 5;
        _hook.CurrentRecordingSlot = slot;

        switch (slot)
        {
            case RecordingSlot.Hotkey:
                IsRecordingHotkey = true;
                CountdownSeconds = 5;
                break;
            case RecordingSlot.Primary:
                IsRecordingPrimary = true;
                PrimaryCountdownSeconds = 5;
                break;
            case RecordingSlot.Modifier:
                IsRecordingModifier = true;
                ModifierCountdownSeconds = 5;
                break;
        }

        StartCountdownTimer();
        NotifyRecordingStateChanged();
    }

    public void CancelRecording()
    {
        StopCountdownTimer();
        _activeRecordingSlot = RecordingSlot.None;
        _hook.CurrentRecordingSlot = RecordingSlot.None;

        IsRecordingHotkey = false;
        IsRecordingPrimary = false;
        IsRecordingModifier = false;

        NotifyRecordingStateChanged();
    }

    private void NotifyRecordingStateChanged()
    {
        OnPropertyChanged(nameof(KeyButtonText));
        OnPropertyChanged(nameof(PrimaryKeyButtonText));
        OnPropertyChanged(nameof(ModifierKeyButtonText));
        OnPropertyChanged(nameof(StatusDisplay));
        OnPropertyChanged(nameof(StatusDescription));
        OnPropertyChanged(nameof(IsIdle));
        OnPropertyChanged(nameof(IsActiveRunning));
    }

    private void StartCountdownTimer()
    {
        StopCountdownTimer();
        _countdownTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _countdownTimer.Tick += OnCountdownTick;
        _countdownTimer.Start();
    }

    private void StopCountdownTimer()
    {
        if (_countdownTimer != null)
        {
            _countdownTimer.Stop();
            _countdownTimer.Tick -= OnCountdownTick;
            _countdownTimer = null;
        }
    }

    private void OnCountdownTick(object? sender, EventArgs e)
    {
        if (_activeRecordingSlot == RecordingSlot.None)
        {
            StopCountdownTimer();
            return;
        }

        if (_slotCountdownRemaining > 1)
        {
            _slotCountdownRemaining--;
            switch (_activeRecordingSlot)
            {
                case RecordingSlot.Hotkey:
                    CountdownSeconds = _slotCountdownRemaining;
                    break;
                case RecordingSlot.Primary:
                    PrimaryCountdownSeconds = _slotCountdownRemaining;
                    break;
                case RecordingSlot.Modifier:
                    ModifierCountdownSeconds = _slotCountdownRemaining;
                    break;
            }
            NotifyRecordingStateChanged();
        }
        else
        {
            CancelRecording();
        }
    }

    private void OnSlotRecorded(RecordingSlot slot, int vkCode)
    {
        Dispatcher.UIThread.Post(() =>
        {
            StopCountdownTimer();
            _activeRecordingSlot = RecordingSlot.None;

            switch (slot)
            {
                case RecordingSlot.Hotkey:
                    HotkeyVkCode = vkCode;
                    HotkeyName = KeyHelper.GetKeyName(vkCode);
                    IsRecordingHotkey = false;
                    _hook.HotkeyVkCode = vkCode;
                    break;

                case RecordingSlot.Primary:
                    PrimaryVkCode = vkCode;
                    PrimaryButtonName = KeyHelper.GetKeyName(vkCode);
                    if (vkCode is 1 or 2 or 4)
                    {
                        SelectedMouseButton = AutoClickerEngine.VkCodeToMouseButton(vkCode);
                    }
                    IsRecordingPrimary = false;
                    _engine.PrimaryVkCode = vkCode;
                    break;

                case RecordingSlot.Modifier:
                    ModifierVkCode = vkCode;
                    ModifierButtonName = KeyHelper.GetKeyName(vkCode);
                    if (vkCode is 1 or 2 or 4)
                    {
                        SelectedModifierButton = AutoClickerEngine.VkCodeToMouseButton(vkCode);
                    }
                    IsRecordingModifier = false;
                    _engine.ModifierVkCode = vkCode;
                    break;
            }

            NotifyRecordingStateChanged();
            OnPropertyChanged(nameof(ModifierStatusText));
            SaveConfig();
        });
    }

    private void OnSlotCancelled(RecordingSlot slot)
    {
        Dispatcher.UIThread.Post(() =>
        {
            CancelRecording();
        });
    }

    private void OnEngineStateChanged(bool running)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsRunning = running;
            TotalClicks = _engine.TotalClicks;
            OnPropertyChanged(nameof(IsIdle));
            OnPropertyChanged(nameof(IsActiveRunning));
            OnPropertyChanged(nameof(StatusDisplay));
            OnPropertyChanged(nameof(StatusDescription));
            OnPropertyChanged(nameof(ActionToggleText));
        });
    }

    private void OnClickCountUpdated(long total)
    {
        long now = Stopwatch.GetTimestamp();
        // Throttle UI thread dispatch to ~20 FPS so message queue never lags
        if (Stopwatch.GetElapsedTime(_lastClickCountUpdateTime, now).TotalMilliseconds >= 50)
        {
            _lastClickCountUpdateTime = now;
            Dispatcher.UIThread.Post(() =>
            {
                TotalClicks = total;
            });
        }
    }

    [RelayCommand]
    public void ToggleStartStop()
    {
        if (IsRunning)
        {
            _engine.Stop();
        }
        else
        {
            SyncSettingsToEngine();
            _engine.Start();
        }
    }

    [RelayCommand]
    public void SelectToggleMode()
    {
        SelectedTriggerMode = TriggerMode.Toggle;
    }

    [RelayCommand]
    public void SelectHoldMode()
    {
        SelectedTriggerMode = TriggerMode.Hold;
    }

    // Hotkey Commands
    [RelayCommand]
    public void StartRecordingHotkey()
    {
        StartRecordingSlot(RecordingSlot.Hotkey);
    }

    [RelayCommand]
    public void CancelRecordingHotkey()
    {
        CancelRecording();
    }

    [RelayCommand]
    public void ToggleRecordingHotkey()
    {
        if (IsRecordingHotkey)
        {
            CancelRecording();
        }
        else
        {
            StartRecordingSlot(RecordingSlot.Hotkey);
        }
    }

    [RelayCommand]
    public void ResetHotkey()
    {
        CancelRecording();
        HotkeyVkCode = 0x75; // F6
        _hook.HotkeyVkCode = 0x75;
        HotkeyName = "F6";
        _hook.ResetKeyState();
        NotifyRecordingStateChanged();
        SaveConfig();
    }

    [RelayCommand]
    public void SetPresetHotkey(string? vkCodeStr)
    {
        if (int.TryParse(vkCodeStr, out int vkCode))
        {
            if (IsRunning)
            {
                _engine.Stop();
            }
            CancelRecording();
            HotkeyVkCode = vkCode;
            _hook.HotkeyVkCode = vkCode;
            HotkeyName = KeyHelper.GetKeyName(vkCode);
            _hook.ResetKeyState();
            NotifyRecordingStateChanged();
            SaveConfig();
        }
    }

    // Primary Button Commands
    [RelayCommand]
    public void StartRecordingPrimary()
    {
        StartRecordingSlot(RecordingSlot.Primary);
    }

    [RelayCommand]
    public void CancelRecordingPrimary()
    {
        CancelRecording();
    }

    [RelayCommand]
    public void ToggleRecordingPrimary()
    {
        if (IsRecordingPrimary)
        {
            CancelRecording();
        }
        else
        {
            StartRecordingSlot(RecordingSlot.Primary);
        }
    }

    [RelayCommand]
    public void ResetPrimaryButton()
    {
        CancelRecording();
        PrimaryVkCode = Win32Api.VK_LBUTTON;
        PrimaryButtonName = "M1 (Left)";
        SelectedMouseButton = MouseButtonType.Left;
        _engine.PrimaryVkCode = Win32Api.VK_LBUTTON;
        NotifyRecordingStateChanged();
        SaveConfig();
    }

    [RelayCommand]
    public void SetPresetPrimaryButton(string? vkCodeStr)
    {
        if (int.TryParse(vkCodeStr, out int vkCode))
        {
            if (IsRunning)
            {
                _engine.Stop();
            }
            CancelRecording();
            PrimaryVkCode = vkCode;
            PrimaryButtonName = KeyHelper.GetKeyName(vkCode);
            if (vkCode is 1 or 2 or 4)
            {
                SelectedMouseButton = AutoClickerEngine.VkCodeToMouseButton(vkCode);
            }
            _engine.PrimaryVkCode = vkCode;
            NotifyRecordingStateChanged();
            SaveConfig();
        }
    }

    // Modifier / Secondary Button Commands
    [RelayCommand]
    public void StartRecordingModifier()
    {
        StartRecordingSlot(RecordingSlot.Modifier);
    }

    [RelayCommand]
    public void CancelRecordingModifier()
    {
        CancelRecording();
    }

    [RelayCommand]
    public void ToggleRecordingModifier()
    {
        if (IsRecordingModifier)
        {
            CancelRecording();
        }
        else
        {
            StartRecordingSlot(RecordingSlot.Modifier);
        }
    }

    [RelayCommand]
    public void ResetModifierButton()
    {
        CancelRecording();
        ModifierVkCode = Win32Api.VK_RBUTTON;
        ModifierButtonName = "M2 (Right)";
        SelectedModifierButton = MouseButtonType.Right;
        _engine.ModifierVkCode = Win32Api.VK_RBUTTON;
        NotifyRecordingStateChanged();
        OnPropertyChanged(nameof(ModifierStatusText));
        SaveConfig();
    }

    [RelayCommand]
    public void SetPresetModifierButton(string? vkCodeStr)
    {
        if (int.TryParse(vkCodeStr, out int vkCode))
        {
            if (IsRunning)
            {
                _engine.Stop();
            }
            CancelRecording();
            ModifierVkCode = vkCode;
            ModifierButtonName = KeyHelper.GetKeyName(vkCode);
            if (vkCode is 1 or 2 or 4)
            {
                SelectedModifierButton = AutoClickerEngine.VkCodeToMouseButton(vkCode);
            }
            _engine.ModifierVkCode = vkCode;
            NotifyRecordingStateChanged();
            OnPropertyChanged(nameof(ModifierStatusText));
            SaveConfig();
        }
    }

    [RelayCommand]
    public void ResetClickCount()
    {
        _engine.ResetClicks();
        TotalClicks = 0;
    }

    [RelayCommand]
    public void SetPresetInterval(string? msParam)
    {
        if (int.TryParse(msParam, out int ms))
        {
            IntervalMs = ms;
        }
    }

    [RelayCommand]
    public void ToggleSettings()
    {
        IsSettingsOpen = !IsSettingsOpen;
        if (IsSettingsOpen)
        {
            IsModifierPopupOpen = false;
        }
    }

    [RelayCommand]
    public void ToggleModifierPopup()
    {
        IsModifierPopupOpen = !IsModifierPopupOpen;
        if (IsModifierPopupOpen)
        {
            IsSettingsOpen = false;
        }
    }

    public void Dispose()
    {
        StopCountdownTimer();
        _engine.Dispose();
        _hook.Dispose();
        GC.SuppressFinalize(this);
    }
}
