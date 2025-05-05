using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Dock.Avalonia;
using Dock.Settings;
using Blitz.ViewModels;
using System;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Blitz.Views;

public static class ApplicationServices
{
    public static MementoCaretaker MementoCaretaker => ApplicationServices.MementoCaretaker;
}

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
        InitializeMenu();
        InitializeThemes();
        DataContext = App.MainWindowViewModelInstance;
    }

    private void OpenRecentMenuItem_Loaded(object sender, RoutedEventArgs e)
    {
        var _viewModelRegistry = ViewModelRegistry.Instance;
        var _mainWindowViewModel = _viewModelRegistry.GetViewModel(nameof(MainWindowViewModel)) as MainWindowViewModel;
        _mainWindowViewModel!.OpenRecentMenuItem = (MenuItem)sender;
        _mainWindowViewModel.LoadRecentFiles();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public static MementoCaretaker MementoCaretaker => ApplicationServices.MementoCaretaker;

    private void InitializeThemes()
    {
        var dark = false;
        dark = !dark;
        App.ThemeManager?.Switch(dark ? 1 : 0);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        // Check for Control + Z (Undo)
        if (e.Key == Key.Z && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            MementoCaretaker?.Undo();
            e.Handled = true;
        }
        // Check for Control + Y (Redo)
        else if (e.Key == Key.Y && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            MementoCaretaker?.Redo();
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

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            // Check if the source or its parent is a MenuItem or other interactive control
            var source = e.Source as Control;
            while (source != null)
            {
                if (source is MenuItem || source is Button || source is TextBox)
                {
                    return; // Do nothing if the click is on a MenuItem, Button, or TextBox
                }
                source = source.Parent as Control;
            }

            // Get the parent window of the UserControl
            if (VisualRoot is Window window)
            {
                window.BeginMoveDrag(e); // Call BeginMoveDrag on the parent window
            }
        }
    }

}