using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

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

        if (files.Count > 1)
        {
            throw new NotImplementedException("Multiple document opening not yet implemented");
        }

        var filePath = Uri.UnescapeDataString(files[0].Path.AbsolutePath);

        return filePath;
    }
}