using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Blitz.Events;
using Blitz.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;
using Rendering;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Blitz.ViewModels.Documents;
using static Blitz.Models.Tools.Library;
using Serilog;
using ExCSS;

namespace Blitz.ViewModels.Tools;

#region AXAML Converters
/// <summary>
/// Converts an item type string to a corresponding icon represented as a StreamGeometry object.
/// </summary>
public class ItemTypeToIconConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        switch (value)
        {
            case "Component":
                return Application.Current!.Resources["ico_lib_type_component"] as StreamGeometry ?? new StreamGeometry();
            case "Movie Clip":
                return Application.Current!.Resources["ico_lib_type_movie_clip"] as StreamGeometry ?? new StreamGeometry();
            case "Graphic":
                return Application.Current!.Resources["ico_lib_type_graphic"] as StreamGeometry ?? new StreamGeometry();
            case "Button":
                return Application.Current!.Resources["ico_lib_type_button"] as StreamGeometry ?? new StreamGeometry();
            case "Puppet":
                return Application.Current!.Resources["ico_lib_type_puppet"] as StreamGeometry ?? new StreamGeometry();
            case "Puppetbase":
                return Application.Current!.Resources["ico_lib_type_puppetBase"] as StreamGeometry ?? new StreamGeometry();
            case "Folder":
                return Application.Current!.Resources["ico_lib_type_folder"] as StreamGeometry ?? new StreamGeometry();
            case "Font":
                return Application.Current!.Resources["ico_lib_type_font"] as StreamGeometry ?? new StreamGeometry();
            case "Sound":
                return Application.Current!.Resources["ico_lib_type_sound"] as StreamGeometry ?? new StreamGeometry();
            case "Bitmap":
                return Application.Current!.Resources["ico_lib_type_bitmap"] as StreamGeometry ?? new StreamGeometry();
            case "Compiled Clip":
                return Application.Current!.Resources["ico_lib_type_compiled_clip"] as StreamGeometry ?? new StreamGeometry();
            case "Screen":
                return Application.Current!.Resources["ico_lib_type_screen"] as StreamGeometry ?? new StreamGeometry();
            case "Video":
                return Application.Current!.Resources["ico_lib_type_video"] as StreamGeometry ?? new StreamGeometry();
            case "Undefined":
                return Application.Current!.Resources["ico_lib_type_undefined"] as StreamGeometry ?? new StreamGeometry();
        }
        return Application.Current!.Resources["ico_lib_type_undefined"] as StreamGeometry ?? new StreamGeometry();
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class MultiTypeToHitTestConverter : IValueConverter
{
    private static readonly HashSet<string> NonHitTestableTypes = new()
    {
        "Undefined",
        "Component",
        "Puppet",
        "Puppetbase",
        "IK Container",
        "Folder",
        "Font",
        "Compiled Clip",
        "Screen",
        "Video"
    };

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // Example logic: Disable hit testing for folders and specific types
        if (value is string type)
        {
            return !NonHitTestableTypes.Contains(type);
        }

        return true; // Default to hit testable
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Converts a boolean to a border brush used for highlighting a row in the Library TreeDataGrid.
/// </summary>
public class BooleanToBorderBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return (bool)value! ? Brushes.Yellow : Brushes.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a boolean to a background color used for highlighting a row in the Library TreeDataGrid.
/// </summary>
public class BooleanToBackgroundConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isDragOver)
        {
            return isDragOver 
                ? new SolidColorBrush(Avalonia.Media.Color.FromArgb(64, 255, 255, 0)) // 64 is 0.25 opacity (255 * 0.25)
                : Brushes.Transparent;
        }
        return Brushes.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Inverts a boolean
/// </summary>
public class InverseBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool booleanValue)
        {
            return !booleanValue;
        }
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
#endregion

#region Constructor
/// <summary>
/// Represents the view model for the library, providing properties and methods
/// to manage and interact with the library's data.
/// </summary>
public partial class LibraryViewModel : Tool
{
    private readonly IGenericDialogs _genericDialogs;
    private readonly EventAggregator _eventAggregator;
    private readonly BlitzAppData _blitzAppData;
    public DocumentViewModel DocumentViewModel;

    public LibraryViewModel()
    {
        Log.Information("[LibraryViewModel] Initializing...");

        // Initialize dependencies
        _genericDialogs = new IGenericDialogs();
        _eventAggregator = EventAggregator.Instance;
        _blitzAppData = new BlitzAppData();

        // Subscribe to events
        _eventAggregator.Subscribe<ActiveDocumentChangedEvent>(OnActiveDocumentChanged);
        _eventAggregator.Subscribe<LibraryItemsChangedEvent>(OnLibraryItemsChanged);

        // Initialize the working document
        try
        {
            _workingCsXFLDoc = CsXFL.An.GetActiveDocument();
        }
        catch
        {
            _workingCsXFLDoc = null;
        }

        // Update the flat source
        UpdateFlatSource();
        FlatItems.CollectionChanged += (sender, e) => UpdateFlatSource();

        // Build HierarchicalTreeDataGridSource, which is the default
        HierarchicalSource = new HierarchicalTreeDataGridSource<LibraryItem>(HierarchicalItems)
        {
            Columns =
            {
                new HierarchicalExpanderColumn<LibraryItem>(
                    new TemplateColumn<LibraryItem>("Name", "NameColumn"),
                    x => x.Children),
                new TextColumn<LibraryItem, string>("Type", x => x.Type),
                new TextColumn<LibraryItem, string>("Use Count", x => x.UseCount),
            },
        };
        HierarchicalSource.Columns.SetColumnWidth(0, new GridLength(250));
        HierarchicalSource.RowSelection!.SingleSelect = false;

        HierarchicalSource.RowSelection.SelectionChanged += (sender, e) =>
        {
            var selectedItems = HierarchicalSource.RowSelection.SelectedItems.OfType<LibraryItem>();
            UserLibrarySelection = selectedItems.Select(item => item.CsXFLItem!).ToArray();
        };
    }
    #endregion

    #region Document State
    private CsXFL.Document? _workingCsXFLDoc;
    private CsXFL.Item[]? _userLibrarySelection;
    public CsXFL.Item[]? UserLibrarySelection
    {
        get => _userLibrarySelection;
        set
        {
            _userLibrarySelection = value;
            OnPropertyChanged(nameof(UserLibrarySelection));
            HandleUserLibrarySelectionChange();
            _eventAggregator.Publish(new UserLibrarySelectionChangedEvent(_userLibrarySelection!));
        }
    }
    #endregion

    #region Data Sources
    public HierarchicalTreeDataGridSource<LibraryItem>? HierarchicalSource { get; set; }
    public FlatTreeDataGridSource<LibraryItem>? FlatSource { get; set; }
    #endregion

    #region Obs. Collections
    private HashSet<string> _itemNames = new HashSet<string>();

    [ObservableProperty]
    private ObservableCollection<LibraryItem> _items = new ObservableCollection<LibraryItem>();

    [ObservableProperty]
    private ObservableCollection<LibraryItem> _flatItems = new ObservableCollection<LibraryItem>();

    [ObservableProperty]
    private ObservableCollection<LibraryItem> _hierarchicalItems = new ObservableCollection<LibraryItem>();
    #endregion

    #region UI State
    [ObservableProperty]
    private string _itemCount = "-";
    [ObservableProperty]
    private string? _canvasColor;
    [ObservableProperty]
    private XDocument? _svgData;
    [ObservableProperty]
    private CsXFL.Rectangle? _boundingBox;
    [ObservableProperty]
    private CsXFL.BitmapItem? _bitmap;
    [ObservableProperty]
    private CsXFL.SoundItem? _sound;
    private readonly List<string> expandedLibraryItems = new List<string>();
    #endregion

    #region Event Handlers
    void OnActiveDocumentChanged(ActiveDocumentChangedEvent e)
    {
        _workingCsXFLDoc = CsXFL.An.GetDocument(e.Document.DocumentIndex!.Value);
        DocumentViewModel = e.Document;
        Log.Information($"[LibraryViewModel] Active document changed to {_workingCsXFLDoc.Filename}");

        InitializeLibraryItems();
        RebuildLibrary();
    }

    void OnLibraryItemsChanged(LibraryItemsChangedEvent e)
    {
        RebuildLibrary();
    }

    void HandleUserLibrarySelectionChange()
    {
        SvgData = null;
        Bitmap = null;
        Sound = null;
        if (UserLibrarySelection == null || UserLibrarySelection.Length == 0 || UserLibrarySelection[0].ItemType == "folder") { return; } // No selection or folder selected

        // Associated logic in LibraryView.axaml.cs for previewing datatypes
        if (UserLibrarySelection![0].ItemType == "movie clip" || UserLibrarySelection[0].ItemType == "graphic" || UserLibrarySelection[0].ItemType == "button")
        {
            string appDataFolder = _blitzAppData.GetTmpFolder();
            SVGRenderer renderer = new SVGRenderer(_workingCsXFLDoc!, appDataFolder, true);

            try
            {
                var symbolToRender = UserLibrarySelection[0] as CsXFL.SymbolItem;
                (XDocument renderedSVG, CsXFL.Rectangle bbox) = renderer.RenderSymbol(symbolToRender!.Timeline, 0);
                SvgData = renderedSVG;
                BoundingBox = bbox;
            }
            catch (Exception ex)
            {
                SvgData = null;
                BoundingBox = null;
                Log.Error(ex, "Failed to render symbol: {ErrorMessage}", ex.Message);
            }
        }

        if (UserLibrarySelection[0].ItemType == "bitmap") { Bitmap = UserLibrarySelection[0] as CsXFL.BitmapItem; }
        if (UserLibrarySelection[0].ItemType == "sound") { Sound = UserLibrarySelection[0] as CsXFL.SoundItem; }
    }
    #endregion

    #region Building Library UI
    void InitializeLibraryItems()
    {
        Items.Clear();
        _itemNames.Clear();
        foreach (var item in _workingCsXFLDoc.Library.Items.Values)
        {
            if (!_itemNames.Contains(item.Name))
            {
                Items.Add(new LibraryItem
                {
                    Name = item.Name,
                    UseCount = item.ItemType == "folder" ? "" : item.UseCount.ToString(),
                    Type = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(item.ItemType.ToLower()),
                    CsXFLItem = item
                });
                _itemNames.Add(item.Name);
            }
        }
    }

    /// <summary>
    /// Rebuilds the library by clearing existing items and repopulating them 
    /// from the current working document. Updates flat and hierarchical views 
    /// and sets the canvas background color.
    /// </summary>
    void RebuildLibrary()
    {
        if (_workingCsXFLDoc == null) 
        {
            ItemCount = "-";
            return;
        }

        GetExpandedRows();

        InitializeLibraryItems();
        InvalidateFlatLibrary(_workingCsXFLDoc);
        InvalidateHierarchicalLibrary(_workingCsXFLDoc);

        // Update the canvas color
        CanvasColor = _workingCsXFLDoc.BackgroundColor;

        // Update the item count
        ItemCount = $"{_workingCsXFLDoc.Library.Items.Count} Items";
    }

    /// <summary>
    /// Invalidates the flat library cache and rebuilds the flat library view.
    /// </summary>
    void InvalidateFlatLibrary(CsXFL.Document doc) {
        FlatItems.Clear();
        var newFlatItems = new List<LibraryItem>();
        foreach (var item in Items)
        {
            if (item.Type == "folder") continue;
            newFlatItems.Add(new LibraryItem
            {
                Name = item.Name,
                UseCount = item.Type == "folder" ? "" : item.UseCount!.ToString(),
                Type = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(item.Type!.ToLower()),
                CsXFLItem = item.CsXFLItem
            });
        }
        FlatItems = new ObservableCollection<LibraryItem>(newFlatItems);
    }

    /// <summary>
    /// Invalidates the hierarchical library cache and rebuilds the hierarchical library view.
    /// </summary>
    void InvalidateHierarchicalLibrary(CsXFL.Document doc)
    {
        HierarchicalItems.Clear();

        var itemsByName = Items.ToDictionary(
            item => item.Name!,
            item => new LibraryItem
            {
                Name = item.Name,
                UseCount = item.Type == "folder" ? "" : item.UseCount!.ToString(),
                Type = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(item.Type!.ToLower()),
                CsXFLItem = item.CsXFLItem
            });

        // Pass 1: Organize items into folders or root
        foreach (var item in itemsByName.Values)
        {
            if (item.Type == "folder") continue;

            var lastSlashIndex = item.Name!.LastIndexOf('/');
            if (lastSlashIndex >= 0)
            {
                var parentName = item.Name.Substring(0, lastSlashIndex);
                if (itemsByName.TryGetValue(parentName, out var parentItem))
                {
                    parentItem.Children ??= new ObservableCollection<LibraryItem>();
                    parentItem.Children.Add(item);
                }
                else
                {
                    HierarchicalItems.Add(item);
                }
            }
            else
            {
                HierarchicalItems.Add(item);
            }
        }

        // Pass 2: Add folders to the root or their parents
        foreach (var item in itemsByName.Values)
        {
            if (item.Type != "folder") continue;

            var lastSlashIndex = item.Name!.LastIndexOf('/');
            if (lastSlashIndex >= 0)
            {
                var parentName = item.Name.Substring(0, lastSlashIndex);
                if (itemsByName.TryGetValue(parentName, out var parentItem))
                {
                    parentItem.Children ??= new ObservableCollection<LibraryItem>();
                    parentItem.Children.Add(item);
                }
                else
                {
                    HierarchicalItems.Add(item);
                }
            }
            else
            {
                HierarchicalItems.Add(item);
            }
        }

        // Pass 3: Update item names to remove paths
        foreach (var item in itemsByName.Values)
        {
            var lastSlashIndex = item.Name!.LastIndexOf('/');
            if (lastSlashIndex >= 0)
            {
                item.Name = item.Name.Substring(lastSlashIndex + 1);
            }
        }

        // Restore Folder Expanded State
        SetExpandedRows();
    }

    /// <summary>
    /// Captures the current expansion state of rows in the hierarchical data grid.
    /// Iterates through the rows using a stack-based approach to avoid recursion.
    /// For each expanded row, its hierarchical path (a sequence of indices) is recorded
    /// in the `expandedLibraryItems` list for later restoration.
    /// </summary>
    void GetExpandedRows()
    {
        // Clear the list of expanded rows at the start
        expandedLibraryItems.Clear();

        if (HierarchicalSource?.Rows == null) return;

        // Use a stack to avoid recursion
        var stack = new Stack<(IEnumerable<IRow> Rows, List<int> Path)>();
        stack.Push((HierarchicalSource.Rows, new List<int>()));

        while (stack.Count > 0)
        {
            var (rows, currentPath) = stack.Pop();
            int index = 0;

            foreach (var row in rows)
            {
                if (row is HierarchicalRow<LibraryItem> hierarchicalRow)
                {
                    var path = new List<int>(currentPath) { index };

                    if (hierarchicalRow.IsExpanded)
                    {
                        expandedLibraryItems.Add(string.Join("/", path));
                    }

                    // Push children to the stack
                    if (hierarchicalRow.Children != null)
                    {
                        stack.Push((hierarchicalRow.Children, path));
                    }
                }

                index++;
            }
        }
    }

    /// <summary>
    /// Restores the expansion state of rows in the hierarchical data grid.
    /// Iterates through the rows using a stack-based approach to avoid recursion.
    /// For each row, checks if its hierarchical path matches any in the `expandedLibraryItems` list
    /// and sets its `IsExpanded` property accordingly.
    /// </summary>
    void SetExpandedRows()
    {
        if (HierarchicalSource?.Rows == null) return;

        // Create a copy of the rows to avoid modifying the collection while iterating
        var rowsCopy = HierarchicalSource.Rows.ToList();

        // Use a stack to avoid recursion
        var stack = new Stack<(IEnumerable<IRow> Rows, List<int> Path)>();
        stack.Push((rowsCopy, new List<int>()));

        while (stack.Count > 0)
        {
            var (rows, currentPath) = stack.Pop();
            int index = 0;

            foreach (var row in rows)
            {
                if (row is HierarchicalRow<LibraryItem> hierarchicalRow)
                {
                    var path = new List<int>(currentPath) { index };
                    string expandedPath = string.Join("/", path);

                    // Set expansion state if the path is in the list
                    hierarchicalRow.IsExpanded = expandedLibraryItems.Contains(expandedPath);

                    // Push children to the stack
                    if (hierarchicalRow.Children != null)
                    {
                        stack.Push((hierarchicalRow.Children.ToList(), path)); // Copy children to avoid modification issues
                    }
                }

                index++;
            }
        }
    }

    void UpdateFlatSource()
    {
        FlatSource = new FlatTreeDataGridSource<LibraryItem>(FlatItems)
        {
            Columns =
            {
                new TextColumn<LibraryItem, string>("Name", x => Path.GetFileName(x.Name)),
                new TextColumn<LibraryItem, string>("Type", x => x.Type),
                new TextColumn<LibraryItem, string>("Use Count", x => x.UseCount),
            },
        };
        FlatSource.Columns.SetColumnWidth(0, new GridLength(250));
        FlatSource.RowSelection!.SingleSelect = false;
        FlatSource.RowSelection.SelectionChanged += (sender, e) =>
        {
            var selectedItems = FlatSource.RowSelection.SelectedItems.OfType<LibraryItem>();
            UserLibrarySelection = selectedItems.Select(item => item.CsXFLItem!).ToArray();
        };
    }
    #endregion

    #region Buttons
    /// <summary>
    /// Adds a folder to the library.
    /// </summary>
    [RelayCommand]
    public async Task AddFolder()
    {
        try
        {
            if (_workingCsXFLDoc == null) { throw new Exception("No document is open."); }
            string baseName = "New Folder";
            int maxNumber = 0;

            foreach (var item in _workingCsXFLDoc.Library.Items)
            {
                if (item.Value.Name.StartsWith(baseName))
                {
                    string suffix = item.Value.Name.Substring(baseName.Length).Trim();
                    if (int.TryParse(suffix, out int number))
                    {
                        maxNumber = Math.Max(maxNumber, number);
                    }
                }
            }

            string newFolderName = $"{baseName} {maxNumber + 1}";

            // Add the folder to the library
            _workingCsXFLDoc.Library.NewFolder(newFolderName);

            // Create a new LibraryItem
            var newLibraryItem = new LibraryItem
            {
                Name = newFolderName,
                Type = "folder",
                UseCount = "",
                CsXFLItem = _workingCsXFLDoc.Library.Items[newFolderName]
            };

            Items.Add(newLibraryItem);

            // Notify other components
            _eventAggregator.Publish(new LibraryItemsChangedEvent());
        }
        catch (Exception e)
        {
            Log.Error(e, "An error occurred: {ErrorMessage}", e.Message);
            await _genericDialogs.ShowError(e.Message);
        }
    }

    /// <summary>
    /// Adds a graphic symbol to the library.
    /// </summary>
    [RelayCommand]
    public async Task AddSymbol()
    {
        try {
            if (_workingCsXFLDoc == null) { throw new Exception("No document is open."); }
            
            string baseName = "New Symbol";
            int maxNumber = 0;

            foreach (var item in _workingCsXFLDoc.Library.Items)
            {
                if (item.Value.Name.StartsWith(baseName))
                {
                    string suffix = item.Value.Name.Substring(baseName.Length).Trim();
                    if (int.TryParse(suffix, out int number))
                    {
                        maxNumber = Math.Max(maxNumber, number);
                    }
                }
            }

            string newGraphicName = $"{baseName} {maxNumber + 1}";

            // Add the new LibraryItem to the library
            var createdItem = _workingCsXFLDoc.Library.AddNewItem("graphic", newGraphicName);

            // Create a new LibraryItem
            var newLibraryItem = new LibraryItem
            {
                Name = newGraphicName,
                Type = "graphic",
                UseCount = "0",
                CsXFLItem = createdItem
            };

            Items.Add(newLibraryItem);
            _eventAggregator.Publish(new LibraryItemsChangedEvent());
        } catch (Exception e) {
            Log.Error(e, "An error occurred: {ErrorMessage}", e.Message);
            await _genericDialogs.ShowError(e.Message);
        }
    }

    /// <summary>
    /// Deletes the selected items from the library. Shows a warning if 5 or more items, including descendants, are selected.
    /// </summary>
    [RelayCommand]
    public async Task Delete()
    {
        try
        {
            _workingCsXFLDoc = CsXFL.An.GetActiveDocument();
            if (_workingCsXFLDoc == null) { throw new Exception("No document is open."); }
            if (_userLibrarySelection == null) { throw new Exception("No items are selected."); }

            // Calculate the total number of items, including descendants
            int totalItemsToDelete = _userLibrarySelection.Sum(item => CountItems(item));

            if (totalItemsToDelete >= 5) // Show warning if 5 or more items are selected
            {
                bool userAccepted = await _genericDialogs.ShowWarning($"Are you sure you want to delete {totalItemsToDelete} items?");
                if (!userAccepted) { return; } // User Cancelled
            }

            foreach (var item in _userLibrarySelection)
            {
                if (item?.Name == null)
                {
                    continue;
                }
                _workingCsXFLDoc.Library.RemoveItem(item.Name);
            }
            _eventAggregator.Publish(new LibraryItemsChangedEvent());
        }
        catch (Exception e)
        {
            Log.Error(e, "An error occurred: {ErrorMessage}", e.Message);
            await _genericDialogs.ShowError(e.Message);
        }
    }
    #endregion

    #region Helper Methods
    /// <summary>
    /// Recursively counts the number of items, including all descendants, for a given item.
    /// </summary>
    int CountItems(CsXFL.Item item)
    {
        if (item.ItemType != "folder")
        {
            return 1; // Single item
        }

        // If it's a folder, count all its descendants recursively
        var folder = HierarchicalItems.FirstOrDefault(i => i.Name == item.Name);
        if (folder?.Children == null || !folder.Children.Any())
        {
            return 1; // Empty folder
        }

        return 1 + folder.Children.Sum(child => CountItemsRecursive(child));
    }

    /// <summary>
    /// Helper method to recursively count all descendants of a LibraryItem.
    /// </summary>
    int CountItemsRecursive(LibraryItem libraryItem)
    {
        if (libraryItem.Children == null || !libraryItem.Children.Any())
        {
            return 1; // No children
        }

        return 1 + libraryItem.Children.Sum(child => CountItemsRecursive(child));
    }
    #endregion
}