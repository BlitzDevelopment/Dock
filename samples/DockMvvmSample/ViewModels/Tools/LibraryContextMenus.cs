using System.Windows.Input;
using Avalonia.Data.Converters;
using Dock.Model.Mvvm.Controls;
using Avalonia.Controls;
using System.Collections.Generic;
using System;
using System.Globalization;
using static DockMvvmSample.Models.Tools.Tool1;

// MARK: Library Contxt Menus
namespace DockMvvmSample.ViewModels.Tools
{
    public class ItemTypeToContextMenuConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var libraryItem = value as LibraryItem;
            var itemType = libraryItem!.Type;
            var contextMenuFactory = new ContextMenuFactory();
            return contextMenuFactory.CreateContextMenu(itemType!);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ContextMenuFactory
    {
        public ContextMenu CreateContextMenu(string itemType)
        {
            Console.WriteLine($"Creating context factory for {itemType} & {itemType.GetType()}");
            
            switch (itemType)
            {
                case "Graphic":
                    return CreateGraphicContextMenu();
                case "Button":
                    return CreateGraphicContextMenu();
                case "Movie Clip":
                    return CreateGraphicContextMenu();
                case "Folder":
                    return CreateFolderContextMenu();
                case "Sound":
                    return CreateSoundContextMenu();
                case "Bitmap":
                    return CreateBitmapContextMenu();
                default:
                    throw new NotImplementedException();
            }
        }

        private ContextMenu CreateGraphicContextMenu()
        {
            var contextMenu = new ContextMenu();
            contextMenu.Items.Add(new MenuItem { Header = "Cut"});
            contextMenu.Items.Add(new MenuItem { Header = "Copy"});
            contextMenu.Items.Add(new MenuItem { Header = "Paste"});
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(new MenuItem { Header = "Rename"});
            contextMenu.Items.Add(new MenuItem { Header = "Duplicate"});
            contextMenu.Items.Add(new MenuItem { Header = "Edit"});
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(new MenuItem { Header = "Properties"});
            return contextMenu;
        }

        private ContextMenu CreateFolderContextMenu()
        {
            var contextMenu = new ContextMenu();
            contextMenu.Items.Add(new MenuItem { Header = "Cut"});
            contextMenu.Items.Add(new MenuItem { Header = "Copy"});
            contextMenu.Items.Add(new MenuItem { Header = "Paste"});
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(new MenuItem { Header = "Rename"});
            contextMenu.Items.Add(new MenuItem { Header = "Duplicate"});
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(new MenuItem { Header = "Expand Folder"});
            contextMenu.Items.Add(new MenuItem { Header = "Collapse Folder"});
            contextMenu.Items.Add(new MenuItem { Header = "Expand All Folders"});
            contextMenu.Items.Add(new MenuItem { Header = "Collapse All Folders"});
            return contextMenu;
        }

        private ContextMenu CreateSoundContextMenu()
        {
            var contextMenu = new ContextMenu();
            contextMenu.Items.Add(new MenuItem { Header = "Cut"});
            contextMenu.Items.Add(new MenuItem { Header = "Copy"});
            contextMenu.Items.Add(new MenuItem { Header = "Paste"});
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(new MenuItem { Header = "Rename"});
            contextMenu.Items.Add(new MenuItem { Header = "Duplicate"});
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(new MenuItem { Header = "Play"});
            contextMenu.Items.Add(new MenuItem { Header = "Update"});
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(new MenuItem { Header = "Properties"});
            return contextMenu;
        }

        private ContextMenu CreateBitmapContextMenu()
        {
            var contextMenu = new ContextMenu();
            contextMenu.Items.Add(new MenuItem { Header = "Cut"});
            contextMenu.Items.Add(new MenuItem { Header = "Copy"});
            contextMenu.Items.Add(new MenuItem { Header = "Paste"});
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(new MenuItem { Header = "Rename"});
            contextMenu.Items.Add(new MenuItem { Header = "Duplicate"});
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(new MenuItem { Header = "Edit"});
            contextMenu.Items.Add(new MenuItem { Header = "Update"});
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(new MenuItem { Header = "Properties"});
            return contextMenu;
        }
    }

    public partial class Tool1ViewModel : Tool
    {
        public ContextMenuFactory ContextMenuFactoryInstance { get; } = new ContextMenuFactory();
    }
}