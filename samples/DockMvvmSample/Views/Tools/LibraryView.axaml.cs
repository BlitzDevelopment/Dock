﻿using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Blitz.Events;
using Blitz.ViewModels;
using Blitz.ViewModels.Tools;
using NAudio.Wave;
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
using Blitz.ViewModels.Documents;

namespace Blitz.Views.Tools;

public partial class LibraryView : UserControl
{
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

        _eventAggregator = EventAggregator.Instance;
        _eventAggregator.Subscribe<ActiveDocumentChangedEvent>(OnActiveDocumentChanged);
        _eventAggregator.Subscribe<LibraryItemsChangedEvent>(OnLibraryItemsChanged);

        LibrarySearch.TextChanged += OnLibrary_searchTextChanged!;
        _libraryViewModel.PropertyChanged += OnLibraryViewModelPropertyChanged;

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

    private void ExpandFolderOnDrop(CsXFL.Item folder, IEnumerable<Blitz.Models.Tools.Library.LibraryItem> items, List<int> currentPath = null)
    {
        int localIndex = 0; // Tracks the index at the current level

        foreach (var item in items)
        {
            string itemPath = item.CsXFLItem!.Name;

            // Build the hierarchical path for the current item
            var hierarchicalPath = new List<int>(currentPath ?? new List<int>()) { localIndex };
            Console.WriteLine($"HierarchicalPath: {string.Join(" -> ", hierarchicalPath)}");

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
                var boundingBox = svg.Picture!.CullRect;

                // Translate and scale drawing canvas to fit SVG image
                canvas.Translate(canvas.LocalClipBounds.MidX, canvas.LocalClipBounds.MidY);
                canvas.Scale(0.9f * Math.Min(canvas.LocalClipBounds.Width / boundingBox.Width, canvas.LocalClipBounds.Height / boundingBox.Height));
                canvas.Translate(-boundingBox.MidX, -boundingBox.MidY);

                // Now finally draw the SVG image
                canvas.DrawPicture(svg.Picture);
            }
        }

        if (_libraryViewModel.Sound != null)
        {
            var audioData = _documentViewModel.GetAudioData(_libraryViewModel.Sound);

            var waveformWidth = 800; // Width of the waveform
            var waveformHeight = 200; // Height of the waveform
            var amplitudes = GetAudioAmplitudes(audioData, _libraryViewModel.Sound.SampleRate);
            var (waveformPicture, analogousColor, lighterColor) = GenerateWaveform(amplitudes, waveformWidth, waveformHeight);
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
        else
        {
            imageControl!.IsVisible = true;
        }

        var bitmapData = _documentViewModel.GetBitmapData(_libraryViewModel.Bitmap);

        // Use SixLabors.ImageSharp to load the image and convert it to a stream
        using (var image = SixLabors.ImageSharp.Image.Load(bitmapData))
        using (var memoryStream = new MemoryStream())
        {
            // Save the image as a PNG to the memory stream
            image.Save(memoryStream, new SixLabors.ImageSharp.Formats.Png.PngEncoder());
            memoryStream.Seek(0, SeekOrigin.Begin);

            // Set the MemoryStream directly to the Image control
            imageControl.Source = new Avalonia.Media.Imaging.Bitmap(memoryStream);
        }
        return;
    }

    // MARK: Audio Preview
    private (SkiaSharp.SKPicture Picture, SkiaSharp.SKColor AnalogousColor, SkiaSharp.SKColor LighterColor) GenerateWaveform(float[] amplitudes, int width, int height)
    {
        using var pictureRecorder = new SkiaSharp.SKPictureRecorder();
        var canvas = pictureRecorder.BeginRecording(new SkiaSharp.SKRect(0, 0, width, height));

        canvas.Clear(SkiaSharp.SKColors.Transparent);
        SKColor canvasColor = SKColor.Parse(_libraryViewModel.CanvasColor);

        // Two-tone waveform is an analogous color to the inverse color of the canvas.
        // This means there is always contrast against the background without being ugly.
        SKColor inverseColor = new SKColor(
            (byte)(255 - canvasColor.Red),
            (byte)(255 - canvasColor.Green),
            (byte)(255 - canvasColor.Blue),
            canvasColor.Alpha
        );

        SKColor analogousColor = new SKColor(
            (byte)((inverseColor.Red + 30) % 256),
            (byte)((inverseColor.Green + 15) % 256),
            (byte)((inverseColor.Blue - 20 + 256) % 256),
            inverseColor.Alpha
        );

        SKColor lighterColor = new SKColor(
            (byte)Math.Min(analogousColor.Red + 50, 255),
            (byte)Math.Min(analogousColor.Green + 50, 255),
            (byte)Math.Min(analogousColor.Blue + 50, 255),
            analogousColor.Alpha
        );

        var paint = new SkiaSharp.SKPaint
        {
            StrokeWidth = 1,
            IsAntialias = true
        };

        var centerY = height / 2;
        var step = (float)width / amplitudes.Length; // Stepsize for each sample

        // Draw the waveform with analogousColor
        paint.Color = analogousColor;
        for (int i = 0; i < amplitudes.Length - 1; i++)
        {
            var x1 = i * step;
            var y1 = centerY - amplitudes[i] * centerY;
            var x2 = (i + 1) * step;
            var y2 = centerY - amplitudes[i + 1] * centerY;

            canvas.DrawLine(x1, y1, x2, y2, paint);
        }

        // Draw a slightly less tall waveform with lighterColor
        float scale = 0.4f;
        paint.Color = lighterColor;
        for (int i = 0; i < amplitudes.Length - 1; i++)
        {
            var x1 = i * step;
            var y1 = centerY - amplitudes[i] * centerY * scale;
            var x2 = (i + 1) * step;
            var y2 = centerY - amplitudes[i + 1] * centerY * scale;

            canvas.DrawLine(x1, y1, x2, y2, paint);
        }

        var picture = pictureRecorder.EndRecording();
        return (picture, analogousColor, lighterColor);
    }

    private float[] GetAudioAmplitudes(byte[] audioData, int sampleRate = 1000)
    {
        using var ms = new MemoryStream(audioData);
        using var reader = new WaveFileReader(ms); // Use WaveFileReader for streams
        var samples = new List<float>();
        var buffer = new byte[sampleRate * reader.WaveFormat.BlockAlign]; // Adjust buffer size based on block align
        int read;

        while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
        {
            // Convert the byte buffer to float samples based on the audio format
            for (int i = 0; i < read; i += reader.WaveFormat.BlockAlign)
            {
                if (reader.WaveFormat.BitsPerSample == 16)
                {
                    // 16-bit PCM: Convert two bytes to a short and normalize
                    if (i + 2 <= read)
                    {
                        short sample = BitConverter.ToInt16(buffer, i);
                        samples.Add(sample / 32768f); // Normalize to range [-1, 1]
                    }
                }
                else if (reader.WaveFormat.BitsPerSample == 8)
                {
                    // 8-bit PCM: Normalize byte to range [-1, 1]
                    byte sample = buffer[i];
                    samples.Add((sample - 128) / 128f);
                }
                else if (reader.WaveFormat.BitsPerSample == 32)
                {
                    // 32-bit float PCM: Directly convert
                    if (i + 4 <= read)
                    {
                        float sample = BitConverter.ToSingle(buffer, i);
                        samples.Add(sample);
                    }
                }
                else
                {
                    throw new NotSupportedException("Unsupported bit depth: " + reader.WaveFormat.BitsPerSample);
                }
            }
        }
        return samples.ToArray();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}