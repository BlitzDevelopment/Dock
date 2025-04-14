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

namespace Blitz.ViewModels.Tools;

// MARK: LibItem Icons
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
                ? new SolidColorBrush(Color.FromArgb(64, 255, 255, 0)) // 64 is 0.25 opacity (255 * 0.25)
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

// MARK: Library Partial VM
/// <summary>
/// Represents the view model for the library, providing properties and methods
/// to manage and interact with the library's data.
/// </summary>
public partial class LibraryViewModel : Tool
{
    #region Dependencies
    private readonly IGenericDialogs _genericDialogs;
    private readonly EventAggregator _eventAggregator;
    private readonly BlitzAppData _blitzAppData;
    private readonly MainWindowViewModel _mainWindowViewModel;
    public DocumentViewModel DocumentViewModel;

    public LibraryViewModel(
        IGenericDialogs genericDialogs,
        EventAggregator eventAggregator,
        BlitzAppData blitzAppData,
        MainWindowViewModel mainWindowViewModel)
    {
        _genericDialogs = genericDialogs ?? throw new ArgumentNullException(nameof(genericDialogs));
        _eventAggregator = eventAggregator ?? throw new ArgumentNullException(nameof(eventAggregator));
        _blitzAppData = blitzAppData ?? throw new ArgumentNullException(nameof(blitzAppData));
        _mainWindowViewModel = mainWindowViewModel ?? throw new ArgumentNullException(nameof(mainWindowViewModel));
    }
    #endregion

    #region Document and Selection State
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

    #region Observable Collections
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
    private Dictionary<LibraryItem, bool> expandedState = new Dictionary<LibraryItem, bool>();
    #endregion

    private void OnActiveDocumentChanged(ActiveDocumentChangedEvent e)
    {
        _workingCsXFLDoc = CsXFL.An.GetDocument(e.Document.DocumentIndex!.Value);
        DocumentViewModel = e.Document;
        Log.Information($"[LibraryViewModel] Active document changed to {_workingCsXFLDoc.Filename}");
        RebuildLibrary();
    }

    public void OnLibraryItemsChanged(LibraryItemsChangedEvent e)
    {
        RebuildLibrary();
    }

    /// <summary>
    /// Rebuilds the library by clearing existing items and repopulating them 
    /// from the current working document. Updates flat and hierarchical views 
    /// and sets the canvas background color.
    /// </summary>
    public void RebuildLibrary() 
    {
        if (_workingCsXFLDoc != null) 
        {
            Items.Clear();
            FlatItems.Clear();
            HierarchicalItems.Clear();

            foreach (var item in _workingCsXFLDoc.Library.Items)
            {
                var libraryItem = new LibraryItem
                {
                    Name = item.Value.Name,
                    UseCount = item.Value.ItemType == "folder" ? "" : item.Value.UseCount.ToString(),
                    Type = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(item.Value.ItemType.ToLower()),
                    CsXFLItem = item.Value
                };
                Items.Add(libraryItem);
            }

            InvalidateFlatLibrary(_workingCsXFLDoc);
            InvalidateHierarchicalLibrary(_workingCsXFLDoc);
            CanvasColor = _workingCsXFLDoc.BackgroundColor;
        }
        ItemCount = _workingCsXFLDoc?.Library.Items.Count.ToString() + " Items" ?? "-";
    }

    /// <summary>
    /// Invalidates the flat library cache and rebuilds the flat library view.
    /// </summary>
    public void InvalidateFlatLibrary(CsXFL.Document doc) {
        // Create a dictionary to store the items by name
        var itemsByName = new Dictionary<string, LibraryItem>();

        // Pass 1: Transfer items
        foreach (var item in Items)
        {
            var libraryItem = new LibraryItem
            {
                Name = item.Name,
                UseCount = item.Type == "folder" ? "" : item.UseCount!.ToString(),
                Type = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(item.Type!.ToLower()),
                CsXFLItem = item.CsXFLItem
            };
            itemsByName[libraryItem.Name] = libraryItem;
        }

        // Pass 2: Add items
        foreach (var item in itemsByName.Values)
        {
            if (item.Type == "Folder") { continue; }
            FlatItems.Add(item);
        }
    }

    /// <summary>
    /// Invalidates the hierarchical library cache and rebuilds the hierarchical library view.
    /// </summary>
    public void InvalidateHierarchicalLibrary(CsXFL.Document doc)
    {
        var itemsByName = new Dictionary<string, LibraryItem>();

        // Pass 1: Transfer items
        foreach (var item in Items)
        {
            var libraryItem = new LibraryItem
            {
                Name = item.Name,
                UseCount = item.Type == "folder" ? "" : item.UseCount!.ToString(),
                Type = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(item.Type!.ToLower()),
                CsXFLItem = item.CsXFLItem
            };
            itemsByName[libraryItem.Name] = libraryItem;
        }

        // Pass 2: Add items to folders
        foreach (var item in itemsByName.Values)
        {
            if (item.Type == "folder") { continue; }
            var pathParts = item.Name!.Split('/');
            var parentName = string.Join("/", pathParts.Take(pathParts.Length - 1));

            if (itemsByName.TryGetValue(parentName, out var parentItem))
            {
                if (parentItem.Children == null)
                {
                    parentItem.Children = new ObservableCollection<LibraryItem>();
                }
                parentItem.Children.Add(item);
            }
            else
            {
                HierarchicalItems.Add(item);
            }
        }

        // Pass 3: Add folders to the root
        foreach (var item in itemsByName.Values)
        {
            if (item.Type == "folder" && item.Name!.Contains("/"))
            {
                var parentName = string.Join("/", item.Name.Split('/').Take(item.Name.Split('/').Length - 1));
                if (itemsByName.TryGetValue(parentName, out var parentItem))
                {
                    if (parentItem.Children == null)
                    {
                        parentItem.Children = new ObservableCollection<LibraryItem>();
                    }
                    parentItem.Children.Add(item);
                }
                else
                {
                    HierarchicalItems.Add(item);
                }
            }
            else if (item.Type == "folder")
            {
                HierarchicalItems.Add(item);
            }
        }

        // Update the Name property of each item to remove the path
        foreach (var item in itemsByName.Values)
        {
            item.Name = item.Name!.Substring(item.Name.LastIndexOf('/') + 1);
        }

        // Restore Folder Expanded State
        RestoreExpansionState(HierarchicalItems);
    }

    private void RestoreExpansionState(IEnumerable<LibraryItem> items, List<int>? currentPath = null)
    {
        if (items == null) { return; }

        int localIndex = 0;
        foreach (var item in items)
        {
            // Build the hierarchical path for the current item
            var hierarchicalPath = currentPath != null ? new List<int>(currentPath) : new List<int>();
            hierarchicalPath.Add(localIndex);

            // Check if the current item should be expanded
            if (expandedState.TryGetValue(item, out bool isExpanded) && isExpanded)
            {
                // Expand the current item's path
                HierarchicalSource!.Expand(new IndexPath(hierarchicalPath.ToArray()));
            }

            // Recursively restore the state for child items
            if (item.Children != null && item.Children.Any())
            {
                RestoreExpansionState(item.Children, hierarchicalPath);
            }

            localIndex++;
        }
    }

    private void HandleUserLibrarySelectionChange()
    {
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

    public void UpdateFlatSource()
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

    // MARK: Buttons
    /// <summary>
    /// Adds a folder to the library.
    /// </summary>
    [RelayCommand]
    private async Task AddFolder()
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
    private async Task AddSymbol()
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
    /// Deletes the selected items from the library. Shows a warning if 5 or more items are selected.
    /// </summary>
    [RelayCommand]
    private async Task Delete()
    {
        try {
            _workingCsXFLDoc = CsXFL.An.GetActiveDocument();
            if (_workingCsXFLDoc == null) { throw new Exception("No document is open."); }
            if (_userLibrarySelection == null) { throw new Exception("No items are selected."); }

            if (_userLibrarySelection.Length >= 5) // Show warning if 5 or more items are selected
            {
                bool userAccepted = await _genericDialogs.ShowWarning($"Are you sure you want to delete {_userLibrarySelection.Length} items?");
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
        } catch (Exception e) {
            Log.Error(e, "An error occurred: {ErrorMessage}", e.Message);
            await _genericDialogs.ShowError(e.Message);
        }
    }

    // MARK: Library Public VM
    public LibraryViewModel(MainWindowViewModel mainWindowViewModel) : this(new IGenericDialogs(), EventAggregator.Instance, new BlitzAppData(), mainWindowViewModel)
    {
        _eventAggregator.Subscribe<ActiveDocumentChangedEvent>(OnActiveDocumentChanged);
        _eventAggregator.Subscribe<LibraryItemsChangedEvent>(OnLibraryItemsChanged);

        try
        {
            _workingCsXFLDoc = CsXFL.An.GetActiveDocument();
        }
        catch
        {
            _workingCsXFLDoc = null;
        }

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
        
        HierarchicalSource.RowExpanded += (sender, args) =>
        {
            if (args.Row.Model is LibraryItem model)
            {
                expandedState[model] = true; // Mark the item as expanded
            }
        };

        HierarchicalSource.RowCollapsed += (sender, args) =>
        {
            if (args.Row.Model is LibraryItem model)
            {
                expandedState[model] = false; // Mark the item as collapsed
            }
        };
    }
}