using System;
using System.Diagnostics;
using System.Threading;
using AutoClicker.Models;
using AutoClicker.Services;
using AutoClicker.Services.Native;
using AutoClicker.ViewModels;
using Xunit;

namespace AutoClicker.Tests;

public class KeyHelperTests
{
    [Theory]
    [InlineData(0x75, "F6")]
    [InlineData(0x70, "F1")]
    [InlineData(0x7B, "F12")]
    [InlineData(0x1B, "Escape")]
    [InlineData(0x20, "Space")]
    [InlineData(0x41, "A")]
    [InlineData(0x5A, "Z")]
    [InlineData(0x30, "0")]
    [InlineData(0x01, "M1 (Left)")]
    [InlineData(0x02, "M2 (Right)")]
    [InlineData(0x04, "M3 (Middle)")]
    [InlineData(0x05, "M4")]
    [InlineData(0x06, "M5")]
    [InlineData(0xB3, "Play / Pause")]
    public void GetKeyName_MapsCorrectly(int vkCode, string expectedName)
    {
        string actual = KeyHelper.GetKeyName(vkCode);
        Assert.Equal(expectedName, actual);
    }
}

public class AutoClickerEngineTests
{
    [Fact]
    public void Engine_StartsAndIncrementsClicks()
    {
        using var engine = new AutoClickerEngine();
        engine.IntervalMs = 20;

        Assert.False(engine.IsRunning);
        engine.Start();
        Assert.True(engine.IsRunning);

        Thread.Sleep(120);

        engine.Stop();
        Assert.False(engine.IsRunning);

        long clicks = engine.TotalClicks;
        Assert.True(clicks >= 2, $"Expected at least 2 clicks, got {clicks}");

        engine.ResetClicks();
        Assert.Equal(0, engine.TotalClicks);
    }

    [Fact]
    public void Engine_StopsImmediatelyEvenWithLargeInterval()
    {
        using var engine = new AutoClickerEngine();
        engine.IntervalMs = 5000; // 5 seconds interval

        engine.Start();
        Thread.Sleep(50); // let worker thread start and enter wait

        var sw = Stopwatch.StartNew();
        engine.Stop();
        sw.Stop();

        Assert.False(engine.IsRunning);
        Assert.True(sw.ElapsedMilliseconds < 150, $"Stop took too long: {sw.ElapsedMilliseconds}ms");
    }
}

public class MainViewModelTests
{
    [Fact]
    public void ViewModel_PresetAndCpsCalculation()
    {
        // Avoid initializing hook on CI/test if possible, or verify ViewModel properties
        var vm = new MainViewModel();

        // Verify defaults: Hold mode, 50ms, CloseToTray true
        Assert.Equal(50, vm.IntervalMs);
        Assert.Equal(TriggerMode.Hold, vm.SelectedTriggerMode);
        Assert.True(vm.IsHoldMode);
        Assert.False(vm.IsToggleMode);
        Assert.True(vm.CloseToTray);
        vm.CloseToTray = false;
        Assert.False(vm.CloseToTray);
        vm.CloseToTray = true;
        Assert.True(vm.CloseToTray);

        vm.IntervalMs = 100;
        vm.SelectedClickType = ClickType.Single;
        Assert.Equal("10.0 CPS", vm.EstimatedCpsText);

        vm.SelectedClickType = ClickType.Double;
        Assert.Equal("20.0 CPS", vm.EstimatedCpsText);

        vm.SetPresetIntervalCommand.Execute("50");
        Assert.Equal(50, vm.IntervalMs);

        // Test mode switches
        vm.IsHoldMode = true;
        Assert.Equal(TriggerMode.Hold, vm.SelectedTriggerMode);
        Assert.True(vm.IsHoldMode);
        Assert.False(vm.IsToggleMode);

        vm.IsToggleMode = true;
        Assert.Equal(TriggerMode.Toggle, vm.SelectedTriggerMode);
        Assert.True(vm.IsToggleMode);
        Assert.False(vm.IsHoldMode);

        // Test mouse hotkey preset
        vm.SetPresetHotkeyCommand.Execute("5"); // Mouse 4
        Assert.Equal(5, vm.HotkeyVkCode);
        Assert.Equal("M4", vm.HotkeyName);
        Assert.Equal("M4", vm.KeyButtonText);

        vm.SetPresetHotkeyCommand.Execute("6"); // Mouse 5
        Assert.Equal(6, vm.HotkeyVkCode);
        Assert.Equal("M5", vm.HotkeyName);
        Assert.Equal("M5", vm.KeyButtonText);

        // Test hotkey button toggle & countdown
        Assert.False(vm.IsRecordingHotkey);
        vm.ToggleRecordingHotkeyCommand.Execute(null);
        Assert.True(vm.IsRecordingHotkey);
        Assert.Equal(5, vm.CountdownSeconds);
        Assert.Equal("waiting...5", vm.KeyButtonText);

        // Cancel via toggle
        vm.ToggleRecordingHotkeyCommand.Execute(null);
        Assert.False(vm.IsRecordingHotkey);
        Assert.Equal("M5", vm.KeyButtonText);

        // Reset hotkey
        vm.ResetHotkeyCommand.Execute(null);
        Assert.Equal(0x75, vm.HotkeyVkCode);
        Assert.Equal("F6", vm.HotkeyName);
        Assert.Equal("F6", vm.KeyButtonText);

        // Test settings popup toggle
        Assert.False(vm.IsSettingsOpen);
        vm.ToggleSettingsCommand.Execute(null);
        Assert.True(vm.IsSettingsOpen);
        vm.ToggleSettingsCommand.Execute(null);
        Assert.False(vm.IsSettingsOpen);

        vm.Dispose();
    }
}