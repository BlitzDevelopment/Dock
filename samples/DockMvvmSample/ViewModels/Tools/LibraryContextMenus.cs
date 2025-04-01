using Avalonia.Controls;
using Avalonia.Data.Converters;
using Blitz.Events;
using Blitz.Views;
using CommunityToolkit.Mvvm.Input;
using DialogHostAvalonia;
using Dock.Model.Mvvm.Controls;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Blitz.ViewModels.Documents;

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
        private readonly AudioService _audioService;
        private readonly EventAggregator _eventAggregator;
        private readonly IGenericDialogs _genericDialogs = new IGenericDialogs();
        private LibraryViewModel _libraryViewModel;
        private CsXFL.Item[]? _userLibrarySelection;
        private CsXFL.Document? _workingCsXFLDoc;

        public ContextMenuFactory()
        {
            _audioService = AudioService.Instance;
            _eventAggregator = EventAggregator.Instance;
            _eventAggregator.Subscribe<UserLibrarySelectionChangedEvent>(OnUserLibrarySelectionChanged);
            _eventAggregator.Subscribe<ActiveDocumentChangedEvent>(OnActiveDocumentChanged);
        }

        private void OnActiveDocumentChanged(ActiveDocumentChangedEvent e)
        {
            Console.WriteLine("ActiveDocumentChangedEvent received in ContextMenuFactory.");
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
            contextMenu.Items.Add(new MenuItem { Header = "Copy", Command = CopyCommand});
            contextMenu.Items.Add(new MenuItem { Header = "Paste", Command = PasteCommand});
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(new MenuItem { Header = "Rename", Command = RenameCommand});
            contextMenu.Items.Add(new MenuItem { Header = "Duplicate"});
            contextMenu.Items.Add(new MenuItem { Header = "Edit"});
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(new MenuItem { Header = "Properties", Command = SymbolPropertiesCommand});
            return contextMenu;
        }

        [RelayCommand]
        private async Task SymbolProperties()
        {
            try {
                _workingCsXFLDoc = CsXFL.An.GetActiveDocument();
                if (_userLibrarySelection == null) { throw new Exception("No items are selected."); }
                if ((_userLibrarySelection[0].ItemType != "graphic") && 
                    (_userLibrarySelection[0].ItemType != "movie clip") && 
                    (_userLibrarySelection[0].ItemType != "button")) 
                { 
                    throw new Exception("Selected item is not a graphic, movie clip, or button.");
                }

                var dialog = new LibrarySymbolProperties(_userLibrarySelection[0]);
                var result = await DialogHost.Show(dialog);
                var dialogIdentifier = result as string;
                dialog.DialogIdentifier = dialogIdentifier!;

                var resultObject = result as dynamic;
                if (resultObject == null) { throw new Exception("LibrarySymbolProperties dialog error, returned object is null."); }
                if (resultObject.Name is not string Name || resultObject.Type is not string Type || resultObject.IsOkay != true)
                {
                    throw new Exception("LibrarySymbolProperties dialog error, returned object is missing properties.");
                }

                var processingSymbol = _userLibrarySelection[0] as CsXFL.SymbolItem;
                string currentPath = processingSymbol!.Name;
                string? directory = Path.GetDirectoryName(currentPath);
                string newPath = directory != null ? Path.Combine(directory, resultObject.Name) : resultObject.Name;
                processingSymbol.Name = newPath;
                processingSymbol.SymbolType = resultObject.Type.ToLower();
                _eventAggregator.Publish(new LibraryItemsChangedEvent());
            } catch (Exception e) {
                await _genericDialogs.ShowError(e.Message);
            }
        }
        #endregion

        #region Folder
        private ContextMenu CreateFolderContextMenu()
        {
            var contextMenu = new ContextMenu();
            contextMenu.Items.Add(new MenuItem { Header = "Cut", Command = DeleteCommand});
            contextMenu.Items.Add(new MenuItem { Header = "Copy", Command = CopyCommand});
            contextMenu.Items.Add(new MenuItem { Header = "Paste", Command = PasteCommand});
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(new MenuItem { Header = "Rename", Command = RenameCommand});
            contextMenu.Items.Add(new MenuItem { Header = "Duplicate"});
            contextMenu.Items.Add(new Separator());
            // TODO: This should be easy to implement
            contextMenu.Items.Add(new MenuItem { Header = "Expand Folder", Command = ExpandFolderCommand});
            contextMenu.Items.Add(new MenuItem { Header = "Collapse Folder", Command = CollapseFolderCommand});
            contextMenu.Items.Add(new MenuItem { Header = "Expand All Folders", Command = ExpandAllFoldersCommand});
            contextMenu.Items.Add(new MenuItem { Header = "Collapse All Folders", Command = CollapseAllFoldersCommand});
            return contextMenu;
        }

        private void ExpandMatchingItems(IEnumerable<Blitz.Models.Tools.Library.LibraryItem> items, List<int> currentPath = null)
        {
            int localIndex = 0; // Tracks the index at the current level

            foreach (var item in items)
            {
                string itemPath = item.CsXFLItem!.Name;

                // Build the hierarchical path for the current item
                var hierarchicalPath = new List<int>(currentPath ?? new List<int>()) { localIndex };
                
                // Check if the current item matches any selected item
                if (_userLibrarySelection!.Any(selection => selection.Name == itemPath))
                {
                    _libraryViewModel.HierarchicalSource!.Expand(new Avalonia.Controls.IndexPath(hierarchicalPath.ToArray()));
                }

                // Recursively check and expand child items
                if (item.Children != null && item.Children.Any())
                {
                    ExpandMatchingItems(item.Children, hierarchicalPath);
                }

                // Increment the local index for the next sibling
                localIndex++;
            }
        }

        private void CollapseMatchingItems(IEnumerable<Blitz.Models.Tools.Library.LibraryItem> items, List<int> currentPath = null)
        {
            int localIndex = 0; // Tracks the index at the current level

            foreach (var item in items)
            {
                string itemPath = item.CsXFLItem!.Name;

                // Build the hierarchical path for the current item
                var hierarchicalPath = new List<int>(currentPath ?? new List<int>()) { localIndex };

                // Check if the current item matches any selected item
                if (_userLibrarySelection!.Any(selection => selection.Name == itemPath))
                {
                    _libraryViewModel.HierarchicalSource!.Collapse(new Avalonia.Controls.IndexPath(hierarchicalPath.ToArray()));
                }

                // Recursively check and expand child items
                if (item.Children != null && item.Children.Any())
                {
                    CollapseMatchingItems(item.Children, hierarchicalPath);
                }

                // Increment the local index for the next sibling
                localIndex++;
            }
        }

        [RelayCommand]
        private async Task ExpandFolder()
        {
            try
            {
                _workingCsXFLDoc = CsXFL.An.GetActiveDocument();
                if (_userLibrarySelection == null) { throw new Exception("No items are selected."); }
                if (_userLibrarySelection[0].ItemType != "folder") { throw new Exception("Selected item is not a folder."); }

                var _viewModelRegistry = ViewModelRegistry.Instance;
                _libraryViewModel = (LibraryViewModel)_viewModelRegistry.GetViewModel(nameof(LibraryViewModel));

                ExpandMatchingItems(_libraryViewModel.HierarchicalSource!.Items);
            }
            catch (Exception e)
            {
                await _genericDialogs.ShowError(e.Message);
            }
        }

        [RelayCommand]
        private async Task CollapseFolder()
        {
            try
            {
                _workingCsXFLDoc = CsXFL.An.GetActiveDocument();
                if (_userLibrarySelection == null) { throw new Exception("No items are selected."); }
                if (_userLibrarySelection[0].ItemType != "folder") { throw new Exception("Selected item is not a folder."); }

                var _viewModelRegistry = ViewModelRegistry.Instance;
                _libraryViewModel = (LibraryViewModel)_viewModelRegistry.GetViewModel(nameof(LibraryViewModel));

                CollapseMatchingItems(_libraryViewModel.HierarchicalSource!.Items);
            }
            catch (Exception e)
            {
                await _genericDialogs.ShowError(e.Message);
            }
        }

        [RelayCommand]
        private async Task ExpandAllFolders()
        {
            try
            {
                _workingCsXFLDoc = CsXFL.An.GetActiveDocument();
                if (_userLibrarySelection == null) { throw new Exception("No items are selected."); }
                if (_userLibrarySelection[0].ItemType != "folder") { throw new Exception("Selected item is not a folder."); }

                var _viewModelRegistry = ViewModelRegistry.Instance;
                _libraryViewModel = (LibraryViewModel)_viewModelRegistry.GetViewModel(nameof(LibraryViewModel));

                _libraryViewModel.HierarchicalSource!.ExpandAll();

            }
            catch (Exception e)
            {
                await _genericDialogs.ShowError(e.Message);
            }
        }

        [RelayCommand]
        private async Task CollapseAllFolders()
        {
            try
            {
                _workingCsXFLDoc = CsXFL.An.GetActiveDocument();
                if (_userLibrarySelection == null) { throw new Exception("No items are selected."); }
                if (_userLibrarySelection[0].ItemType != "folder") { throw new Exception("Selected item is not a folder."); }

                var _viewModelRegistry = ViewModelRegistry.Instance;
                _libraryViewModel = (LibraryViewModel)_viewModelRegistry.GetViewModel(nameof(LibraryViewModel));

                _libraryViewModel.HierarchicalSource!.CollapseAll();

            }
            catch (Exception e)
            {
                await _genericDialogs.ShowError(e.Message);
            }
        }
        #endregion

        #region Sound
        private ContextMenu CreateSoundContextMenu()
        {
            var contextMenu = new ContextMenu();
            contextMenu.Items.Add(new MenuItem { Header = "Cut", Command = DeleteCommand});
            contextMenu.Items.Add(new MenuItem { Header = "Copy", Command = CopyCommand});
            contextMenu.Items.Add(new MenuItem { Header = "Paste", Command = PasteCommand});
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(new MenuItem { Header = "Rename", Command = RenameCommand});
            contextMenu.Items.Add(new MenuItem { Header = "Duplicate"});
            contextMenu.Items.Add(new Separator());
            // TODO: Audio thread
            contextMenu.Items.Add(new MenuItem { Header = "Play", Command = PlayCommand, CommandParameter = this});
            contextMenu.Items.Add(new MenuItem { Header = "Update"});
            contextMenu.Items.Add(new Separator());
            // TODO: Sound Properties Dialog
            contextMenu.Items.Add(new MenuItem { Header = "Properties"});
            return contextMenu;
        }

        [RelayCommand]
        private void Play()
        {
            if (_userLibrarySelection == null) { return; }
            if (_userLibrarySelection[0].ItemType != "sound") { return; }

            var soundItem = _userLibrarySelection[0] as CsXFL.SoundItem;
            var _viewModelRegistry = ViewModelRegistry.Instance;
            _libraryViewModel = (LibraryViewModel)_viewModelRegistry.GetViewModel(nameof(LibraryViewModel));

            if (_libraryViewModel.DocumentViewModel == null)
            {
                throw new InvalidOperationException("_documentViewModel is null.");
            }

            if (soundItem == null)
            {
                throw new ArgumentNullException(nameof(soundItem), "soundItem is null.");
            }

            var audioData = _libraryViewModel.DocumentViewModel.GetAudioData(soundItem);
            // Load audio data into OpenAL buffer
            using (MemoryStream memoryStream = new MemoryStream(audioData))
            {
                // Assuming the audio data is in WAV format
                var (format, data, sampleRate) = _audioService.LoadWave(memoryStream);
                _audioService.Play(data, format, sampleRate);
            }
        }

        #endregion

        #region Bitmap
        private ContextMenu CreateBitmapContextMenu()
        {
            var contextMenu = new ContextMenu();
            contextMenu.Items.Add(new MenuItem { Header = "Cut", Command = DeleteCommand});
            contextMenu.Items.Add(new MenuItem { Header = "Copy", Command = CopyCommand});
            contextMenu.Items.Add(new MenuItem { Header = "Paste", Command = PasteCommand});
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(new MenuItem { Header = "Rename", Command = RenameCommand});
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

        // MARK: Generic Cmnds
        [RelayCommand]
        private async Task Rename()
        {
            try {
                var dialog = new LibrarySingleRename();
                var dialogIdentifier = await DialogHost.Show(dialog) as string;
                dialog.DialogIdentifier = dialogIdentifier!;
            } catch (Exception e){
                await _genericDialogs.ShowError(e.Message);
            }
        }

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
                    if (!userAccepted) { return; } // User cancelled
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
                await _genericDialogs.ShowError(e.Message);
            }
        }

        [RelayCommand]
        private async Task Copy()
        {

        }

        [RelayCommand]
        private async Task Paste()
        {

        }

        // Todo: Duplicate is generic
        // Todo: Edit will be generic when Canvas is implemented
    }

    public partial class LibraryViewModel : Tool
    {
        public ContextMenuFactory ContextMenuFactoryInstance { get; } = new ContextMenuFactory();
    }
}