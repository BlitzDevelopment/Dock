using System.IO;
using System;

public interface IBlitzAppData
{
    string GetTmpFolder();
    string GetRecentFilesPath();
}

public class BlitzAppData : IBlitzAppData
{
    string localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    public string GetTmpFolder()
    {
        string appDataFolder = Path.Combine(localAppDataPath, "Blitz", "tmp");

        if (!Directory.Exists(appDataFolder))
        {
            Directory.CreateDirectory(appDataFolder);
        }

        return appDataFolder;
    }

    public string GetRecentFilesPath()
    {
        string recentFilesPath = Path.Combine(localAppDataPath, "Blitz", "recentFiles.txt");

        if (!File.Exists(recentFilesPath))
        {
            File.Create(recentFilesPath).Dispose();
        }

        return recentFilesPath;
    }
}