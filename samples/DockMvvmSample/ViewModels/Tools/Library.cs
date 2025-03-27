using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Blitz.Events;
using Blitz.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DialogHostAvalonia;
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
using static Blitz.Models.Tools.Library;

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
    private CsXFL.BitmapItem? _bitmap;
    [ObservableProperty]
    private CsXFL.SoundItem? _sound;
    #endregion

    private void OnActiveDocumentChanged(ActiveDocumentChangedEvent e)
    {
        WorkingCsXFLDoc = CsXFL.An.GetDocument(e.Index);
        Console.WriteLine($"[LibraryViewModel] WorkingCsXFLDoc changed to {WorkingCsXFLDoc.Filename}");
        RebuildLibrary();
    }

    public void OnLibraryItemsChanged(LibraryItemsChangedEvent e)
    {
        RebuildLibrary();
    }

    public void RebuildLibrary() 
    {
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

    private void HandleUserLibrarySelectionChange()
    {
        Console.WriteLine($"[LibraryViewModel] UserLibrarySelection changed to {UserLibrarySelection}");
        Bitmap = null;
        Sound = null;
        if (UserLibrarySelection == null || UserLibrarySelection.Length == 0 || UserLibrarySelection[0].ItemType == "folder") { return; }

        if (UserLibrarySelection![0].ItemType == "movieclip" || UserLibrarySelection[0].ItemType == "graphic")
        {
            string appDataFolder = _blitzAppData.GetTmpFolder();
            SVGRenderer renderer = new SVGRenderer(WorkingCsXFLDoc!, appDataFolder, true);

            // TODO: This
            //var renderedSVG = renderer.RenderSymbol((UserLibrarySelection[0] as CsXFL.SymbolItem)!, 0, 512, 512);
            //SvgData = renderedSVG;
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
        FlatSource.RowSelection!.SingleSelect = false;
        FlatSource.RowSelection.SelectionChanged += (sender, e) =>
        {
            var selectedItems = FlatSource.RowSelection.SelectedItems.OfType<LibraryItem>();
            UserLibrarySelection = selectedItems.Select(item => item.CsXFLItem!).ToArray();
        };
    }

    // MARK: Buttons
    [RelayCommand]
    private void AddFolder()
    {
        string baseName = "New Folder";
        int maxNumber = 0;

        foreach (var item in WorkingCsXFLDoc.Library.Items)
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
        WorkingCsXFLDoc.Library.NewFolder(newFolderName);
        LibraryItem newFolder = new LibraryItem
        {
            Name = newFolderName,
            UseCount = "",
            Type = "Folder",
            CsXFLItem = WorkingCsXFLDoc.Library.Items[newFolderName]
        };
        HierarchicalItems.Add(newFolder);
    }

    //TODO: This creates movieclips?
    //Needs its own dialog
    [RelayCommand]
    private void AddSymbol()
    {
        string baseName = "New Symbol";
        int maxNumber = 0;

        foreach (var item in WorkingCsXFLDoc.Library.Items)
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
        WorkingCsXFLDoc.Library.AddNewItem("graphic", newGraphicName);
        LibraryItem newGraphic = new LibraryItem
        {
            Name = newGraphicName,
            UseCount = "",
            Type = "Graphic",
            CsXFLItem = WorkingCsXFLDoc.Library.Items[newGraphicName]
        };
        HierarchicalItems.Add(newGraphic);
    }

    [RelayCommand]
    private async Task<bool> ShowWarning(string text)
    {
        var dialog = new MainGenericWarning(text);
        var result = await DialogHost.Show(dialog);
        var dialogIdentifier = result as string;
        dialog.DialogIdentifier = dialogIdentifier!;
        return result is bool isOkayPressed && isOkayPressed;
    }

    [RelayCommand]
    private async Task Delete()
    {
        WorkingCsXFLDoc = CsXFL.An.GetActiveDocument();
        if (_userLibrarySelection == null) { return; }

        // Show warning if 5 or more items are selected
        if (_userLibrarySelection.Length >= 5)
        {
            bool userAccepted = await ShowWarning($"Are you sure you want to delete {_userLibrarySelection.Length} items?");
            if (!userAccepted) { return; }
        }

        // Check if WorkingCsXFLDoc is null
        if (WorkingCsXFLDoc == null)
        {
            return;
        }

        // Perform deletion
        foreach (var item in _userLibrarySelection)
        {
            if (item?.Name == null)
            {
                continue;
            }
            WorkingCsXFLDoc.Library.RemoveItem(item.Name);
        }

        _eventAggregator.Publish(new LibraryItemsChangedEvent());
    }

    // MARK: Library Public VM
    public LibraryViewModel(MainWindowViewModel mainWindowViewModel)
    {
        _blitzAppData = new BlitzAppData();
        _mainWindowViewModel = mainWindowViewModel;
        _eventAggregator = EventAggregator.Instance;

        _eventAggregator.Subscribe<ActiveDocumentChangedEvent>(OnActiveDocumentChanged);
        _eventAggregator.Subscribe<LibraryItemsChangedEvent>(OnLibraryItemsChanged);

        try
        {
            WorkingCsXFLDoc = CsXFL.An.GetActiveDocument();
        }
        catch
        {
            WorkingCsXFLDoc = null;
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
        HierarchicalSource.RowSelection!.SingleSelect = false;
        HierarchicalSource.RowSelection.SelectionChanged += (sender, e) =>
        {
            var selectedItems = HierarchicalSource.RowSelection.SelectedItems.OfType<LibraryItem>();
            UserLibrarySelection = selectedItems.Select(item => item.CsXFLItem!).ToArray();
        };
    }
}