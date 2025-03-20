using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Dock.Avalonia;
using Dock.Settings;
using Blitz.ViewModels;
using System;
using Avalonia.Interactivity;

namespace Blitz.Views;

public static class ApplicationServices
{
    public static MementoCaretaker MementoCaretaker => (MementoCaretaker)MementoCaretakerInstance.Instance;
}

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
        InitializeThemes();
        InitializeMenu();
        DataContext = new MainWindowViewModel();
    }

    private void OpenRecentMenuItem_Loaded(object sender, RoutedEventArgs e)
    {
        var _viewModelRegistry = ViewModelRegistry.Instance;
        var _mainWindowViewModel = _viewModelRegistry.GetViewModel(nameof(MainWindowViewModel)) as MainWindowViewModel;
        _mainWindowViewModel.OpenRecentMenuItem = (MenuItem)sender;
        _mainWindowViewModel.LoadRecentFiles(); // Load recent files here
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void InitializeThemes()
    {
        var dark = false;
        var theme = this.Find<Button>("ThemeButton");
        if (theme is { })
        {
            theme.Click += (_, _) =>
            {
                dark = !dark;
                App.ThemeManager?.Switch(dark ? 1 : 0);
            };
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        // Check for Control + Z (Undo)
        if (e.Key == Key.Z && (e.KeyModifiers & KeyModifiers.Control) == KeyModifiers.Control)
        {
            var memento = ApplicationServices.MementoCaretaker.Undo();
            if (memento != null)
            {
                Console.WriteLine($"Undo: {memento.Description}");
                memento.Restore();
            }
            e.Handled = true;
        }
        // Check for Control + Y (Redo)
        else if (e.Key == Key.Y && (e.KeyModifiers & KeyModifiers.Control) == KeyModifiers.Control)
        {
            var memento = ApplicationServices.MementoCaretaker.Redo();
            if (memento != null)
            {
                Console.WriteLine($"Redo: {memento.Description}");
                memento.Restore();
            }
            e.Handled = true;
        }
    }

    private void InitializeMenu()
    {
        var optionsIsDragEnabled = this.FindControl<MenuItem>("OptionsIsDragEnabled");
        if (optionsIsDragEnabled is { })
        {
            optionsIsDragEnabled.Click += (_, _) =>
            {
                if (VisualRoot is Window window)
                {
                    var isEnabled = window.GetValue(DockProperties.IsDragEnabledProperty);
                    window.SetValue(DockProperties.IsDragEnabledProperty, !isEnabled);
                }
            };
        }

        var optionsIsDropEnabled = this.FindControl<MenuItem>("OptionsIsDropEnabled");
        if (optionsIsDropEnabled is { })
        {
            optionsIsDropEnabled.Click += (_, _) =>
            {
                if (VisualRoot is Window window)
                {
                    var isEnabled = window.GetValue(DockProperties.IsDropEnabledProperty);
                    window.SetValue(DockProperties.IsDropEnabledProperty, !isEnabled);
                }
            };
        }
    }
}
