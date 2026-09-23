using System;
using Avalonia.Controls;
using AutoClicker.ViewModels;

namespace AutoClicker.Views;

public partial class MainWindow : Window
{
    private bool _isForceClose = false;

    public MainWindow()
    {
        InitializeComponent();
        Closing += OnClosing;
        Closed += OnClosed;
    }

    public void ForceClose()
    {
        _isForceClose = true;
        Close();
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_isForceClose)
        {
            return;
        }

        if (DataContext is MainViewModel vm && vm.CloseToTray)
        {
            e.Cancel = true;
            Hide();
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (DataContext is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}