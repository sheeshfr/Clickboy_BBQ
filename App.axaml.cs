using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AutoClicker.ViewModels;
using AutoClicker.Views;

namespace AutoClicker;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var vm = new MainViewModel();
            var mainWindow = new MainWindow
            {
                DataContext = vm,
            };

            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void TrayIcon_Clicked(object? sender, EventArgs e)
    {
        ShowMainWindow();
    }

    private void MenuOpen_Click(object? sender, EventArgs e)
    {
        ShowMainWindow();
    }

    private void MenuExit_Click(object? sender, EventArgs e)
    {
        ExitApplication();
    }

    private void ShowMainWindow()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow is Window window)
        {
            window.Show();
            if (window.WindowState == Avalonia.Controls.WindowState.Minimized)
            {
                window.WindowState = Avalonia.Controls.WindowState.Normal;
            }
            window.Activate();
        }
    }

    private void ExitApplication()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (desktop.MainWindow is MainWindow mainWindow)
            {
                mainWindow.ForceClose();
            }
            desktop.Shutdown();
        }
    }
}