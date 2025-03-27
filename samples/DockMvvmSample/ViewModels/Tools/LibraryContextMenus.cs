using Avalonia.Data.Converters;
using Dock.Model.Mvvm.Controls;
using Avalonia.Controls;
using System;
using System.Globalization;
using Blitz.Views;
using CommunityToolkit.Mvvm.Input;
using DialogHostAvalonia;
using System.Threading.Tasks;
using Blitz.Events;
using System.IO;

using static Blitz.Models.Tools.Library;
using System.Data;
using Blitz.Views.Tools;

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
        private readonly EventAggregator _eventAggregator;
        private CsXFL.Item[]? _userLibrarySelection;
        public CsXFL.Document WorkingCsXFLDoc;
        public ContextMenuFactory()
        {
            _eventAggregator = EventAggregator.Instance;
            _eventAggregator.Subscribe<UserLibrarySelectionChangedEvent>(OnUserLibrarySelectionChanged);
        }

        private void OnUserLibrarySelectionChanged(UserLibrarySelectionChangedEvent e)
        {
            _userLibrarySelection = e.UserLibrarySelection;
        }

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
            contextMenu.Items.Add(new MenuItem { Header = "Cut", Command = DeleteCommand});
            contextMenu.Items.Add(new MenuItem { Header = "Copy"});
            contextMenu.Items.Add(new MenuItem { Header = "Paste"});
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(new MenuItem { Header = "Rename", Command = RenameCommand});
            contextMenu.Items.Add(new MenuItem { Header = "Duplicate"});
            contextMenu.Items.Add(new MenuItem { Header = "Edit"});
            contextMenu.Items.Add(new Separator());
            // TODO: Graphic/MC Properties Dialog
            contextMenu.Items.Add(new MenuItem { Header = "Properties", Command = SymbolPropertiesCommand});
            return contextMenu;
        }

        [RelayCommand]
        private async Task SymbolProperties()
        {
            WorkingCsXFLDoc = CsXFL.An.GetActiveDocument();
            if (_userLibrarySelection == null) { return; }
            if ((_userLibrarySelection[0].ItemType != "graphic") && 
                (_userLibrarySelection[0].ItemType != "movie clip") && 
                (_userLibrarySelection[0].ItemType != "button")) 
            { 
                return; 
            }

            var dialog = new LibrarySymbolProperties(_userLibrarySelection[0]);
            var result = await DialogHost.Show(dialog);
            var dialogIdentifier = result as string;
            dialog.DialogIdentifier = dialogIdentifier!;

            var resultObject = result as dynamic;
            if (resultObject == null) { return; }
            if (resultObject.Name is not string Name || resultObject.Type is not string Type || resultObject.IsOkay != true)
            {
                Console.WriteLine("Dialog error.");
                return;
            }

            string currentPath = _userLibrarySelection[0].Name;
            string? directory = Path.GetDirectoryName(currentPath);
            string newPath = directory != null ? Path.Combine(directory, resultObject.Name) : resultObject.Name;
            _userLibrarySelection[0].Name = newPath;

        }
        #endregion

        #region Folder
        private ContextMenu CreateFolderContextMenu()
        {
            var contextMenu = new ContextMenu();
            contextMenu.Items.Add(new MenuItem { Header = "Cut", Command = DeleteCommand});
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
            contextMenu.Items.Add(new MenuItem { Header = "Cut", Command = DeleteCommand});
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
            contextMenu.Items.Add(new MenuItem { Header = "Cut", Command = DeleteCommand});
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

        // Todo: Duplicate is generic
        // Todo: Edit will be generic when Canvas is implemented
    }

    public partial class LibraryViewModel : Tool
    {
        public ContextMenuFactory ContextMenuFactoryInstance { get; } = new ContextMenuFactory();
    }
}