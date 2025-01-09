using Dock.Model.Mvvm.Controls;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using System;
using static DockMvvmSample.Models.Tools.Tool1;
using Newtonsoft.Json.Linq;
using CsXFL;

namespace DockMvvmSample.ViewModels.Tools;

public class Tool1ViewModel : Tool
{
    private ObservableCollection<Models.Tools.Tool1.LibraryItem> _items = new()
    {
        
    };

    public Tool1ViewModel()
    {
        var doc = new CsXFL.Document("Hardcoded Path Award");
        if (doc != null)
        {
            foreach (var item in doc.Library.Items)
            {
                _items.Add(new LibraryItem
                {
                    Name = item.Value.Name,
                    UseCount = item.Value.UseCount,
                    Type = item.Value.ItemType
                });
            }
        }
        else
        {
            // Populate LibraryItems with empty items
            _items = new ObservableCollection<Models.Tools.Tool1.LibraryItem>();
        }

        Source = new HierarchicalTreeDataGridSource<LibraryItem>(_items)
        {
            Columns =
            {
                new HierarchicalExpanderColumn<LibraryItem>(
                    new TextColumn<LibraryItem, string>("Name", x => x.Name),
                    x => x.Children),
                new TextColumn<LibraryItem, string>("Type", x => x.Type),
                new TextColumn<LibraryItem, int>("Use Count", x => x.UseCount),
            },
        };
    }

    public HierarchicalTreeDataGridSource<LibraryItem> Source { get; }
}
