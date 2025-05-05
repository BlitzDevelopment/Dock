using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Blitz.Themes;
using Blitz.ViewModels;
using Blitz.Views;
using System;
using System.Globalization;
using System.Resources;

namespace Blitz;

public static class Lang
{
    private static readonly ResourceManager ResourceManager = new ResourceManager("Blitz.Resources", typeof(Lang).Assembly);

    public static CultureInfo Culture { get; set; } = CultureInfo.CurrentCulture;

    public static string GetString(string key)
    {
        return ResourceManager.GetString(key, Culture) ?? string.Empty;
    }
}

public class App : Application
{
    public static IThemeManager? ThemeManager;

    private static AudioService? _audioService;
    public static AudioService AudioService => _audioService ??= new AudioService();

    private static EventAggregator? _eventAggregator;
    public static EventAggregator EventAggregator => _eventAggregator ??= new EventAggregator();

    private static IFileService? _fileService;
    public static IFileService FileService => _fileService ??= new FileService(((IClassicDesktopStyleApplicationLifetime)App.Current!.ApplicationLifetime!).MainWindow!);

    private static IGenericDialogs? _genericDialogs;
    public static IGenericDialogs GenericDialogs => _genericDialogs ??= new IGenericDialogs();

    private static IBlitzAppData? _blitzAppData;
    public static IBlitzAppData BlitzAppData => _blitzAppData ??= new BlitzAppData();

    public static MainWindowViewModel MainWindowViewModelInstance { get; } = new MainWindowViewModel();

    public override void Initialize()
    {
        ThemeManager = new FluentThemeManager();
        ThemeManager.Initialize(this);
        AudioService.InitializeOpenAL();

        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // DockManager.s_enableSplitToWindow = true;

        // Lang.Culture = new CultureInfo("EN");

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
