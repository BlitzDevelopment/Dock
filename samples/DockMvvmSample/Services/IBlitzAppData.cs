using System.IO;
using System;
using System.Text.Json;
using System.Collections.Generic;
using Serilog;

public interface IBlitzAppData
{
    Dictionary<string, object> Preferences { get; }
    string GetTmpFolder();
    string GetRecentFilesPath();
    string GetLogFilePath();
    string GetPreferencesPath();
    void LoadPreferences();
    void SavePreferencesToDisk();
    bool TryGetNestedPreference<T>(string panelKey, string preferenceKey, string propertyKey, out T? propertyValue);
}

public class BlitzAppData : IBlitzAppData
{
    public Dictionary<string, object> Preferences { get; private set; } = new();
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

        return preferencesFilePath;
    }

    public void LoadPreferences()
    {
        string prefPath = GetPreferencesPath();
        if (File.Exists(prefPath))
        {
            try
            {
                string jsonContent = File.ReadAllText(prefPath);

                if (string.IsNullOrWhiteSpace(jsonContent))
                {
                    Log.Warning("Preferences file is empty. Restoring factory preferences.");
                    RestoreFactoryPreferences();
                }
                else
                {
                    var deserializedPreferences = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonContent);
                    if (deserializedPreferences != null)
                    {
                        Preferences = deserializedPreferences;
                    }
                    else
                    {
                        Log.Warning("Deserialization returned null. Restoring factory preferences.");
                        RestoreFactoryPreferences();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error loading preferences: {ex.Message}");
            }
        }
        else
        {
            Log.Warning("Preferences file not found. Restoring factory preferences.");
            RestoreFactoryPreferences();
        }
    }

    private void RestoreFactoryPreferences()
    {
        string prefPath = GetPreferencesPath();
        try
        {
            // Factory preferences with hierarchical structure
            var factoryPreferences = new Dictionary<string, object>
            {
                { "General", new Dictionary<string, object>
                    {
                        {
                            "Debug Overlays", new 
                            {
                                Value = "None",
                                Tags = new[] { "developer", "debugging", "fps", "render" },
                                UIType = "ComboBox",
                                Options = new[] { "None", "Fps", "DirtyRects", "LayoutTimeGraph", "RenderTimeGraph" },
                                Tooltip = "Display various information about application render performance."
                            }
                        },
                        {
                            "Language", new 
                            {
                                Value = "English",
                                Tags = new[] { "language" },
                                UIType = "ComboBox",
                                Options = new[] { "English", "Igpay Atinlay" },
                                Tooltip = "Change the application language."
                            }
                        },
                        {
                            "Skia GPU Resource Bytes", new 
                            {
                                Value = (256 * 1024 * 1024).ToString(),
                                Tags = new[] { "developer", "debugging" },
                                UIType = "TextBox",
                                Tooltip = "Requires application restart! How many bytes of GPU resources Skia should use. Default is 256MB."
                            }
                        },
                        {
                            "Notification Sounds", new 
                            {
                                Value = true,
                                Tags = new[] { "sound" },
                                UIType = "CheckBox",
                                Tooltip = "Enable or disable system notification sounds when an error or information modal appears."
                            }
                        },
                        {
                            "Theme", new 
                            {
                                Value = "Dark",
                                Tags = new[] { "appearance", "ui", "visual" },
                                UIType = "ComboBox",
                                Options = new[] { "Dark", "Light" },
                                Tooltip = "Choose the application theme."
                            }
                        },
                    }
                },
                { "Library", new Dictionary<string, object>
                    {
                        {
                            "exampleKey", new 
                            {
                                Value = true,
                                Tags = new[] { "tag1", "tag2" },
                                UIType = "CheckBox"
                            }
                        },
                    }
                },
                { "Document", new Dictionary<string, object>
                    {
                        {
                            "Show Progress Ring", new 
                            {
                                Value = true,
                                Tags = new[] { "appearance", "ui", "visual" },
                                UIType = "CheckBox",
                                Tooltip = "Show a progress ring during expensive operations."
                            }
                        },
                    }
                }
            };

            string jsonContent = JsonSerializer.Serialize(factoryPreferences, new JsonSerializerOptions { WriteIndented = true });

            File.WriteAllText(prefPath, jsonContent);

            Log.Information("Factory preferences restored.");
        }
        catch (Exception ex)
        {
            Log.Information($"Error restoring factory preferences: {ex.Message} {ex.StackTrace}");
        }
    }

    public void SavePreferencesToDisk()
    {
        try
        {
            string prefPath = GetPreferencesPath();
            string jsonContent = JsonSerializer.Serialize(Preferences, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(prefPath, jsonContent);
            Log.Information("Preferences saved to disk.");
        }
        catch (Exception ex)
        {
            Log.Error($"Error saving preferences to disk: {ex.Message}");
        }
    }

        public bool TryGetNestedPreference<T>(string panelKey, string preferenceKey, string propertyKey, out T? propertyValue)
    {
        propertyValue = default;

        if (Preferences.TryGetValue(panelKey, out var panelValue) &&
            panelValue is JsonElement panelJsonElement &&
            panelJsonElement.ValueKind == JsonValueKind.Object)
        {
            var panelPreferences = JsonSerializer.Deserialize<Dictionary<string, object>>(panelJsonElement.GetRawText());
            if (panelPreferences != null && panelPreferences.TryGetValue(preferenceKey, out var preferenceValue) &&
                preferenceValue is JsonElement preferenceJsonElement &&
                preferenceJsonElement.ValueKind == JsonValueKind.Object)
            {
                if (preferenceJsonElement.TryGetProperty(propertyKey, out var propertyJsonElement))
                {
                    try
                    {
                        if (typeof(T) == typeof(string) && propertyJsonElement.ValueKind == JsonValueKind.String)
                        {
                            propertyValue = (T)(object)propertyJsonElement.GetString();
                            return true;
                        }
                        if (typeof(T) == typeof(bool) && propertyJsonElement.ValueKind == JsonValueKind.True || propertyJsonElement.ValueKind == JsonValueKind.False)
                        {
                            propertyValue = (T)(object)propertyJsonElement.GetBoolean();
                            return true;
                        }
                        if (typeof(T) == typeof(int) && propertyJsonElement.ValueKind == JsonValueKind.Number)
                        {
                            propertyValue = (T)(object)propertyJsonElement.GetInt32();
                            return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error casting property value: {ex.Message}");
                    }
                }
            }
        }

        return false;
    }

}