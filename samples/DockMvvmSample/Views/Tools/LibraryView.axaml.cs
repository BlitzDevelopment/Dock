using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Blitz.Events;
using Blitz.ViewModels;
using Blitz.ViewModels.Documents;
using Blitz.ViewModels.Tools;
using SixLabors.ImageSharp;
using SkiaSharp;
using Svg.Skia;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Blitz.Views.Tools;

public partial class LibraryView : UserControl
{
    #region Dependencies
    private readonly AudioService _audioService;
    private readonly EventAggregator _eventAggregator;
    private readonly IGenericDialogs? _genericDialogs;
    #endregion

    #region ViewModels
    private DocumentViewModel? _documentViewModel;
    private LibraryViewModel _libraryViewModel;
    private MainWindowViewModel _mainWindowViewModel;
    #endregion

    #region State
    private Blitz.Models.Tools.Library.LibraryItem? _previousItem;
    private CsXFL.Document? _workingCsXFLDoc;
    private readonly Stopwatch _stopwatch = new Stopwatch();
    private string? _searchText = "";
    private bool _useFlatSource = false;
    private System.Timers.Timer? _hoverTimer;
    #endregion

    #region Cached Data
    private SKPicture? _cachedSvgPicture;
    private SKPicture? _cachedWaveformPicture;
    #endregion
    
    #region Constructor
    public LibraryView()
    {
        InitializeComponent();
        _workingCsXFLDoc = null;

        var _viewModelRegistry = ViewModelRegistry.Instance;
        _libraryViewModel = (LibraryViewModel)_viewModelRegistry.GetViewModel(nameof(LibraryViewModel));
        _mainWindowViewModel = (MainWindowViewModel)_viewModelRegistry.GetViewModel(nameof(MainWindowViewModel));

        _audioService = AudioService.Instance;
        _eventAggregator = EventAggregator.Instance;
        _eventAggregator.Subscribe<ActiveDocumentChangedEvent>(OnActiveDocumentChanged);
        _eventAggregator.Subscribe<LibraryItemsChangedEvent>(OnLibraryItemsChanged);

        LibrarySearch.TextChanged += OnLibrary_SearchTextChanged!;
        _libraryViewModel.PropertyChanged += OnLibraryViewModelPropertyChanged;

        // Allow drag-and-drop into HierarchalTreeView
        HierarchalTreeView.SetValue(DragDrop.AllowDropProperty, true);
        HierarchalTreeView.AddHandler(DragDrop.DragOverEvent, OnDragOver);
        HierarchalTreeView.AddHandler(DragDrop.DropEvent, OnDrop);
        HierarchalTreeView.AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        HierarchalTreeView.AddHandler(DragDrop.DragEnterEvent, OnDragEnter);

        HierarchalTreeView.PointerPressed += (sender, e) =>
        {
            // Ensure the event is handled even if the item is not selected
            e.Handled = true;

            // Check if the right mouse button was pressed
            if (e.GetCurrentPoint(HierarchalTreeView).Properties.IsRightButtonPressed)
            {
                return; // Allow the context menu to appear
            }

            // Handle left mouse button for drag-and-drop
            if (e.GetCurrentPoint(HierarchalTreeView).Properties.IsLeftButtonPressed)
            {
                var position = e.GetPosition(HierarchalTreeView);
                var hitTestResult = HierarchalTreeView.InputHitTest(position);

                if (hitTestResult is Control control && control.DataContext is Blitz.Models.Tools.Library.LibraryItem item)
                {
                    try
                    {
                        // Start the drag operation
                        var data = new DataObject();
                        data.Set("DraggedItem", item);

                        // Ensure the DataObject is valid before starting the drag operation
                        if (data.Contains("DraggedItem"))
                        {
                            DragDrop.DoDragDrop(e, data, DragDropEffects.Move);
                        }
                    }
                    catch (System.Runtime.InteropServices.COMException ex)
                    {
                        Debug.WriteLine($"Drag-and-drop operation failed: {ex.Message}");
                    }
                }
            }
        };
         
        // Drag-and-drop into FlatTreeView
        FlatTreeView.SetValue(DragDrop.AllowDropProperty, true);
        FlatTreeView.AddHandler(DragDrop.DragOverEvent, FlatDoDrag);
        FlatTreeView.AddHandler(DragDrop.DropEvent, FlatDrop);
        FlatTreeView.AddHandler(DragDrop.DragLeaveEvent, FlatDragLeave);
        FlatTreeView.AddHandler(DragDrop.DragEnterEvent, FlatDragEnter);
    }
    #endregion

    #region Drag-and-Drop
    void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = DragDropEffects.None;

        // Check if the drag data contains file paths (file explorer drag-drop)
        if (e.Data.Contains("FileNameW") || e.Data.Contains("FileName"))
        {
            e.DragEffects = DragDropEffects.Copy; // Allow copying files
        }
        else
        {
            // Existing logic for internal rearranging
            var position = e.GetPosition(HierarchalTreeView);
            var hitTestResult = HierarchalTreeView.InputHitTest(position);

            if (hitTestResult is Control control && control.DataContext is Blitz.Models.Tools.Library.LibraryItem targetItem)
            {
                if (targetItem.CsXFLItem?.ItemType == "folder")
                {
                    e.DragEffects = DragDropEffects.Move;

                    if (_previousItem != null && _previousItem != targetItem)
                    {
                        _previousItem.IsDragOver = false;
                        _hoverTimer?.Stop();
                        _hoverTimer = null;
                    }

                    targetItem.IsDragOver = true;

                    if (_previousItem != targetItem)
                    {
                        _hoverTimer = new System.Timers.Timer(500);
                        _hoverTimer.Elapsed += (s, args) =>
                        {
                            _hoverTimer?.Stop();
                            _hoverTimer = null;

                            Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                if (_previousItem == targetItem)
                                {
                                    ExpandFolderOnDrop(targetItem.CsXFLItem, _libraryViewModel.HierarchicalItems);
                                }
                            });
                        };
                        _hoverTimer.Start();
                    }

                    _previousItem = targetItem;
                }
            }
        }

        e.Handled = true;
    }

    async void OnDrop(object? sender, DragEventArgs e)
    {
        if (_previousItem != null)
        {
            _previousItem.IsDragOver = false;
            _previousItem = null;
        }

        // Handle file explorer drag-drop
        if (e.Data.Contains("FileNameW") || e.Data.Contains("FileName"))
        {
            if (_workingCsXFLDoc == null) { return; }

            var files = e.Data.GetFileNames();
            var validExtensions = new[] { ".png", ".jpg", ".gif", ".mp3", ".wav", ".flac" };

            if (files.Any(file => validExtensions.Any(ext => file.EndsWith(ext, StringComparison.OrdinalIgnoreCase))))
            {
                bool anyFailures = false;
                foreach (var file in files)
                {
                    bool didWork = _workingCsXFLDoc.ImportFile(file);
                    if (!didWork) { anyFailures = true; }
                }

                if (anyFailures)
                {
                    await _genericDialogs!.ShowWarning("One or more files could not be imported.");
                }
            }
            else
            {
                await _genericDialogs!.ShowError("File not in valid format.");
            }

            _eventAggregator.Publish(new LibraryItemsChangedEvent());
        }
        else
        {
            // Existing logic for internal rearranging
            if (e.Data.Contains("DraggedItem") && e.Data.Get("DraggedItem") is Blitz.Models.Tools.Library.LibraryItem draggedItem)
            {
                var position = e.GetPosition(HierarchalTreeView);
                var hitTestResult = HierarchalTreeView.InputHitTest(position);

                if (hitTestResult is Control control && control.DataContext is Blitz.Models.Tools.Library.LibraryItem targetItem)
                {
                    if (_libraryViewModel.UserLibrarySelection?.Contains(targetItem.CsXFLItem) == true)
                    {
                        return;
                    }

                    if (targetItem.CsXFLItem!.ItemType == "folder")
                    {
                        var folderName = targetItem.CsXFLItem.Name;

                        foreach (var selectedItem in _libraryViewModel.UserLibrarySelection!)
                        {
                            _workingCsXFLDoc!.Library.MoveToFolder(folderName, selectedItem);
                        }

                        ExpandFolderOnDrop(targetItem.CsXFLItem, _libraryViewModel.HierarchicalItems);
                        _eventAggregator.Publish(new LibraryItemsChangedEvent());
                    }
                }
            }
        }

        e.Handled = true;
    }

    void OnDragLeave(object? sender, DragEventArgs e)
    {
        if (_previousItem != null)
        {
            _previousItem.IsDragOver = false;
            _previousItem = null;
        }

        e.Handled = true;
    }

    void OnDragEnter(object? sender, DragEventArgs e)
    {
        e.Handled = true;
    }

    void FlatDoDrag(object? sender, DragEventArgs e) 
     {
         e.DragEffects = DragDropEffects.Move;
     }
 
     async void FlatDrop(object? sender, DragEventArgs e)
     {
        if (_workingCsXFLDoc == null) { return; }
        // Check if the dragged data contains files using "FileNameW" or "FileName"
        if (e.Data.Contains("FileNameW") || e.Data.Contains("FileName"))
        {
            var files = e.Data.GetFileNames();
            var validExtensions = new[] { ".png", ".jpg", ".gif", ".mp3", ".wav", ".flac" };
            if (files.Any(file => validExtensions.Any(ext => file.EndsWith(ext, StringComparison.OrdinalIgnoreCase))))
            {
                bool anyFailures = false; // Track if any file fails to import
                foreach (var file in files)
                {
                    bool didWork = _workingCsXFLDoc.ImportFile(file);
                    if (!didWork) {anyFailures = true;} // Mark failure
                }

                if (anyFailures) { await _genericDialogs!.ShowWarning("One or more files could not be imported."); } // Show a warning if any file failed to import
            }
            else { await _genericDialogs!.ShowError("File not in valid format."); }
        } else { await _genericDialogs!.ShowError("DragDrop data does not contain FileName or FileNameW"); }
        _eventAggregator.Publish(new LibraryItemsChangedEvent());
     }
 
     void FlatDragLeave(object? sender, DragEventArgs e)
     {
         e.Handled = true;
     }
 
     void FlatDragEnter(object? sender, DragEventArgs e)
     {
         e.Handled = true;
     }

    void ExpandFolderOnDrop(CsXFL.Item folder, IEnumerable<Blitz.Models.Tools.Library.LibraryItem> items, List<int>? currentPath = null)
    {
        int localIndex = 0; // Tracks the index at the current level

        foreach (var item in items)
        {
            // Check if the current item's CsXFLItem matches the folder
            if (item.CsXFLItem == folder)
            {
                // Build the hierarchical path for the current item
                var hierarchicalPath = new List<int>(currentPath ?? new List<int>()) { localIndex };

                // Expand the matching folder
                _libraryViewModel.HierarchicalSource!.Expand(new Avalonia.Controls.IndexPath(hierarchicalPath.ToArray()));
                return; // Stop further recursion as the folder is found
            }

            // Recursively check and expand child items
            if (item.Children != null && item.Children.Any())
            {
                var hierarchicalPath = new List<int>(currentPath ?? new List<int>()) { localIndex };
                ExpandFolderOnDrop(folder, item.Children, hierarchicalPath);
            }

            // Increment the local index for the next sibling
            localIndex++;
        }
    }
    #endregion

    #region Event Handlers
    void OnLibraryViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LibraryViewModel.SvgData))
        {
            LibrarySVGPreview.IsVisible = true;
            _cachedSvgPicture = null;
            LibrarySVGPreview.Invalidate();
        }
        else if (e.PropertyName == nameof(LibraryViewModel.Sound))
        {
            LibrarySVGPreview.IsVisible = true;
            _cachedWaveformPicture = null;
            LibrarySVGPreview.Invalidate();
        }
        else if (e.PropertyName == nameof(LibraryViewModel.Bitmap))
        {
            LibrarySVGPreview.IsVisible = false;
            UpdateBitmapPreview();
        }
    }

    void OnLibraryItemsChanged(LibraryItemsChangedEvent e)
    {
        FilterAndUpdateFlatLibrary(_searchText!);
    }

    void OnActiveDocumentChanged(ActiveDocumentChangedEvent e)
    {
        _documentViewModel = e.Document;
        _workingCsXFLDoc = CsXFL.An.GetDocument(e.Document.DocumentIndex!.Value);

        // Rebuild the FlatLibrary with the current search parameters
        if (_libraryViewModel != null && _workingCsXFLDoc != null)
        {
            FilterAndUpdateFlatLibrary(_searchText!);
        }
        
    }

    void OnLibrary_SearchTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_workingCsXFLDoc == null) { return; }

        var textBox = sender as TextBox;
        if (textBox != null) { _searchText = textBox.Text!; }

        // Handle illegal input
        if (_searchText.Contains('/') || _searchText.Contains('\\'))
        {
            if (textBox == null) { return; }
            
            Flyout flyout = new Flyout();
            flyout.Content = new TextBlock { Text = "Illegal characters '/' or '\\' are not allowed." };
            flyout.ShowAt(textBox);

            Task.Delay(3000).ContinueWith(_ => 
            {
                flyout.Hide();
            }, TaskScheduler.FromCurrentSynchronizationContext());
            textBox.Text = new string(_searchText.Where(c => c != '/' && c != '\\').ToArray());
        }

        FilterAndUpdateFlatLibrary(_searchText);
    }

    void OnClearButtonClick(object? sender, RoutedEventArgs e)
    {
        LibrarySearch.Text = string.Empty;
        FilterAndUpdateFlatLibrary(string.Empty);
    }
    #endregion

    #region UI Logic
    void FilterAndUpdateFlatLibrary(string _searchText)
    {
        if (string.IsNullOrEmpty(_searchText))
        {
            _libraryViewModel.ItemCount = _workingCsXFLDoc?.Library.Items.Count.ToString() + " Items" ?? "-";
            _useFlatSource = false;
            HierarchalTreeView.IsVisible = true;
            HierarchalTreeView.RowSelection!.Clear();
            _libraryViewModel.UserLibrarySelection = null;
            FlatTreeView.IsVisible = false;
            FlatTreeView.RowSelection!.Clear();
        }
        else
        {
            if (!_useFlatSource)
            {
                _useFlatSource = true;
                HierarchalTreeView.IsVisible = false;
                HierarchalTreeView.RowSelection!.Clear();
                _libraryViewModel.UserLibrarySelection = null;
                FlatTreeView.IsVisible = true;
                FlatTreeView.RowSelection!.Clear();
                FlatTreeView.Source = _libraryViewModel.FlatSource;
            }

            // Filter items based on the search text and exclude folders
            var filteredItems = _libraryViewModel.Items
                .Where(item => Path.GetFileName(item.Name).Contains(_searchText, StringComparison.OrdinalIgnoreCase)
                            && item.Type != "Folder")
                .ToList();

            _libraryViewModel.ItemCount = filteredItems.Count == 1 
                ? "1 Result" 
                : filteredItems.Count + " Results";

            // Update FlatSource directly
            _libraryViewModel.FlatSource = new FlatTreeDataGridSource<Blitz.Models.Tools.Library.LibraryItem>(new ObservableCollection<Blitz.Models.Tools.Library.LibraryItem>(filteredItems))
            {
                Columns =
                {
                    new TextColumn<Blitz.Models.Tools.Library.LibraryItem, string>("Name", x => Path.GetFileName(x.Name)),
                    new TextColumn<Blitz.Models.Tools.Library.LibraryItem, string>("Type", x => x.Type),
                    new TextColumn<Blitz.Models.Tools.Library.LibraryItem, string>("Use Count", x => x.UseCount),
                },
            };
            _libraryViewModel.FlatSource.RowSelection!.SingleSelect = false;
            FlatTreeView.Source = _libraryViewModel.FlatSource;

            FlatTreeView.RowSelection!.SelectionChanged += (sender, e) =>
            {
                var selectedItems = FlatTreeView.RowSelection.SelectedItems.OfType<Blitz.Models.Tools.Library.LibraryItem>();
                _libraryViewModel.UserLibrarySelection = selectedItems.Select(item => item.CsXFLItem!).ToArray();
            };
        }
    }

    // MARK: SVG & Sound Preview
    void OnCanvasPaint(object sender, SKPaintSurfaceEventArgs e)
    {
        if (_libraryViewModel == null || _workingCsXFLDoc == null) { return; }
        
        var canvas = e.Surface.Canvas;

        if (_libraryViewModel.SvgData != null)
        {
            if (_cachedSvgPicture == null)
            {
                using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(_libraryViewModel.SvgData.ToString())))
                {
                    var svg = new SKSvg();
                    svg.Load(stream);
                    _cachedSvgPicture = svg.Picture;
                }
            }

            // Use the cached picture
            var boundingBox = _libraryViewModel.BoundingBox!;
            var boundingBoxWidth = boundingBox.Right - boundingBox.Left;
            var boundingBoxHeight = boundingBox.Top - boundingBox.Bottom;
            var boundingBoxCenterX = boundingBox.Left + boundingBoxWidth / 2;
            var boundingBoxCenterY = boundingBox.Bottom + boundingBoxHeight / 2;

            canvas.Translate(canvas.LocalClipBounds.MidX, canvas.LocalClipBounds.MidY);
            canvas.Scale(0.9f * (float)Math.Min(canvas.LocalClipBounds.Width / boundingBoxWidth, canvas.LocalClipBounds.Height / boundingBoxHeight));
            canvas.Translate((float)-boundingBoxCenterX, (float)-boundingBoxCenterY);

            canvas.DrawPicture(_cachedSvgPicture);
        }

        if (_libraryViewModel.Sound != null)
        {
            canvas.ResetMatrix();

            if (_cachedWaveformPicture == null)
            {
                var fileExtension = Path.GetExtension(_libraryViewModel.Sound.Href)?.TrimStart('.').ToLower() ?? string.Empty;
                var audioData = _documentViewModel!.DecryptAudioDat(_libraryViewModel.Sound);

                if (fileExtension == "wav" || fileExtension == "flac")
                {
                    var amplitudes = _audioService.GetAudioAmplitudes(audioData, 16, 1);
                    (_cachedWaveformPicture, _, _) = _audioService.GenerateWaveform(amplitudes, 800, 200, _libraryViewModel.CanvasColor!);
                }
                else if (fileExtension == "mp3")
                {
                    var pcmData = _audioService.DecodeMp3ToWav(audioData);
                    var amplitudes = _audioService.GetAudioAmplitudes(pcmData, 16, 1);
                    (_cachedWaveformPicture, _, _) = _audioService.GenerateWaveform(amplitudes, 800, 200, _libraryViewModel.CanvasColor!);
                }
            }

            if (_cachedWaveformPicture != null) // Ensure the picture is not null
            {
                var canvasWidth = e.Info.Width;
                var canvasHeight = e.Info.Height;

                // Calculate the horizontal scaling factor
                var scaleX = canvasWidth / 800f;

                // Center vertically
                var offsetY = (canvasHeight - (200 * scaleX)) / 2f;

                canvas.Save();
                canvas.Scale(scaleX, scaleX); // Apply horizontal scaling
                canvas.Translate(0, offsetY / scaleX); // Adjust vertical offset after scaling
                canvas.DrawPicture(_cachedWaveformPicture);
                canvas.Restore();
            }
            else
            {
                Console.WriteLine("Failed to generate waveform picture.");
            }
        }
    }

    // MARK: Bitmap Preview
    void UpdateBitmapPreview()
    {
        var imageControl = this.FindControl<Avalonia.Controls.Image>("LibraryBitmapPreview");

        if (_libraryViewModel.Bitmap == null)
        {
            imageControl!.IsVisible = false;
            return;
        }

        imageControl!.IsVisible = true;

        // Retrieve the cached bitmap data
        var bitmapData = _documentViewModel!.GetBitmapData(_libraryViewModel.Bitmap);

        // Directly set the cached bitmap data to the Image control
        using (var memoryStream = new MemoryStream(bitmapData))
        {
            memoryStream.Seek(0, SeekOrigin.Begin); // Ensure the stream is at the beginning
            imageControl.Source = new Avalonia.Media.Imaging.Bitmap(memoryStream);
        }
    }
    #endregion

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}