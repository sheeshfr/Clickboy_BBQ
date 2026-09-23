using System;
using System.Diagnostics;
using System.Threading;
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
    public MouseButtonType MouseButton { get; set; } = MouseButtonType.Left;
    public ClickType ClickType { get; set; } = ClickType.Single;

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
        lock (_lock)
        {
            if (!IsRunning) return;

            IsRunning = false;
            _cts?.Cancel();
            _workerThread = null;

            StateChanged?.Invoke(false);
        }
    }

    public void ResetClicks()
    {
        Interlocked.Exchange(ref _totalClicks, 0);
        ClickCountUpdated?.Invoke(0);
    }

    private void ClickLoop(CancellationToken token)
    {
        Win32Api.TimeBeginPeriod(1);
        var sw = Stopwatch.StartNew();

        try
        {
            while (!token.IsCancellationRequested)
            {
                long targetMs = Math.Max(1, IntervalMs);
                long clickStartTime = sw.ElapsedMilliseconds;

                ExecuteClick();
                long currentCount = Interlocked.Increment(ref _totalClicks);
                ClickCountUpdated?.Invoke(currentCount);

                if (token.IsCancellationRequested)
                    break;

                // Accurate sleep / wait until next click interval
                while (!token.IsCancellationRequested)
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
            Win32Api.TimeEndPeriod(1);
        }
    }

    public static readonly UIntPtr INJECTED_SIGNATURE = (UIntPtr)0xC11C801;

    private void ExecuteClick()
    {
        uint downFlag = MouseButton switch
        {
            MouseButtonType.Right => Win32Api.MOUSEEVENTF_RIGHTDOWN,
            MouseButtonType.Middle => Win32Api.MOUSEEVENTF_MIDDLEDOWN,
            _ => Win32Api.MOUSEEVENTF_LEFTDOWN
        };

        uint upFlag = MouseButton switch
        {
            MouseButtonType.Right => Win32Api.MOUSEEVENTF_RIGHTUP,
            MouseButtonType.Middle => Win32Api.MOUSEEVENTF_MIDDLEUP,
            _ => Win32Api.MOUSEEVENTF_LEFTUP
        };

        Win32Api.mouse_event(downFlag, 0, 0, 0, INJECTED_SIGNATURE);
        Win32Api.mouse_event(upFlag, 0, 0, 0, INJECTED_SIGNATURE);

        if (ClickType == ClickType.Double)
        {
            Thread.Sleep(10);
            Win32Api.mouse_event(downFlag, 0, 0, 0, INJECTED_SIGNATURE);
            Win32Api.mouse_event(upFlag, 0, 0, 0, INJECTED_SIGNATURE);
        }
    }

    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
        GC.SuppressFinalize(this);
    }
}
