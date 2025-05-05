using Avalonia.Controls;
using Avalonia.Interactivity;
using DialogHostAvalonia;
using Avalonia.Markup.Xaml;
using Avalonia;
using Avalonia.Layout;
using System.Text.Json;
using System;
using System.Collections.Generic;
using Serilog;
using System.Linq;
using Blitz.Events;

namespace Blitz.Views
{
    public partial class MainPreferences : UserControl
    {
        private string _searchText = string.Empty;
        public string? DialogIdentifier { get; set; }

        public MainPreferences()
        {
            AvaloniaXamlLoader.Load(this);

            App.BlitzAppData.LoadPreferences();
            BuildTabs();
            BuildContent();

            DevOps.PropertyChanged += DevOps_PropertyChanged;
            Search.PropertyChanged += Search_PropertyChanged;

            App.EventAggregator.Subscribe<ApplicationPreferencesChangedEvent>(OnPreferencesChanged);
        }

        // Logic for changing the application theme based on user preferences
        private void OnPreferencesChanged(ApplicationPreferencesChangedEvent obj)
        {
            App.BlitzAppData.TryGetNestedPreference<string>("General", "Theme", "Value", out var themeValue);
            var isDarkTheme = themeValue?.Equals("Dark", StringComparison.OrdinalIgnoreCase) ?? false;
            App.ThemeManager?.Switch(isDarkTheme ? 1 : 0);
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

        private void BuildTabs()
        {
            foreach (var panel in App.BlitzAppData.Preferences)
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

            foreach (var panel in App.BlitzAppData.Preferences)
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
            // Use the first key in Preferences if no panelKey is provided
            panelKey ??= App.BlitzAppData.Preferences.Keys.FirstOrDefault();
            if (panelKey == null || !App.BlitzAppData.Preferences.TryGetValue(panelKey, out var panelValue)) return;

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
            foreach (var panel in App.BlitzAppData.Preferences)
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
                                App.BlitzAppData.Preferences[panel.Key] = JsonSerializer.SerializeToElement(panelPreferences);

                                App.EventAggregator.Publish(new ApplicationPreferencesChangedEvent(App.BlitzAppData.Preferences));
                                App.BlitzAppData.SavePreferencesToDisk();
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

        private void OkayButton_Click(object sender, RoutedEventArgs e)
        {
            DialogHost.Close(DialogIdentifier, true);
        }
    }
}