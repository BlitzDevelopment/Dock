using System;
using Avalonia;
using Microsoft.Extensions.Options;
using Avalonia.Threading;
using Avalonia.Rendering;
using Serilog;
using System.IO;
using System.Linq;
using Avalonia.Controls.ApplicationLifetimes;

namespace Blitz;

internal class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        BlitzAppData appData = new BlitzAppData();
        string logDirectory = appData.GetLogFilePath();
        string logFileName = $"log-{DateTime.Now:yyyyMMdd_HHmmss}.txt";
        string logFilePath = Path.Combine(logDirectory, logFileName);

        // Configure Serilog
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(logFilePath)
            .CreateLogger();

        // Retain only the last 7 log files
        var logFiles = Directory.GetFiles(logDirectory, "log-*.txt")
            .OrderByDescending(File.GetCreationTime)
            .Skip(7);

        foreach (var file in logFiles)
        {
            File.Delete(file);
        }

        Log.Information("Starting Blitz...");
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        Log.Information("Application lifetime ended.");
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .UseSkia()
            .With(new SkiaOptions { MaxGpuResourceSizeBytes = 256 * 1024 * 1024 }) // Adjust as needed
            .UsePlatformDetect()
            .LogToTrace();
            
}
