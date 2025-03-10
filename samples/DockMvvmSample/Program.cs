using System;
using Avalonia;
using Microsoft.Extensions.Options;

namespace Blitz;

internal class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .UseSkia()
            .UsePlatformDetect()
            .LogToTrace();
}
