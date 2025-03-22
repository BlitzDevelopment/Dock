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
using static Blitz.Models.Tools.Library;
using System.IO;

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
    private BlitzAppData _blitzAppData;
    private MainWindowViewModel _mainWindowViewModel;
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
    public HierarchicalTreeDataGridSource<LibraryItem>? HierarchicalSource { get; set; }
    public FlatTreeDataGridSource<LibraryItem>? FlatSource { get; set; }

    [ObservableProperty]
    private ObservableCollection<LibraryItem> _items = new ObservableCollection<LibraryItem>();
    [ObservableProperty]
    private ObservableCollection<LibraryItem> _flatItems = new ObservableCollection<LibraryItem>();
    [ObservableProperty]
    private ObservableCollection<LibraryItem> _hierarchicalItems = new ObservableCollection<LibraryItem>();

    [ObservableProperty]
    private string _itemCount = "-";
    [ObservableProperty]
    private string? _canvasColor;
    [ObservableProperty]
    private XDocument? _svgData;

    private void MainWindowViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.MainDocument))
        {
            if (_mainWindowViewModel.MainDocument != null) 
            {
                Items.Clear();
                FlatItems.Clear();
                HierarchicalItems.Clear();

                foreach (var item in _mainWindowViewModel.MainDocument.Library.Items)
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

                InvalidateFlatLibrary(_mainWindowViewModel.MainDocument);
                InvalidateHierarchicalLibrary(_mainWindowViewModel.MainDocument);
                CanvasColor = _mainWindowViewModel.MainDocument.BackgroundColor;
            }
            ItemCount = _mainWindowViewModel.MainDocument?.Library.Items.Count.ToString() + " Items" ?? "-";
        }
    }

    private void HandleUserLibrarySelectionChange()
    {
        if (UserLibrarySelection == null || UserLibrarySelection.Length == 0) { return; }
        if (UserLibrarySelection![0].ItemType == "movieclip" || UserLibrarySelection[0].ItemType == "graphic")
        {
            string appDataFolder = _blitzAppData.GetTmpFolder();
            SVGRenderer renderer = new SVGRenderer(_mainWindowViewModel.MainDocument!, appDataFolder, true);

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

    // MARK: Library Public VM
    public LibraryViewModel(MainWindowViewModel mainWindowViewModel)
    {
        _blitzAppData = new BlitzAppData();
        _mainWindowViewModel = mainWindowViewModel;
        _mainWindowViewModel.PropertyChanged += MainWindowViewModelPropertyChanged;
        var doc = mainWindowViewModel.MainDocument;

        UpdateFlatSource();
        FlatItems.CollectionChanged += (sender, e) => UpdateFlatSource();

        // Build HierarchicalTreeDataGridSource
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