using System;
using System.Diagnostics;
using System.Threading;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using AutoClicker.Models;
using AutoClicker.Services.Native;

namespace AutoClicker.Services;

public class AutoClickerEngine : IDisposable
{
    private Thread? _workerThread;
    private CancellationTokenSource? _cts;
    private readonly object _lock = new();
    private long _totalClicks = 0;

    public int IntervalMs { get; set; } = 100;
    public int PrimaryVkCode { get; set; } = Win32Api.VK_LBUTTON;
    public ClickType ClickType { get; set; } = ClickType.Single;

    public MouseButtonType MouseButton
    {
        get => VkCodeToMouseButton(PrimaryVkCode);
        set => PrimaryVkCode = MouseButtonToVkCode(value);
    }

    // Modifier (Secondary) Slot Options
    public bool IsModifierEnabled { get; set; } = false;
    public int ModifierVkCode { get; set; } = Win32Api.VK_RBUTTON;
    public ModifierAction ModifierAction { get; set; } = ModifierAction.Hold;
    public ModifierOrder ModifierOrder { get; set; } = ModifierOrder.ModifierFirst;
    public int ModifierDelayMs { get; set; } = 1;

    public MouseButtonType ModifierButton
    {
        get => VkCodeToMouseButton(ModifierVkCode);
        set => ModifierVkCode = MouseButtonToVkCode(value);
    }

    public static MouseButtonType VkCodeToMouseButton(int vkCode) => vkCode switch
    {
        Win32Api.VK_RBUTTON => MouseButtonType.Right,
        Win32Api.VK_MBUTTON => MouseButtonType.Middle,
        _ => MouseButtonType.Left
    };

    public static int MouseButtonToVkCode(MouseButtonType button) => button switch
    {
        MouseButtonType.Right => Win32Api.VK_RBUTTON,
        MouseButtonType.Middle => Win32Api.VK_MBUTTON,
        _ => Win32Api.VK_LBUTTON
    };

    public bool IsRunning { get; private set; }

    public event Action<bool>? StateChanged;
    public event Action<long>? ClickCountUpdated;

    public long TotalClicks => Interlocked.Read(ref _totalClicks);

    public void Start()
    {
        lock (_lock)
        {
            if (IsRunning) return;

            IsRunning = true;
            _cts = new CancellationTokenSource();

            _workerThread = new Thread(() => ClickLoop(_cts.Token))
            {
                IsBackground = true,
                Priority = ThreadPriority.Highest,
                Name = "AutoClickerWorker"
            };
            _workerThread.Start();

            StateChanged?.Invoke(true);
        }
    }

    public void Stop()
    {
        CancellationTokenSource? cts;

        lock (_lock)
        {
            if (!IsRunning) return;

            IsRunning = false;
            cts = _cts;
            _cts = null;
            _workerThread = null;
        }

        // 1. Cancel background worker immediately so it stops executing further clicks
        cts?.Cancel();

        // 2. Release buttons INSTANTLY on millisecond 0 - ZERO DELAY!
        ReleaseAllButtons();

        // 3. Notify state change
        StateChanged?.Invoke(false);
    }

    public void ResetClicks()
    {
        Interlocked.Exchange(ref _totalClicks, 0);
        ClickCountUpdated?.Invoke(0);
    }

    public static readonly UIntPtr INJECTED_SIGNATURE = (UIntPtr)0xC11C801;

    public static bool IsMouseButton(int vkCode)
    {
        return vkCode is Win32Api.VK_LBUTTON or Win32Api.VK_RBUTTON or Win32Api.VK_MBUTTON or Win32Api.VK_XBUTTON1 or Win32Api.VK_XBUTTON2;
    }

    public static void SendButtonUp(int vkCode)
    {
        if (IsMouseButton(vkCode))
        {
            uint flag = vkCode switch
            {
                Win32Api.VK_RBUTTON => Win32Api.MOUSEEVENTF_RIGHTUP,
                Win32Api.VK_MBUTTON => Win32Api.MOUSEEVENTF_MIDDLEUP,
                Win32Api.VK_XBUTTON1 or Win32Api.VK_XBUTTON2 => Win32Api.MOUSEEVENTF_XUP,
                _ => Win32Api.MOUSEEVENTF_LEFTUP
            };
            uint mouseData = vkCode == Win32Api.VK_XBUTTON2 ? 2u : (vkCode == Win32Api.VK_XBUTTON1 ? 1u : 0u);

            var input = new Win32Api.INPUT
            {
                type = Win32Api.INPUT_MOUSE,
                mi = new Win32Api.MOUSEINPUT
                {
                    mouseData = mouseData,
                    dwFlags = flag,
                    dwExtraInfo = INJECTED_SIGNATURE
                }
            };

            uint sent = Win32Api.SendInput(1, new[] { input }, Marshal.SizeOf<Win32Api.INPUT>());
            if (sent == 0)
            {
                Win32Api.mouse_event(flag, 0, 0, mouseData, INJECTED_SIGNATURE);
            }
        }
        else
        {
            var input = new Win32Api.INPUT
            {
                type = Win32Api.INPUT_KEYBOARD,
                ki = new Win32Api.KEYBDINPUT
                {
                    wVk = (ushort)vkCode,
                    wScan = 0,
                    dwFlags = Win32Api.KEYEVENTF_KEYUP,
                    dwExtraInfo = INJECTED_SIGNATURE
                }
            };

            uint sent = Win32Api.SendInput(1, new[] { input }, Marshal.SizeOf<Win32Api.INPUT>());
            if (sent == 0)
            {
                Win32Api.keybd_event((byte)vkCode, 0, Win32Api.KEYEVENTF_KEYUP, INJECTED_SIGNATURE);
            }
        }
    }

    private void SendButtonDown(int vkCode)
    {
        if (!IsRunning || _cts?.IsCancellationRequested == true) return;

        if (IsMouseButton(vkCode))
        {
            uint flag = vkCode switch
            {
                Win32Api.VK_RBUTTON => Win32Api.MOUSEEVENTF_RIGHTDOWN,
                Win32Api.VK_MBUTTON => Win32Api.MOUSEEVENTF_MIDDLEDOWN,
                Win32Api.VK_XBUTTON1 or Win32Api.VK_XBUTTON2 => Win32Api.MOUSEEVENTF_XDOWN,
                _ => Win32Api.MOUSEEVENTF_LEFTDOWN
            };
            uint mouseData = vkCode == Win32Api.VK_XBUTTON2 ? 2u : (vkCode == Win32Api.VK_XBUTTON1 ? 1u : 0u);

            var input = new Win32Api.INPUT
            {
                type = Win32Api.INPUT_MOUSE,
                mi = new Win32Api.MOUSEINPUT
                {
                    mouseData = mouseData,
                    dwFlags = flag,
                    dwExtraInfo = INJECTED_SIGNATURE
                }
            };

            uint sent = Win32Api.SendInput(1, new[] { input }, Marshal.SizeOf<Win32Api.INPUT>());
            if (sent == 0)
            {
                Win32Api.mouse_event(flag, 0, 0, mouseData, INJECTED_SIGNATURE);
            }
        }
        else
        {
            var input = new Win32Api.INPUT
            {
                type = Win32Api.INPUT_KEYBOARD,
                ki = new Win32Api.KEYBDINPUT
                {
                    wVk = (ushort)vkCode,
                    wScan = 0,
                    dwFlags = Win32Api.KEYEVENTF_KEYDOWN,
                    dwExtraInfo = INJECTED_SIGNATURE
                }
            };

            uint sent = Win32Api.SendInput(1, new[] { input }, Marshal.SizeOf<Win32Api.INPUT>());
            if (sent == 0)
            {
                Win32Api.keybd_event((byte)vkCode, 0, Win32Api.KEYEVENTF_KEYDOWN, INJECTED_SIGNATURE);
            }
        }
    }

    public void ReleaseAllButtons()
    {
        try
        {
            // 1. Release Primary button first (e.g. Left Click fire)
            SendButtonUp(PrimaryVkCode);

            // 2. Release Modifier button instantly (e.g. Right Click ADS)
            if (IsModifierEnabled)
            {
                SendButtonUp(ModifierVkCode);
            }
        }
        catch
        {
            // Ignore during disposal/cleanup
        }
    }

    private void ExecuteClick(int vkCode, CancellationToken token)
    {
        if (!IsRunning || token.IsCancellationRequested) return;

        // Games like Remnant 2 (Unreal Engine 5) poll input on frame ticks (~16.6ms at 60 FPS).
        // 0ms hold times cause clicks to be dropped or missed by weapon triggers.
        // Maintain a realistic hold duration while preserving total interval timing.
        int holdDurationMs = IntervalMs < 30 ? Math.Max(1, IntervalMs / 2) : Math.Clamp(IntervalMs / 4, 15, 30);

        SendButtonDown(vkCode);
        
        if (holdDurationMs > 0 && !token.IsCancellationRequested && IsRunning)
        {
            token.WaitHandle.WaitOne(holdDurationMs);
        }

        SendButtonUp(vkCode);

        if (ClickType == ClickType.Double && !token.IsCancellationRequested && IsRunning)
        {
            token.WaitHandle.WaitOne(10);
            if (!IsRunning || token.IsCancellationRequested) return;
            SendButtonDown(vkCode);
            if (holdDurationMs > 0 && !token.IsCancellationRequested && IsRunning)
            {
                token.WaitHandle.WaitOne(holdDurationMs);
            }
            SendButtonUp(vkCode);
        }
    }

    private void ClickLoop(CancellationToken token)
    {
        Win32Api.TimeBeginPeriod(1);
        var sw = Stopwatch.StartNew();

        try
        {
            bool holdModifier = IsModifierEnabled && ModifierAction == ModifierAction.Hold;
            int initialDelay = Math.Max(1, ModifierDelayMs);

            if (holdModifier)
            {
                if (ModifierOrder == ModifierOrder.ModifierFirst)
                {
                    // User's case: hold secondary (e.g. Right Click ADS), wait delay, then spam primary
                    SendButtonDown(ModifierVkCode);
                    if (token.WaitHandle.WaitOne(initialDelay) || !IsRunning || token.IsCancellationRequested)
                        return;
                }
                else
                {
                    // Primary first, then hold secondary
                    SendButtonDown(PrimaryVkCode);
                    if (token.WaitHandle.WaitOne(initialDelay) || !IsRunning || token.IsCancellationRequested)
                        return;
                    SendButtonDown(ModifierVkCode);
                }
            }

            while (IsRunning && !token.IsCancellationRequested)
            {
                long targetMs = Math.Max(1, IntervalMs);
                long clickStartTime = sw.ElapsedMilliseconds;

                if (IsModifierEnabled && ModifierAction == ModifierAction.Spam)
                {
                    if (ModifierOrder == ModifierOrder.ModifierFirst)
                    {
                        ExecuteClick(ModifierVkCode, token);
                        if (ModifierDelayMs > 0 && !token.IsCancellationRequested && IsRunning)
                        {
                            token.WaitHandle.WaitOne(ModifierDelayMs);
                        }
                        ExecuteClick(PrimaryVkCode, token);
                    }
                    else
                    {
                        ExecuteClick(PrimaryVkCode, token);
                        if (ModifierDelayMs > 0 && !token.IsCancellationRequested && IsRunning)
                        {
                            token.WaitHandle.WaitOne(ModifierDelayMs);
                        }
                        ExecuteClick(ModifierVkCode, token);
                    }
                }
                else
                {
                    ExecuteClick(PrimaryVkCode, token);
                }

                if (!IsRunning || token.IsCancellationRequested)
                    break;

                long currentCount = Interlocked.Increment(ref _totalClicks);
                ClickCountUpdated?.Invoke(currentCount);

                // Accurate sleep / wait until next click interval
                while (IsRunning && !token.IsCancellationRequested)
                {
                    long elapsed = sw.ElapsedMilliseconds - clickStartTime;
                    long remaining = targetMs - elapsed;
                    if (remaining <= 0)
                        break;

                    if (remaining > 10)
                    {
                        int chunk = (int)Math.Min(remaining - 5, 50);
                        if (token.WaitHandle.WaitOne(chunk))
                            break;
                    }
                    else if (remaining > 2)
                    {
                        Thread.Sleep(1);
                    }
                    else
                    {
                        Thread.SpinWait(40);
                    }
                }
            }
        }
        finally
        {
            ReleaseAllButtons();
            Win32Api.TimeEndPeriod(1);
        }
    }

    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
        GC.SuppressFinalize(this);
    }
}
