using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.Rendering;
using Blitz.Events;
using System.Collections.Generic;
using System.Text.Json;
using Serilog;
using System;

namespace Blitz.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        App.EventAggregator.Subscribe<ApplicationPreferencesChangedEvent>(OnPreferencesChanged);

        InitializeComponent();

        // Set the loaded theme for the application, no need for logic to update theme here, that's in MainPreferences panel
        App.BlitzAppData.TryGetNestedPreference<string>("General", "Theme", "Value", out var themeValue);
        var isDarkTheme = themeValue?.Equals("Dark", StringComparison.OrdinalIgnoreCase) ?? false;
        App.ThemeManager?.Switch(!isDarkTheme ? 1 : 0);
    }

    public enum DebugOverlays
    {
        None,
        Fps,
        DirtyRects,
        LayoutTimeGraph,
        RenderTimeGraph
    }

    // Todo, IBlitzAppData has a function that should do this easier but I've got so much shit to do
    private void OnPreferencesChanged(ApplicationPreferencesChangedEvent obj)
    {
        Console.WriteLine("Preferences changed event received.");
        if (obj.Preferences.TryGetValue("General", out var generalPreferences) &&
            generalPreferences is JsonElement generalJsonElement &&
            generalJsonElement.ValueKind == JsonValueKind.Object)
        {
            var generalDict = JsonSerializer.Deserialize<Dictionary<string, object>>(generalJsonElement.GetRawText());
            if (generalDict != null && generalDict.TryGetValue("Debug Overlays", out var debugOverlayPreference) &&
                debugOverlayPreference is JsonElement debugOverlayJsonElement &&
                debugOverlayJsonElement.ValueKind == JsonValueKind.Object)
            {
                var value = debugOverlayJsonElement.GetProperty("Value").GetString();

                if (Enum.TryParse<DebugOverlays>(value, out var debugOverlayEnum))
                {
                    Dispatcher.UIThread.Invoke(() =>
                    {
                        if (Application.Current!.ApplicationLifetime is IControlledApplicationLifetime lifetime)
                        {
                            Window.GetTopLevel(this)!.RendererDiagnostics.DebugOverlays = (RendererDebugOverlays)debugOverlayEnum;
                        }
                    });
                }
                else
                {
                    Log.Error("Failed to parse Debug Overlays enum.");
                }
            }
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
