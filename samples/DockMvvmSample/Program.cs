using System;
using Avalonia;
using Microsoft.Extensions.Options;
using Serilog;

namespace Blitz;

internal class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        BlitzAppData appData = new BlitzAppData();
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(appData.GetLogFilePath(), rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
            .CreateLogger();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        Log.Information("Application ended gracefully.");
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .UseSkia()
            .With(new SkiaOptions { MaxGpuResourceSizeBytes = 256 * 1024 * 1024 }) // Adjust as needed
            .UsePlatformDetect()
            .LogToTrace();
            
}
