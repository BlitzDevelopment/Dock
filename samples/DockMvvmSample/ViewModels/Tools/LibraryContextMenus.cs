using Avalonia.Data.Converters;
using Dock.Model.Mvvm.Controls;
using Avalonia.Controls;
using System;
using System.Globalization;
using Blitz.Views;
using CommunityToolkit.Mvvm.Input;
using DialogHostAvalonia;
using System.Threading.Tasks;

using static Blitz.Models.Tools.Library;

// MARK: Library Contxt Menus
namespace Blitz.ViewModels.Tools
{
    public class ItemTypeToContextMenuConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var libraryItem = value as LibraryItem;
            var itemType = libraryItem!.Type;
            var contextMenuFactory = new ContextMenuFactory();
            return contextMenuFactory.CreateContextMenu(itemType!);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public partial class ContextMenuFactory
    {
        public ContextMenu CreateContextMenu(string itemType)
        {            
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

        #region Graphic/MC
        private ContextMenu CreateGraphicContextMenu()
        {
            var contextMenu = new ContextMenu();
            contextMenu.Items.Add(new MenuItem { Header = "Cut"});
            contextMenu.Items.Add(new MenuItem { Header = "Copy"});
            contextMenu.Items.Add(new MenuItem { Header = "Paste"});
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(new MenuItem { Header = "Rename", Command = RenameCommand});
            contextMenu.Items.Add(new MenuItem { Header = "Duplicate"});
            contextMenu.Items.Add(new MenuItem { Header = "Edit"});
            contextMenu.Items.Add(new Separator());
            // TODO: Graphic/MC Properties Dialog
            contextMenu.Items.Add(new MenuItem { Header = "Properties"});
            return contextMenu;
        }
        #endregion

        #region Folder
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
            // TODO: This should be easy to implement
            contextMenu.Items.Add(new MenuItem { Header = "Expand Folder"});
            contextMenu.Items.Add(new MenuItem { Header = "Collapse Folder"});
            contextMenu.Items.Add(new MenuItem { Header = "Expand All Folders"});
            contextMenu.Items.Add(new MenuItem { Header = "Collapse All Folders"});
            return contextMenu;
        }
        #endregion

        #region Sound
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
            // TODO: Audio thread
            contextMenu.Items.Add(new MenuItem { Header = "Play"});
            contextMenu.Items.Add(new MenuItem { Header = "Update"});
            contextMenu.Items.Add(new Separator());
            // TODO: Sound Properties Dialog
            contextMenu.Items.Add(new MenuItem { Header = "Properties"});
            return contextMenu;
        }
        #endregion

        #region Bitmap
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
            // TODO: What is update? Can we just make this "replace"?
            contextMenu.Items.Add(new MenuItem { Header = "Update"});
            contextMenu.Items.Add(new Separator());
            // TODO: Bitmap Properties Dialog
            contextMenu.Items.Add(new MenuItem { Header = "Properties"});
            return contextMenu;
        }
        #endregion

        // MARK: Generic Cmmds
        [RelayCommand]
        private async Task Rename()
        {
            var dialog = new LibrarySingleRename();
            var dialogIdentifier = await DialogHost.Show(dialog) as string;
            dialog.DialogIdentifier = dialogIdentifier!;
        }

        // Todo: Duplicate is generic
        // Todo: Edit will be generic when Canvas is implemented
        // Todo: Deletion is generic
    }    

    public partial class LibraryViewModel : Tool
    {
        public ContextMenuFactory ContextMenuFactoryInstance { get; } = new ContextMenuFactory();
    }
}