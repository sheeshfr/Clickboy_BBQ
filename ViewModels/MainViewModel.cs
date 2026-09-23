using System;
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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusDescription))]
    [NotifyPropertyChangedFor(nameof(ActionToggleText))]
    [NotifyPropertyChangedFor(nameof(KeyButtonText))]
    private string _hotkeyName = "F6";

    [ObservableProperty]
    private int _hotkeyVkCode = 0x75; // F6

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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusDisplay))]
    [NotifyPropertyChangedFor(nameof(StatusDescription))]
    [NotifyPropertyChangedFor(nameof(ActionToggleText))]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    [NotifyPropertyChangedFor(nameof(IsActiveRunning))]
    private bool _isRunning = false;

    public bool IsIdle => !IsRunning && !IsRecordingHotkey;
    public bool IsActiveRunning => IsRunning && !IsRecordingHotkey;

    [ObservableProperty]
    private long _totalClicks = 0;

    public string StatusDisplay
    {
        get
        {
            if (IsRecordingHotkey) return "RECORDING";
            return IsRunning ? "CLICKING" : "IDLE";
        }
    }

    public string StatusDescription
    {
        get
        {
            if (IsRecordingHotkey) return "Press any key or mouse button (Mouse 4/5, Middle, etc.) • Esc cancels";
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

    public MainViewModel()
    {
        _engine = new AutoClickerEngine();
        _hook = new GlobalKeyboardHook();

        _hook.BlockHotkey = BlockHotkey;
        _hook.StopOnEscape = StopOnEscape;

        _engine.StateChanged += OnEngineStateChanged;
        _engine.ClickCountUpdated += OnClickCountUpdated;

        _hook.HotkeyDown += OnHotkeyDown;
        _hook.HotkeyUp += OnHotkeyUp;
        _hook.HotkeyRecorded += OnHotkeyRecorded;
        _hook.RecordingCancelled += OnRecordingCancelled;
        _hook.EmergencyStopTriggered += OnEmergencyStopTriggered;

        SyncSettingsToEngine();
    }

    partial void OnBlockHotkeyChanged(bool value)
    {
        _hook.BlockHotkey = value;
    }

    partial void OnStopOnEscapeChanged(bool value)
    {
        _hook.StopOnEscape = value;
    }

    private void OnEmergencyStopTriggered()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (IsRunning)
            {
                _engine.Stop();
                _hook.ResetKeyState();
            }
        });
    }

    partial void OnIntervalMsChanged(int value)
    {
        if (value < 1) IntervalMs = 1;
        _engine.IntervalMs = IntervalMs;
    }

    partial void OnSelectedMouseButtonChanged(MouseButtonType value)
    {
        _engine.MouseButton = value;
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
    }

    private void SyncSettingsToEngine()
    {
        _engine.IntervalMs = Math.Max(1, IntervalMs);
        _engine.MouseButton = SelectedMouseButton;
        _engine.ClickType = SelectedClickType;
    }

    private void OnHotkeyDown()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (IsRecordingHotkey) return;

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
        });
    }

    private void OnHotkeyUp()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (IsRecordingHotkey) return;

            if (SelectedTriggerMode == TriggerMode.Hold)
            {
                if (IsRunning)
                {
                    _engine.Stop();
                }
            }
        });
    }

    private void StartCountdownTimer()
    {
        StopCountdownTimer();
        CountdownSeconds = 5;
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
        if (!IsRecordingHotkey)
        {
            StopCountdownTimer();
            return;
        }

        if (CountdownSeconds > 0)
        {
            CountdownSeconds--;
        }
        else
        {
            StopCountdownTimer();
            CancelRecordingHotkey();
        }
    }

    private void OnHotkeyRecorded(int vkCode)
    {
        Dispatcher.UIThread.Post(() =>
        {
            StopCountdownTimer();
            HotkeyVkCode = vkCode;
            HotkeyName = KeyHelper.GetKeyName(vkCode);
            IsRecordingHotkey = false;
            OnPropertyChanged(nameof(StatusDescription));
        });
    }

    private void OnRecordingCancelled()
    {
        Dispatcher.UIThread.Post(() =>
        {
            StopCountdownTimer();
            IsRecordingHotkey = false;
            OnPropertyChanged(nameof(StatusDescription));
        });
    }

    private void OnEngineStateChanged(bool running)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsRunning = running;
        });
    }

    private void OnClickCountUpdated(long total)
    {
        // Throttled UI update or direct post
        Dispatcher.UIThread.Post(() =>
        {
            TotalClicks = total;
        });
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

    [RelayCommand]
    public void StartRecordingHotkey()
    {
        if (IsRunning)
        {
            _engine.Stop();
        }
        IsRecordingHotkey = true;
        _hook.IsRecording = true;
        StartCountdownTimer();
    }

    [RelayCommand]
    public void CancelRecordingHotkey()
    {
        StopCountdownTimer();
        IsRecordingHotkey = false;
        _hook.IsRecording = false;
        OnPropertyChanged(nameof(StatusDescription));
    }

    [RelayCommand]
    public void ToggleRecordingHotkey()
    {
        if (IsRecordingHotkey)
        {
            CancelRecordingHotkey();
        }
        else
        {
            StartRecordingHotkey();
        }
    }

    [RelayCommand]
    public void ResetHotkey()
    {
        StopCountdownTimer();
        HotkeyVkCode = 0x75; // F6
        _hook.HotkeyVkCode = 0x75;
        HotkeyName = "F6";
        IsRecordingHotkey = false;
        _hook.IsRecording = false;
        OnPropertyChanged(nameof(StatusDescription));
    }

    [RelayCommand]
    public void ResetClickCount()
    {
        _engine.ResetClicks();
        TotalClicks = 0;
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
            StopCountdownTimer();
            HotkeyVkCode = vkCode;
            _hook.HotkeyVkCode = vkCode;
            HotkeyName = KeyHelper.GetKeyName(vkCode);
            IsRecordingHotkey = false;
            _hook.IsRecording = false;
            _hook.ResetKeyState();
            OnPropertyChanged(nameof(StatusDescription));
        }
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
    }

    public void Dispose()
    {
        StopCountdownTimer();
        _engine.Dispose();
        _hook.Dispose();
        GC.SuppressFinalize(this);
    }
}
