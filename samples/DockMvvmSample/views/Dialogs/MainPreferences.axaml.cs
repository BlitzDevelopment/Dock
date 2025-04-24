using Avalonia.Controls;
using Avalonia.Interactivity;
using DialogHostAvalonia;
using Avalonia.Markup.Xaml;
using Avalonia;
using Avalonia.Layout;
using System.IO;
using System.Text.Json;
using System;
using System.Collections.Generic;
using Serilog;
using System.Linq;
using Microsoft.Extensions.Options;
using Blitz.Events;

namespace Blitz.Views
{
    public partial class MainPreferences : UserControl
    {
        private readonly EventAggregator _eventAggregator;
        private readonly BlitzAppData _blitzAppData = new();
        private Dictionary<string, object> _preferences = new();
        private string _searchText = string.Empty;
        public string? DialogIdentifier { get; set; }

        public MainPreferences()
        {
            AvaloniaXamlLoader.Load(this);
            _eventAggregator = EventAggregator.Instance;
            
            string prefPath = _blitzAppData.GetPreferencesPath();

            if (File.Exists(prefPath))
            {
                try
                {
                    string jsonContent = File.ReadAllText(prefPath);

                    if (string.IsNullOrWhiteSpace(jsonContent))
                    {
                        Log.Warning("Preferences file is empty. Restoring factory preferences.");
                        RestoreFactoryPreferences(prefPath);
                    }
                    else
                    {
                        _preferences = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonContent);
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
                RestoreFactoryPreferences(prefPath);
            }

            RestoreFactoryPreferences(prefPath);
            BuildTabs();
            BuildContent();

            DevOps.PropertyChanged += DevOps_PropertyChanged;
            Search.PropertyChanged += Search_PropertyChanged;
        }

        private void DevOps_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property.Name == nameof(CheckBox.IsChecked))
            {
                BuildContent(null, _searchText);
            }
        }

        private void Search_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            _searchText = Search.Text?.ToLower() ?? string.Empty;
            if (e.Property.Name == nameof(TextBox.Text))
            {
                BuildContent(null, _searchText);
            }
        }

        private void PrintPreferences()
        {
            foreach (var panel in _preferences)
            {
                string panelName = panel.Key;
                Console.WriteLine($"Panel: {panelName}");

                if (panel.Value is JsonElement panelJsonElement && 
                    panelJsonElement.ValueKind == JsonValueKind.Object)
                {
                    var panelPreferences = JsonSerializer.Deserialize<Dictionary<string, object>>(panelJsonElement.GetRawText());
                    if (panelPreferences != null)
                    {
                        Log.Information($"PreferenceGroup: {panelName}");
                        foreach (var kvp in panelPreferences)
                        {
                            string key = kvp.Key;
                            var valueObject = kvp.Value;

                            // Extract Value and Tags dynamically
                            if (valueObject is JsonElement valueJsonElement && 
                                valueJsonElement.ValueKind == JsonValueKind.Object)
                            {
                                var valueProperty = valueJsonElement.GetProperty("Value").ToString();
                                var tagsProperty = valueJsonElement.TryGetProperty("Tags", out var tagsJsonElement) && 
                                                tagsJsonElement.ValueKind == JsonValueKind.Array
                                    ? JsonSerializer.Deserialize<string[]>(tagsJsonElement.GetRawText())
                                    : Array.Empty<string>();

                                Log.Information($"  Key: {key}");
                                Log.Information($"    Value: {valueProperty}");
                                Log.Information($"    Tags: {string.Join(", ", tagsProperty)}");
                            }
                        }
                    }
                }
            }
        }

        private void RestoreFactoryPreferences(string prefPath)
        {
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

        private void BuildTabs()
        {
            foreach (var panel in _preferences)
            {
                var button = new Button
                {
                    Content = panel.Key,
                    FontWeight = Avalonia.Media.FontWeight.Bold,
                    Height = 50,
                    VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                };

                // Attach a click event to load the content for the selected tab
                button.Click += (sender, e) => BuildContent(panel.Key);

                // Add the button to the StackPanel
                PreferencesTabs.Children.Add(button);
            }
        }

        private void BuildContent(string? panelKey = null, string? search = null)
        {
            // Clear existing content
            PreferencesContent.Children.Clear();

            if (!string.IsNullOrWhiteSpace(search))
            {
                BuildContentWithSearch(search);
                return;
            }

            BuildContentForPanel(panelKey);
        }

        private void BuildContentWithSearch(string search)
        {
            string lowerSearch = search.ToLower();
            bool isDevOpsChecked = DevOps.IsChecked == true;

            foreach (var panel in _preferences)
            {
                if (panel.Value is not JsonElement outerPanelJsonElement || outerPanelJsonElement.ValueKind != JsonValueKind.Object) continue;

                var panelPreferences = JsonSerializer.Deserialize<Dictionary<string, object>>(outerPanelJsonElement.GetRawText());
                if (panelPreferences == null) continue;

                foreach (var preference in panelPreferences)
                {
                    if (preference.Value is not JsonElement innerValueJsonElement || innerValueJsonElement.ValueKind != JsonValueKind.Object) continue;

                    var tagsProperty = GetTags(innerValueJsonElement);

                    // Skip preferences with the "developer" tag if DevOps is disabled
                    if (!isDevOpsChecked && tagsProperty.Any(tag => tag.Equals("developer", StringComparison.OrdinalIgnoreCase))) continue;

                    // Apply search filter
                    if (!preference.Key.ToLower().Contains(lowerSearch) &&
                        !tagsProperty.Any(tag => tag.ToLower().Contains(lowerSearch))) continue;

                    AddPreferenceContent(preference.Key, innerValueJsonElement);
                }
            }
        }

        private void BuildContentForPanel(string? panelKey)
        {
            // Use the first key in _preferences if no panelKey is provided
            panelKey ??= _preferences.Keys.FirstOrDefault();
            if (panelKey == null || !_preferences.TryGetValue(panelKey, out var panelValue)) return;

            if (panelValue is not JsonElement outerPanelJsonElementForKey || outerPanelJsonElementForKey.ValueKind != JsonValueKind.Object) return;

            var panelPreferencesForKey = JsonSerializer.Deserialize<Dictionary<string, object>>(outerPanelJsonElementForKey.GetRawText());
            if (panelPreferencesForKey == null) return;

            foreach (var preference in panelPreferencesForKey)
            {
                if (preference.Value is not JsonElement innerValueJsonElementForKey || innerValueJsonElementForKey.ValueKind != JsonValueKind.Object) continue;

                var tagsProperty = GetTags(innerValueJsonElementForKey);

                bool isDevOpsChecked = DevOps.IsChecked == true;

                // Skip preferences with the "Developer" tag if DevOps is disabled
                if (!isDevOpsChecked && tagsProperty.Any(tag => tag.Equals("developer", StringComparison.OrdinalIgnoreCase))) continue;

                AddPreferenceContent(preference.Key, innerValueJsonElementForKey);
            }
        }

        private string[] GetTags(JsonElement jsonElement)
        {
            return jsonElement.TryGetProperty("Tags", out var tagsJsonElement) && tagsJsonElement.ValueKind == JsonValueKind.Array
                ? JsonSerializer.Deserialize<string[]>(tagsJsonElement.GetRawText())
                : Array.Empty<string>();
        }

        private void AddPreferenceContent(string key, JsonElement preference)
        {
            if (preference.ValueKind != JsonValueKind.Object) return;

            // Extract properties from the JSON element
            var value = preference.GetProperty("Value").ToString();
            var uiType = preference.TryGetProperty("UIType", out var uiTypeElement) ? uiTypeElement.GetString() : null;
            var options = preference.TryGetProperty("Options", out var optionsElement) && optionsElement.ValueKind == JsonValueKind.Array
                ? JsonSerializer.Deserialize<string[]>(optionsElement.GetRawText())
                : Array.Empty<string>();
            var tooltip = preference.TryGetProperty("Tooltip", out var tooltipElement) ? tooltipElement.GetString() : null;

            // Create UI elements based on the UIType
            Control control = uiType switch
            {
                "CheckBox" => new CheckBox
                {
                    IsChecked = bool.TryParse(value, out var isChecked) && isChecked,
                    Margin = new Thickness(5),
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    [ToolTip.TipProperty] = tooltip // Set tooltip if available
                },
                "ComboBox" => new ComboBox
                {
                    ItemsSource = options,
                    SelectedItem = value,
                    Margin = new Thickness(5),
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    [ToolTip.TipProperty] = tooltip // Set tooltip if available
                },
                "TextBox" => new TextBox
                {
                    Text = value,
                    Margin = new Thickness(5),
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    [ToolTip.TipProperty] = tooltip // Set tooltip if available
                },
                _ => new TextBlock
                {
                    Text = value,
                    FontWeight = Avalonia.Media.FontWeight.Bold,
                    FontSize = 16,
                    Margin = new Thickness(5),
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    [ToolTip.TipProperty] = tooltip // Set tooltip if available
                }
            };

            // Attach event handlers to update preferences when the control value changes
            switch (control)
            {
                case CheckBox checkBox:
                    checkBox.Checked += (s, e) => UpdatePreference(key, true);
                    checkBox.Unchecked += (s, e) => UpdatePreference(key, false);
                    break;
                case ComboBox comboBox:
                    comboBox.SelectionChanged += (s, e) => UpdatePreference(key, comboBox.SelectedItem?.ToString());
                    break;
                case TextBox textBox:
                    textBox.PropertyChanged += (s, e) =>
                    {
                        if (e.Property.Name == nameof(TextBox.Text))
                        {
                            UpdatePreference(key, textBox.Text);
                        }
                    };
                    break;
            }

            // Create a horizontal StackPanel to hold the label and the control
            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(5),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            };

            // Add a TextBlock for the label
            stackPanel.Children.Add(new TextBlock
            {
                Text = $"{key}:",
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0), // Add some spacing between the label and the control
            });

            // Add the control to the StackPanel
            stackPanel.Children.Add(control);

            // Add the StackPanel to the PreferencesContent panel
            PreferencesContent.Children.Add(stackPanel);
        }

        private void UpdatePreference(string key, object value)
        {
            foreach (var panel in _preferences)
            {
                if (panel.Value is JsonElement panelJsonElement && panelJsonElement.ValueKind == JsonValueKind.Object)
                {
                    var panelPreferences = JsonSerializer.Deserialize<Dictionary<string, object>>(panelJsonElement.GetRawText());
                    if (panelPreferences != null && panelPreferences.TryGetValue(key, out var existingPreference))
                    {
                        if (existingPreference is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Object)
                        {
                            var updatedPreference = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonElement.GetRawText());
                            if (updatedPreference != null)
                            {
                                updatedPreference["Value"] = value;
                                panelPreferences[key] = JsonSerializer.SerializeToElement(updatedPreference);

                                // Update the parent panel in _preferences
                                _preferences[panel.Key] = JsonSerializer.SerializeToElement(panelPreferences);

                                Console.WriteLine($"Updated preference: {key} = {value}");
                                _eventAggregator.Publish(new ApplicationPreferencesChangedEvent(_preferences));
                                SavePreferencesToDisk();
                                return;
                            }
                        }
                        else
                        {
                            Log.Error($"Preference '{key}' is not a JsonElement or is not an object. Type: {existingPreference?.GetType()}");
                        }
                    }
                }
            }

            Log.Error($"Preference '{key}' not found in any panel.");
        }

        private void SavePreferencesToDisk()
        {
            try
            {
                string prefPath = _blitzAppData.GetPreferencesPath();
                string jsonContent = JsonSerializer.Serialize(_preferences, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(prefPath, jsonContent);
                Log.Information("Preferences saved to disk.");
            }
            catch (Exception ex)
            {
                Log.Error($"Error saving preferences to disk: {ex.Message}");
            }
        }

        private void OkayButton_Click(object sender, RoutedEventArgs e)
        {
            DialogHost.Close(DialogIdentifier, true);
        }
    }
}