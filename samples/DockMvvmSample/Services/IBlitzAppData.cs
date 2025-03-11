using System.IO;
using System;

public interface IBlitzAppData
{
    string GetTmpFolder();
}

public class BlitzAppData : IBlitzAppData
{
    public string GetTmpFolder()
    {
        string localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string appDataFolder = Path.Combine(localAppDataPath, "Blitz", "tmp");

        if (!Directory.Exists(appDataFolder))
        {
            Directory.CreateDirectory(appDataFolder);
        }

        return appDataFolder;
    }
}