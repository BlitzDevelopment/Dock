using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Blitz.Events;
using Blitz.ViewModels;
using Blitz.ViewModels.Tools;
using Svg.Skia;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Blitz.ViewModels.Documents;

namespace Blitz.Views.Tools;

public partial class LibraryView : UserControl
{
    private readonly IGenericDialogs _genericDialogs;
    private readonly AudioService _audioService;
    private readonly EventAggregator _eventAggregator;
    private LibraryViewModel _libraryViewModel;
    private MainWindowViewModel _mainWindowViewModel;
    private DocumentViewModel _documentViewModel;
    private Blitz.Models.Tools.Library.LibraryItem? _previousItem;
    private CsXFL.Document? _workingCsXFLDoc;
    private string? _searchText = "";
    private bool _useFlatSource = false;
    private readonly Stopwatch _stopwatch = new Stopwatch();
    private bool _isDragging = false;
    
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

        LibrarySearch.TextChanged += OnLibrary_searchTextChanged!;
        _libraryViewModel.PropertyChanged += OnLibraryViewModelPropertyChanged;

        //MARK: File Explorer D&D
        var treeView = FlatTreeView;
        
        // Enable drag-and-drop events
        treeView.SetValue(DragDrop.AllowDropProperty, true);
        treeView.AddHandler(DragDrop.DragOverEvent, FlatDoDrag);
        treeView.AddHandler(DragDrop.DropEvent, FlatDrop);
        treeView.AddHandler(DragDrop.DragLeaveEvent, FlatDragLeave);
        treeView.AddHandler(DragDrop.DragEnterEvent, FlatDragEnter);

        //MARK: Library D&D

        // Handle pointer pressed
        HierarchalTreeView.PointerPressed += (sender, e) =>
        {
            _stopwatch.Restart();
        };

        HierarchalTreeView.PointerMoved += (sender, e) =>
        {
            if (_stopwatch.Elapsed.TotalSeconds < 0.5) { return; }

            var position = e.GetPosition(HierarchalTreeView);
            var hitTestResult = HierarchalTreeView.InputHitTest(position);

            var pointerPoint = e.GetCurrentPoint(HierarchalTreeView);
            _isDragging = pointerPoint.Properties.IsLeftButtonPressed;

            Blitz.Models.Tools.Library.LibraryItem targetItem = default!;

            if (hitTestResult is Control control && control.DataContext is Blitz.Models.Tools.Library.LibraryItem item)
            {
                // Skip highlighting if the target item is in the current selection
                if (_libraryViewModel.UserLibrarySelection?.Contains(item.CsXFLItem) == true)
                {
                    return;
                }

                targetItem = item;

                // Highlight the folder if it's a valid target and dragging is active
                if (_isDragging && targetItem.CsXFLItem!.ItemType == "folder")
                {
                    if (_previousItem != null && _previousItem != targetItem)
                    {
                        _previousItem.IsDragOver = false;
                    }

                    targetItem.IsDragOver = true;
                    _previousItem = targetItem;
                }
            }
            else
            {
                // Reset IsDragOver for the last hovered item if no valid target is hit
                if (_previousItem != null)
                {
                    _previousItem.IsDragOver = false;
                    _previousItem = null;
                }
            }
        };

        HierarchalTreeView.PointerReleased += (sender, e) =>
        {
            _stopwatch.Restart();

            // Reset IsDragOver for the last hovered item
            if (_previousItem != null)
            {
                _previousItem.IsDragOver = false;
                _previousItem = null;
            }

            //Console.WriteLine("PointerReleased: Checking for Drop");

            var pointerPosition = e.GetPosition(HierarchalTreeView);
            var hitTestResult = HierarchalTreeView.InputHitTest(pointerPosition);
            if (hitTestResult is Control control && control.DataContext is Blitz.Models.Tools.Library.LibraryItem targetItem)
            {
                // Skip dropping if the target item is in the current selection
                if (_libraryViewModel.UserLibrarySelection?.Contains(targetItem.CsXFLItem) == true)
                {
                    //Console.WriteLine("PointerReleased: Cannot drop onto an item in the current selection.");
                    return;
                }

                if (targetItem.CsXFLItem!.ItemType != "folder") { return; }
                var folderName = targetItem.CsXFLItem!.Name;
                //Console.WriteLine($"PointerReleased: Dropped onto {targetItem.CsXFLItem!.Name}");

                foreach (var selectedItem in _libraryViewModel.UserLibrarySelection!)
                {
                    _workingCsXFLDoc!.Library.MoveToFolder(folderName, selectedItem);
                }
                ExpandFolderOnDrop(targetItem.CsXFLItem, _libraryViewModel.HierarchicalItems);
                _eventAggregator.Publish(new LibraryItemsChangedEvent());
            }
            else
            {
                //Console.WriteLine("PointerReleased: No valid drop target found.");
            }

            e.Handled = true;
        };

    }

    private void FlatDoDrag(object? sender, DragEventArgs e) 
    {
        e.DragEffects = DragDropEffects.Move;
    }

    private void FlatDrop(object? sender, DragEventArgs e)
    {
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

                if (anyFailures) { _genericDialogs.ShowWarning("One or more files could not be imported."); } // Show a warning if any file failed to import
            }
            else { _genericDialogs.ShowError("File not in valid format."); }
        } else {_genericDialogs.ShowError("DragDrop data does not contain FileName or FileNameW"); }
        _eventAggregator.Publish(new LibraryItemsChangedEvent());
    }

    private void FlatDragLeave(object? sender, DragEventArgs e)
    {
        e.Handled = true;
    }

    private void FlatDragEnter(object? sender, DragEventArgs e)
    {
        e.Handled = true;
    }

    private void ExpandFolderOnDrop(CsXFL.Item folder, IEnumerable<Blitz.Models.Tools.Library.LibraryItem> items, List<int> currentPath = null)
    {
        int localIndex = 0; // Tracks the index at the current level

        foreach (var item in items)
        {
            string itemPath = item.CsXFLItem!.Name;

            // Build the hierarchical path for the current item
            var hierarchicalPath = new List<int>(currentPath ?? new List<int>()) { localIndex };

            // Check if the current item matches the folder name
            if (item.CsXFLItem == folder)
            {
                _libraryViewModel.HierarchicalSource!.Expand(new Avalonia.Controls.IndexPath(hierarchicalPath.ToArray()));
            }

            //Recursively check and expand child items
            if (item.Children != null && item.Children.Any())
            {
                ExpandFolderOnDrop(folder, item.Children, hierarchicalPath);
            }

            // Increment the local index for the next sibling
            localIndex++;
        }
    }

    // MARK: Event Handlers
    private void OnLibraryViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LibraryViewModel.Bitmap)) { UpdateBitmapPreview(); }
        if (e.PropertyName == nameof(LibraryViewModel.Sound)) { LibrarySVGPreview.Invalidate(); }
        if (e.PropertyName == nameof(LibraryViewModel.SvgData)) { LibrarySVGPreview.Invalidate(); }
    }

    private void OnLibraryItemsChanged(LibraryItemsChangedEvent e)
    {
        FilterAndUpdateFlatLibrary(_searchText!);
    }

    private void OnActiveDocumentChanged(ActiveDocumentChangedEvent e)
    {
        _documentViewModel = e.Document;
        _workingCsXFLDoc = CsXFL.An.GetDocument(e.Document.DocumentIndex!.Value);

        // Rebuild the FlatLibrary with the current search parameters
        if (_libraryViewModel != null && _workingCsXFLDoc != null)
        {
            FilterAndUpdateFlatLibrary(_searchText!);
        }
        
    }

    public void OnLibrary_searchTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_workingCsXFLDoc == null) { return; }

        string _searchText = "";
        var textBox = sender as TextBox;
        if (textBox != null) { _searchText = textBox.Text!; }

        // Handle illegal input
        if (_searchText.Contains('/') || _searchText.Contains('\\'))
        {
            if (textBox == null) { return; }
            
            Flyout flyout = new Flyout();
            flyout.Content = new TextBlock { Text = "Illegal characters '/' or '\\' are not allowed." };
            flyout.ShowAt(textBox);

            // Todo: Don't use Task.Delay
            // Dismiss the Flyout after 3 seconds
            Task.Delay(3000).ContinueWith(_ => 
            {
                flyout.Hide();
            }, TaskScheduler.FromCurrentSynchronizationContext());
            textBox.Text = new string(_searchText.Where(c => c != '/' && c != '\\').ToArray());
        }

        FilterAndUpdateFlatLibrary(_searchText);
    }

    private void FilterAndUpdateFlatLibrary(string _searchText)
    {
        if (string.IsNullOrEmpty(_searchText))
        {
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

    // MARK: Symbol Preview
    // Todo: Double check performance here for SKXamlCanvas, we shouldn't have a stuttering issue with such basic vectors
    // This is almost certainly due to getting the SVG every time the canvas is invalidated.
    /// <summary>
    /// Handles the library preview canvas and renders an SVG image onto it.
    /// </summary>
    private void OnCanvasPaint(object sender, SKPaintSurfaceEventArgs e)
    {
        if (_libraryViewModel == null || _workingCsXFLDoc == null) { return; }
        
        var canvas = e.Surface.Canvas;

        if (_libraryViewModel.SvgData != null)
        {
            using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(_libraryViewModel.SvgData.ToString())))
            {
                var svg = new SKSvg();
                svg.Load(stream);

                // Get bounding rectangle for SVG image
                var boundingBox = _libraryViewModel.BoundingBox; 

                // Calculate the width, height, and center of the bounding box
                var boundingBoxWidth = boundingBox.Right - boundingBox.Left;
                var boundingBoxHeight = boundingBox.Top - boundingBox.Bottom;
                var boundingBoxCenterX = boundingBox.Left + boundingBoxWidth / 2;
                var boundingBoxCenterY = boundingBox.Bottom + boundingBoxHeight / 2;

                // Translate and scale drawing canvas to fit SVG image
                canvas.Translate(canvas.LocalClipBounds.MidX, canvas.LocalClipBounds.MidY);
                canvas.Scale(0.9f * (float)Math.Min(canvas.LocalClipBounds.Width / boundingBoxWidth, canvas.LocalClipBounds.Height / boundingBoxHeight));
                canvas.Translate((float)-boundingBoxCenterX, (float)-boundingBoxCenterY);

                // Now finally draw the SVG image
                canvas.DrawPicture(svg.Picture);
            }
        }

        if (_libraryViewModel.Sound != null)
        {
            var audioData = _documentViewModel.GetAudioData(_libraryViewModel.Sound);

            Console.WriteLine($"Audio data length: {audioData.Length}");

            var waveformWidth = 800; // Width of the waveform
            var waveformHeight = 200; // Height of the waveform
            var amplitudes = _audioService.GetAudioAmplitudes(audioData, 16, 1);
            var (waveformPicture, analogousColor, lighterColor) = _audioService.GenerateWaveform(amplitudes, waveformWidth, waveformHeight, _libraryViewModel.CanvasColor!);
            var canvasWidth = e.Info.Width;
            var canvasHeight = e.Info.Height;
            var centerX = canvasWidth / 2f;
            var centerY = canvasHeight / 2f;
            var offsetX = centerX - (waveformWidth / 2f);
            var offsetY = centerY - (waveformHeight / 2f);

            canvas.Save();
            canvas.Translate(offsetX, offsetY);
            canvas.DrawPicture(waveformPicture);
            canvas.Restore();
        }
    }

    // MARK: Bitmap Preview
    private void UpdateBitmapPreview()
    {
        var imageControl = this.FindControl<Avalonia.Controls.Image>("LibraryBitmapPreview");

        if (_libraryViewModel.Bitmap == null)
        {
            imageControl!.IsVisible = false;
            return;
        }

        imageControl!.IsVisible = true;

        var bitmapData = _documentViewModel.GetBitmapData(_libraryViewModel.Bitmap);
        var correctImage = CsXFL.ImageUtils.ConvertDatToRawImage(bitmapData);

        // Directly use the bitmap data if it's already in a compatible format
        using (var memoryStream = new MemoryStream())
        {
            correctImage.Save(memoryStream, new SixLabors.ImageSharp.Formats.Png.PngEncoder()); // Save as PNG
            memoryStream.Seek(0, SeekOrigin.Begin); // Reset the stream position

            // Set the MemoryStream directly to the Image control
            imageControl.Source = new Avalonia.Media.Imaging.Bitmap(memoryStream);
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}