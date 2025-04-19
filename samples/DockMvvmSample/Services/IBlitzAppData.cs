using System.IO;
using System;
using System.Text.Json;

public interface IBlitzAppData
{
    string GetTmpFolder();
    string GetRecentFilesPath();
}

public class BlitzAppData : IBlitzAppData
{
    string localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    string roamingAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

    public string GetTmpFolder()
    {
        string appDataFolder = Path.Combine(localAppDataPath, "Blitz", "tmp");

        if (!Directory.Exists(appDataFolder))
        {
            Directory.CreateDirectory(appDataFolder);
        }

        return appDataFolder;
    }

    public string GetLogFilePath()
    {
        string logFilePath = Path.Combine(localAppDataPath, "Blitz", "logs");

        string directoryPath = Path.Combine(localAppDataPath, "Blitz");
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);  // Create the directory if it doesn't exist
        }

        return logFilePath;
    }

    public string GetRecentFilesPath()
    {
        string recentFilesPath = Path.Combine(localAppDataPath, "Blitz", "recentFiles.txt");

        string directoryPath = Path.Combine(localAppDataPath, "Blitz");
        if (!Directory.Exists(directoryPath)) 
        {
            Directory.CreateDirectory(directoryPath);  // Create the directory if it doesn't exist
        }

        // Now you can safely create the file
        if (!File.Exists(recentFilesPath))
        {
            File.Create(recentFilesPath).Dispose();
        }

        return recentFilesPath;
    }

    public string GetPreferencesPath()
    {
        string preferencesFilePath = Path.Combine(roamingAppDataPath, "Blitz", "preferences.json");

        // Ensure the directory exists
        string directoryPath = Path.Combine(roamingAppDataPath, "Blitz");
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        // Ensure the file exists
        if (!File.Exists(preferencesFilePath))
        {
            // Create an empty JSON file
            File.WriteAllText(preferencesFilePath, "{}");
        }

        return preferencesFilePath;
    }
}