﻿using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Blitz.Themes;
using Blitz.ViewModels;
using Blitz.Views;
using System;

namespace Blitz;

public class App : Application
{
    public static IThemeManager? ThemeManager;
    public static AudioService? AudioService;
    public static MainWindowViewModel MainWindowViewModelInstance { get; } = new MainWindowViewModel();

    public override void Initialize()
    {
        ThemeManager = new FluentThemeManager();
        ThemeManager.Initialize(this);

        AudioService = new AudioService();
        AudioService.InitializeOpenAL();

        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // DockManager.s_enableSplitToWindow = true;

        switch (ApplicationLifetime)
        {
            case IClassicDesktopStyleApplicationLifetime desktopLifetime:
            {
                var mainWindow = new MainWindow
                {
                    DataContext = MainWindowViewModelInstance // Use the shared instance
                };

                mainWindow.Closing += (_, _) =>
                {
                    MainWindowViewModelInstance.CloseLayout(); // Use the shared instance
                };

                desktopLifetime.MainWindow = mainWindow;

                desktopLifetime.Exit += (_, _) =>
                {
                    MainWindowViewModelInstance.CloseLayout(); // Use the shared instance
                };

                break;
            }
            case ISingleViewApplicationLifetime singleViewLifetime:
            {
                var mainView = new MainView()
                {
                    DataContext = MainWindowViewModelInstance // Use the shared instance
                };

                singleViewLifetime.MainView = mainView;

                break;
            }
        }

        base.OnFrameworkInitializationCompleted();
#if DEBUG
        this.AttachDevTools();
#endif
    }
}
