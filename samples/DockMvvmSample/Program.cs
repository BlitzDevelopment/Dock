using System;
using Avalonia;
using Microsoft.Extensions.Options;
using Serilog;
using System.IO;

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
            .WriteTo.File(
                Path.Combine(appData.GetLogFilePath(), $"log-{DateTime.Now:yyyyMMdd_HHmmss}.txt"), // Unique log file per session
                rollingInterval: RollingInterval.Infinite, // No rolling within a session
                retainedFileCountLimit: 7 // Keep only the last 7 session logs
            )
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
