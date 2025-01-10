using Dock.Model.Mvvm.Controls;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using CommunityToolkit.Mvvm.ComponentModel;
using static DockMvvmSample.Models.Tools.Tool1;
using System;
using System.ComponentModel;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;
using CsXFL;

namespace DockMvvmSample.ViewModels.Tools;

public partial class Tool1ViewModel : Tool
{
    private MainWindowViewModel _mainWindowViewModel;

    [ObservableProperty]
    private ObservableCollection<Models.Tools.Tool1.LibraryItem> _items = new ObservableCollection<Models.Tools.Tool1.LibraryItem>();
    [ObservableProperty]
    private string _itemCount = "-";
    public HierarchicalTreeDataGridSource<LibraryItem> Source { get; }

    private void MainWindowViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.MainDocument))
        {
            Items.Clear();
            if (_mainWindowViewModel.MainDocument != null) { BuildLibrary(_mainWindowViewModel.MainDocument);}
            ItemCount = _mainWindowViewModel.MainDocument?.Library.Items.Count.ToString() + " Items" ?? "-";
        }
    }

    private void BuildLibrary(CsXFL.Document doc) {
        // Create a dictionary to store the items by name
        var itemsByName = new Dictionary<string, LibraryItem>();

        // Pass 1: Create items
        foreach (var item in doc.Library.Items)
        {
            var libraryItem = new LibraryItem
            {
                Name = item.Value.Name,
                UseCount = item.Value.ItemType == "folder" ? "" : item.Value.UseCount.ToString(),
                Type = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(item.Value.ItemType.ToLower())
            };

            itemsByName[libraryItem.Name] = libraryItem;
        }

        // Pass 2: Add items to folders
        foreach (var item in itemsByName.Values)
        {
            if (item.Type == "folder")
            {
                continue;
            }

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
                // Item doesn't belong to a folder, add it to the root
                Items.Add(item);
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
                    Items.Add(item);
                }
            }
            else if (item.Type == "folder")
            {
                Items.Add(item);
            }
        }

        // Update the Name property of each item to remove the path
        foreach (var item in itemsByName.Values)
        {
            item.Name = item.Name!.Substring(item.Name.LastIndexOf('/') + 1);
        }
    }

    public Tool1ViewModel(MainWindowViewModel mainWindowViewModel)
    {
        _mainWindowViewModel = mainWindowViewModel;
        _mainWindowViewModel.PropertyChanged += MainWindowViewModelPropertyChanged;
        var doc = mainWindowViewModel.MainDocument;

        if (_mainWindowViewModel.MainDocument != null) { BuildLibrary(_mainWindowViewModel.MainDocument);}

        // Build HierarchicalTreeDataGridSource
        Source = new HierarchicalTreeDataGridSource<LibraryItem>(_items)
        {
            Columns =
            {
                new HierarchicalExpanderColumn<LibraryItem>(
                    new TextColumn<LibraryItem, string>("Name", x => x.Name),
                    x => x.Children),
                new TextColumn<LibraryItem, string>("Type", x => x.Type),
                new TextColumn<LibraryItem, string>("Use Count", x => x.UseCount),
            },
        };
    }
}