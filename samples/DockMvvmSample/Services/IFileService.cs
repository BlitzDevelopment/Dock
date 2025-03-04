using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using System.IO;
using System.Diagnostics;

public interface IFileService
{
    Task<string> OpenFileAsync(Window mainWindow);
}

public class FileService : IFileService
{
    public static FilePickerFileType BlitzCompatible { get; } = new("Blitz") { Patterns = new[] { "*.fla" }};

    private readonly Window _mainWindow;

    public FileService(Window mainWindow)
    {
        _mainWindow = mainWindow;
    }

    public async Task<string> OpenFileAsync(Window mainWindow)
    {
        var storage = Window.GetTopLevel(mainWindow)!.StorageProvider;

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Document",
            AllowMultiple = true,
            FileTypeFilter = new[] { BlitzCompatible }
        });

        if (files.Count == 0)
        {
            return null!;
        }

        if (files.Count > 1)
        {
            throw new NotImplementedException("Multiple document opening not yet implemented");
        }

        var filePath = Uri.UnescapeDataString(files[0].Path.AbsolutePath);

        if (!File.Exists(filePath))
        {
            Debug.WriteLine($"Warning: File {filePath} does not exist.");
            // You can also throw a custom exception here if needed
        }

        return filePath;
    }

    public async Task<string> ExportFileAsync(Window mainWindow, string defaultExt)
    {
        var storage = Window.GetTopLevel(mainWindow)!.StorageProvider;

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Media",
            DefaultExtension = defaultExt
        });

        if (file == null)
        {
            return null!;
        }

        var filePath = Uri.UnescapeDataString(file.Path.AbsolutePath);

        return filePath;
    }
}