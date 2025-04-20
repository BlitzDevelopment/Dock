using Avalonia.Controls;
using Avalonia.Data.Converters;
using Blitz.Events;
using Blitz.Views;
using CommunityToolkit.Mvvm.Input;
using DialogHostAvalonia;
using Dock.Model.Mvvm.Controls;
using Serilog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using static Blitz.Models.Tools.Library;

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
        }

        void OnUserLibrarySelectionChanged(UserLibrarySelectionChangedEvent e)
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
        ContextMenu CreateGraphicContextMenu()
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
        /// <summary>
        /// Displays the LibrarySymbolProperties dialog for the selected symbol in the library.
        /// </summary>
        async Task SymbolProperties()
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
                if (resultObject == null) { return; } // User cancelled
                if (resultObject.Name is not string Name || resultObject.Type is not string Type || resultObject.IsOkay != true)
                {
                    throw new Exception("LibrarySymbolProperties dialog error, returned object is missing properties.");
                }

                var processingSymbol = _userLibrarySelection[0] as CsXFL.SymbolItem;
                string currentPath = processingSymbol!.Name;

                string originalPath = processingSymbol.Name.Contains("/") ? processingSymbol.Name.Substring(0, processingSymbol.Name.LastIndexOf('/') + 1) : "";
                string newPath = originalPath + resultObject.Name;

                _workingCsXFLDoc.Library.RenameItem(processingSymbol.Name, newPath);

                processingSymbol.SymbolType = resultObject.Type.ToLower();
                _eventAggregator.Publish(new LibraryItemsChangedEvent());
            } catch (Exception e) {
                await _genericDialogs.ShowError(e.Message);
            }
        }
        #endregion

        #region Folder
        ContextMenu CreateFolderContextMenu()
        {
            var contextMenu = new ContextMenu();
            contextMenu.Items.Add(new MenuItem { Header = "Cut", Command = DeleteCommand});
            contextMenu.Items.Add(new MenuItem { Header = "Copy", Command = CopyCommand});
            contextMenu.Items.Add(new MenuItem { Header = "Paste", Command = PasteCommand});
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(new MenuItem { Header = "Rename", Command = RenameCommand});
            contextMenu.Items.Add(new MenuItem { Header = "Duplicate"});
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(new MenuItem { Header = "Expand Folder", Command = ExpandFolderCommand});
            contextMenu.Items.Add(new MenuItem { Header = "Collapse Folder", Command = CollapseFolderCommand});
            contextMenu.Items.Add(new MenuItem { Header = "Expand All Folders", Command = ExpandAllFoldersCommand});
            contextMenu.Items.Add(new MenuItem { Header = "Collapse All Folders", Command = CollapseAllFoldersCommand});
            return contextMenu;
        }

        /// <summary>
        /// Helper function to recursively expand folders in "Expand Folders" command.
        /// </summary>
        void ExpandMatchingItems(IEnumerable<Blitz.Models.Tools.Library.LibraryItem> items, List<int>? currentPath = null)
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

        /// <summary>
        /// Helper function to recursively collapse folders in "Collapse Folders" command.
        /// </summary>
        void CollapseMatchingItems(IEnumerable<Blitz.Models.Tools.Library.LibraryItem> items, List<int>? currentPath = null)
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
        /// <summary>
        /// Expands the selected folder in the library.
        /// </summary>
        async Task ExpandFolder()
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
        /// <summary>
        /// Collapses the selected folder in the library.
        /// </summary>
        async Task CollapseFolder()
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
        /// <summary>
        /// Expands all folders in the library.
        /// </summary>
        async Task ExpandAllFolders()
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
        /// <summary>
        /// Collapses all folders in the library.
        /// </summary>
        async Task CollapseAllFolders()
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
        ContextMenu CreateSoundContextMenu()
        {
            var contextMenu = new ContextMenu();
            contextMenu.Items.Add(new MenuItem { Header = "Cut", Command = DeleteCommand});
            contextMenu.Items.Add(new MenuItem { Header = "Copy", Command = CopyCommand});
            contextMenu.Items.Add(new MenuItem { Header = "Paste", Command = PasteCommand});
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(new MenuItem { Header = "Rename", Command = RenameCommand});
            contextMenu.Items.Add(new MenuItem { Header = "Duplicate"});
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(new MenuItem { Header = "Play", Command = PlayCommand, CommandParameter = this});
            contextMenu.Items.Add(new MenuItem { Header = "Update"});
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(new MenuItem { Header = "Properties", Command = SoundPropertiesCommand});
            return contextMenu;
        }

        [RelayCommand]
        /// <summary>
        /// Play a given soundItem from the library. Uses OpenAL to play PCM data, use ffmpeg to decode to PCM for other formats.
        /// </summary>
        async Task Play()
        {
            if (_userLibrarySelection == null) { return; }
            if (_userLibrarySelection[0].ItemType != "sound") { return; }

            // Stop any currently playing sound
            _audioService.Stop();

            var soundItem = _userLibrarySelection[0] as CsXFL.SoundItem;
            var _viewModelRegistry = ViewModelRegistry.Instance;
            _libraryViewModel = (LibraryViewModel)_viewModelRegistry.GetViewModel(nameof(LibraryViewModel));

            if (_libraryViewModel.DocumentViewModel == null) { throw new InvalidOperationException("_documentViewModel is null."); }
            if (soundItem == null) { throw new ArgumentNullException(nameof(soundItem), "soundItem is null."); }
            
            string fileExtension = "";
            string format = soundItem.Format;
            string[] parts = format.Split(' ');

            string bitDepthString = new string(parts[1].Where(char.IsDigit).ToArray());
            int bitDepth = int.Parse(bitDepthString);

            int numChannels = format.Contains("Mono", StringComparison.OrdinalIgnoreCase) ? 1 :
                            format.Contains("Stereo", StringComparison.OrdinalIgnoreCase) ? 2 : 0;

            int sampleRate = soundItem.SampleRate;

            if (soundItem.Href != null) {
                fileExtension = Path.GetExtension(soundItem.Href)?.TrimStart('.').ToLower() ?? string.Empty;
            }

            var audioData = _libraryViewModel.DocumentViewModel.DecryptAudioDat(soundItem);
            if (fileExtension == "wav" || fileExtension == "flac")
            {
                using (MemoryStream memoryStream = new MemoryStream(audioData))
                {
                    var (newFormat, data, newSampleRate) = _audioService.LoadWave(memoryStream, numChannels, soundItem.SampleRate, bitDepth);
                    string audioFormat = parts[2];

                    OpenTK.Audio.OpenAL.ALFormat alFormat = audioFormat switch
                    {
                        "Mono" => OpenTK.Audio.OpenAL.ALFormat.Mono16,
                        "Stereo" => OpenTK.Audio.OpenAL.ALFormat.Stereo16,
                        _ => throw new NotSupportedException($"Unsupported audio format: {audioFormat}")
                    };

                    _audioService.Play(data, newFormat, newSampleRate);
                }
            }
            else if (fileExtension == "mp3")
            {
                byte[] pcmData = await Task.Run(() => _audioService.DecodeMp3ToWav(audioData));
                using (MemoryStream memoryStream = new MemoryStream(pcmData))
                {
                    var (newFormat, data, newSampleRate) = _audioService.LoadWave(memoryStream, numChannels, soundItem.SampleRate, bitDepth);
                    string audioFormat = parts[2];

                    OpenTK.Audio.OpenAL.ALFormat alFormat = audioFormat switch
                    {
                        "Mono" => OpenTK.Audio.OpenAL.ALFormat.Mono16,
                        "Stereo" => OpenTK.Audio.OpenAL.ALFormat.Stereo16,
                        _ => throw new NotSupportedException($"Unsupported audio format: {audioFormat}")
                    };

                    _audioService.Play(data, newFormat, newSampleRate);
                }
            }
            else
            {
                Log.Error($"[LibraryContextMenu] Unsupported audio format or href not found: {fileExtension}");
            }
        }

        [RelayCommand]
        /// <summary>
        /// Displays the LibrarySoundProperties dialog for the selected sound in the library.
        /// </summary>
        async Task SoundProperties()
        {
            try {
                _workingCsXFLDoc = CsXFL.An.GetActiveDocument();
                if (_userLibrarySelection == null) { throw new Exception("No items are selected."); }
                if (_userLibrarySelection[0].ItemType != "sound")
                { 
                    throw new Exception("Selected item is not a sound.");
                }

                var _viewModelRegistry = ViewModelRegistry.Instance;
                _libraryViewModel = (LibraryViewModel)_viewModelRegistry.GetViewModel(nameof(LibraryViewModel));

                var audioData = _libraryViewModel.DocumentViewModel.DecryptAudioDat(_userLibrarySelection[0] as CsXFL.SoundItem);
                var dialog = new LibrarySoundProperties(_userLibrarySelection[0], audioData);
                var result = await DialogHost.Show(dialog);
                var dialogIdentifier = result as string;
                dialog.DialogIdentifier = dialogIdentifier!;

                var resultObject = result as dynamic;
                if (resultObject == null) { return; } // User cancelled

                // Do stuff lol

            } catch (Exception e) {
                await _genericDialogs.ShowError(e.Message);
            }
        }

        #endregion

        #region Bitmap
        ContextMenu CreateBitmapContextMenu()
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
            contextMenu.Items.Add(new MenuItem { Header = "Update"});
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(new MenuItem { Header = "Properties", Command = BitmapPropertiesCommand});
            return contextMenu;
        }

        [RelayCommand]
        /// <summary>
        /// Displays the LibraryBitmapProperties dialog for the selected bitmap in the library.
        /// </summary>
        async Task BitmapProperties()
        {
            try {
                _workingCsXFLDoc = CsXFL.An.GetActiveDocument();
                if (_userLibrarySelection == null) { throw new Exception("No items are selected."); }
                if (_userLibrarySelection[0].ItemType != "bitmap")
                { 
                    throw new Exception("Selected item is not a bitmap.");
                }

                var _viewModelRegistry = ViewModelRegistry.Instance;
                _libraryViewModel = (LibraryViewModel)_viewModelRegistry.GetViewModel(nameof(LibraryViewModel));

                var dialog = new LibraryBitmapProperties(_userLibrarySelection[0]);
                var result = await DialogHost.Show(dialog);
                var dialogIdentifier = result as string;
                dialog.DialogIdentifier = dialogIdentifier!;

                var resultObject = result as dynamic;
                if (resultObject == null) { return; } // User cancelled

                // Do stuff lol

            } catch (Exception e) {
                await _genericDialogs.ShowError(e.Message);
            }
        }

        #endregion

        #region Generic Cmds
        [RelayCommand]
        /// <summary>
        /// Displays the LibrarySingleRename dialog for the selected item in the library.
        /// </summary>
        async Task Rename() // TODO: This should have a Find&Replace dialog for USR SEL > 1
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
        /// <summary>
        /// Deletes the selected item from the library. References the LibraryViewModel to perform the delete operation.
        /// </summary>
        async Task Delete()
        {
            var _viewModelRegistry = ViewModelRegistry.Instance;
            _libraryViewModel = (LibraryViewModel)_viewModelRegistry.GetViewModel(nameof(LibraryViewModel));
            _libraryViewModel.Delete();
        }

        [RelayCommand]
        async Task Copy()
        {

        }

        [RelayCommand]
        async Task Paste()
        {

        }

        [RelayCommand]
        async Task Duplicate()
        {
            _workingCsXFLDoc = CsXFL.An.GetActiveDocument();
            if (_workingCsXFLDoc == null) { throw new Exception("No document is open."); }
            if (_userLibrarySelection == null) { throw new Exception("No items are selected."); }

            foreach (var item in _userLibrarySelection)
            {

            }

            _eventAggregator.Publish(new LibraryItemsChangedEvent());
        }
        #endregion
    }

    public partial class LibraryViewModel : Tool
    {
        public ContextMenuFactory ContextMenuFactoryInstance { get; } = new ContextMenuFactory();
    }
}