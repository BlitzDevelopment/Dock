using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.Rendering;

namespace Blitz.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        Dispatcher.UIThread.Invoke(() =>
        {
            if (Application.Current!.ApplicationLifetime is IControlledApplicationLifetime lifetime)
            {
                Window.GetTopLevel(this)!.RendererDiagnostics.DebugOverlays = RendererDebugOverlays.RenderTimeGraph;
            }
        });
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
