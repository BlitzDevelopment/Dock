using Dock.Model.Mvvm.Controls;
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Media;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Data.Converters;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.ComponentModel;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using Rendering;
using System.Xml.Linq;
using System.IO;
using static Blitz.Models.Tools.Library;
using Blitz.Events;

namespace Blitz.ViewModels.Tools;

// MARK: LibItem Icons
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

// MARK: Library Partial VM
public partial class LibraryViewModel : Tool
{
    #region Dependencies
    private readonly EventAggregator _eventAggregator;
    private BlitzAppData _blitzAppData;
    private MainWindowViewModel _mainWindowViewModel;
    #endregion

    #region Document and Selection State
    private CsXFL.Document WorkingCsXFLDoc;
    private CsXFL.Item[]? _userLibrarySelection;
    public CsXFL.Item[]? UserLibrarySelection
    {
        get => _userLibrarySelection;
        set
        {
            _userLibrarySelection = value;
            OnPropertyChanged(nameof(UserLibrarySelection));
            HandleUserLibrarySelectionChange();
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
    #endregion

    private void OnActiveDocumentChanged(ActiveDocumentChangedEvent e)
    {
        WorkingCsXFLDoc = e.NewDocument!;
        Console.WriteLine($"[LibraryViewModel] WorkingCsXFLDoc changed to {WorkingCsXFLDoc.Filename}");

        if (WorkingCsXFLDoc != null) 
            {
                Items.Clear();
                FlatItems.Clear();
                HierarchicalItems.Clear();

                foreach (var item in WorkingCsXFLDoc.Library.Items)
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

                InvalidateFlatLibrary(WorkingCsXFLDoc);
                InvalidateHierarchicalLibrary(WorkingCsXFLDoc);
                CanvasColor = WorkingCsXFLDoc.BackgroundColor;
            }
            ItemCount = WorkingCsXFLDoc?.Library.Items.Count.ToString() + " Items" ?? "-";
    }

    private void HandleUserLibrarySelectionChange()
    {
        if (UserLibrarySelection == null || UserLibrarySelection.Length == 0) { return; }
        if (UserLibrarySelection![0].ItemType == "movieclip" || UserLibrarySelection[0].ItemType == "graphic")
        {
            string appDataFolder = _blitzAppData.GetTmpFolder();
            SVGRenderer renderer = new SVGRenderer(WorkingCsXFLDoc!, appDataFolder, true);

            // TODO: This
            //var renderedSVG = renderer.RenderSymbol((UserLibrarySelection[0] as CsXFL.SymbolItem)!, 0, 512, 512);
            //SvgData = renderedSVG;
        }
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
        FlatSource.RowSelection!.SingleSelect = false;

        FlatSource.RowSelection.SelectionChanged += (sender, e) =>
        {
            var selectedItems = FlatSource.RowSelection.SelectedItems.OfType<LibraryItem>();
            UserLibrarySelection = selectedItems.Select(item => item.CsXFLItem!).ToArray();
        };
    }

    /// <summary>
    /// Updates the flat library representation by processing the current library items.
    /// </summary>
    /// <param name="doc">The document associated with the library.</param>
    /// <remarks>
    /// This method performs the following steps:
    /// <list type="number">
    /// <item>
    /// Creates a dictionary to store library items by their name.
    /// </item>
    /// <item>
    /// Iterates through the existing items to create and populate <see cref="LibraryItem"/> objects,
    /// setting their properties such as <c>Name</c>, <c>UseCount</c>, <c>Type</c>, and associated <c>CsXFLItem</c>.
    /// </item>
    /// <item>
    /// Adds non-folder items to the flat library representation (<c>FlatItems</c>).
    /// </item>
    /// </list>
    /// Folder items are excluded from the flat library representation.
    /// </remarks>
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
    /// Reorganizes the library items into a hierarchical structure based on their names and types.
    /// </summary>
    /// <param name="doc">The document associated with the library items.</param>
    /// <remarks>
    /// This method processes the library items in three passes:
    /// <list type="number">
    /// <item>
    /// <description>
    /// <b>Pass 1:</b> Transfers items into a dictionary, creating new <see cref="LibraryItem"/> instances
    /// with their properties initialized based on the original items.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <b>Pass 2:</b> Adds non-folder items to their respective parent folders based on their names.
    /// If a parent folder is not found, the item is added to the root of the hierarchical structure.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <b>Pass 3:</b> Adds folder items to their respective parent folders or to the root if no parent folder exists.
    /// </description>
    /// </item>
    /// </list>
    /// After processing, the <c>Name</c> property of each item is updated to remove its path, leaving only the item's name.
    /// </remarks>
    public void InvalidateHierarchicalLibrary(CsXFL.Document doc) {
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
            // Item doesn't belong to a folder, add it to the root
            else { HierarchicalItems.Add(item); }
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
                { HierarchicalItems.Add(item); }
            }
            else if (item.Type == "folder") { HierarchicalItems.Add(item); }
        }

        // Update the Name property of each item to remove the path
        foreach (var item in itemsByName.Values)
        {
            item.Name = item.Name!.Substring(item.Name.LastIndexOf('/') + 1);
        }
    }

    // MARK: Library Public VM
    public LibraryViewModel(MainWindowViewModel mainWindowViewModel)
    {
        _blitzAppData = new BlitzAppData();
        _mainWindowViewModel = mainWindowViewModel;
        _eventAggregator = EventAggregator.Instance;

        _eventAggregator.Subscribe<ActiveDocumentChangedEvent>(OnActiveDocumentChanged);

        WorkingCsXFLDoc = CsXFL.An.GetActiveDocument();

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
        HierarchicalSource.RowSelection!.SingleSelect = false;

        HierarchicalSource.RowSelection.SelectionChanged += (sender, e) =>
        {
            var selectedItems = HierarchicalSource.RowSelection.SelectedItems.OfType<LibraryItem>();
            UserLibrarySelection = selectedItems.Select(item => item.CsXFLItem!).ToArray();
        };
    }
}