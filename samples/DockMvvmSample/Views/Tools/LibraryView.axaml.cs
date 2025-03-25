using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Svg.Skia;
using Blitz.ViewModels.Tools;
using Blitz.Events;
using System.IO;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using Avalonia.Controls.Models.TreeDataGrid;
using System.ComponentModel;
using System.Collections.Generic;
using CsXFL;
using SkiaSharp;
using System.IO.Compression;

using NAudio.Wave;

namespace Blitz.Views.Tools;

// Improvement:
// private readonly Dictionary<string, byte[]> _bitmapCache = new();
// When should we dispose the bitmap cache? OnDocumentClosed? Does a bitmap's href ever change?
// One ZipArchive per document, dispose OnDocumentClosed
public partial class LibraryView : UserControl
{
    private readonly EventAggregator _eventAggregator;
    private LibraryViewModel _libraryViewModel { set; get; }
    private CsXFL.Document? WorkingCsXFLDoc;
    private string? SearchText = "";
    private bool UseFlatSource = false;
    
    public LibraryView()
    {
        InitializeComponent();
        var _viewModelRegistry = ViewModelRegistry.Instance;
        _libraryViewModel = (LibraryViewModel)_viewModelRegistry.GetViewModel(nameof(LibraryViewModel));
        _eventAggregator = EventAggregator.Instance;
        _eventAggregator.Subscribe<ActiveDocumentChangedEvent>(OnActiveDocumentChanged);
        WorkingCsXFLDoc = null;

        LibrarySearch.TextChanged += OnLibrarySearchTextChanged!;
        _libraryViewModel.PropertyChanged += OnLibraryViewModelPropertyChanged;
    }

    private void OnLibraryViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LibraryViewModel.Bitmap)) { UpdateBitmapPreview(); }
        if (e.PropertyName == nameof(LibraryViewModel.Sound)) { LibrarySVGPreview.Invalidate(); }
    }

    private void OnActiveDocumentChanged(ActiveDocumentChangedEvent e)
    {
        WorkingCsXFLDoc = CsXFL.An.GetDocument(e.Index)!;

        // Rebuild the FlatLibrary with the current search parameters
        if (_libraryViewModel != null && WorkingCsXFLDoc != null)
        {
            FilterAndUpdateFlatLibrary(SearchText!);
        }
    }

    /// <summary>
    /// Filters the library items based on the provided search text and updates the flat library view.
    /// Handles the text changed event for the library search TextBox.
    /// Updates the library view based on the search text entered by the user.
    /// </summary>
    /// <param name="sender">The source of the event, typically the TextBox where the text is being entered.</param>
    /// <param name="e">The event arguments containing information about the text change.</param>
    /// <remarks>
    /// - If the search text contains illegal characters ('/' or '\\'), a Flyout is displayed to notify the user,
    ///   and the illegal characters are removed from the TextBox.
    /// - If the search text is empty, the hierarchical tree view is displayed, and the flat tree view is hidden.
    /// - If the search text is not empty, the flat tree view is displayed with filtered items based on the search text.
    /// - The filtering is case-insensitive and matches the search text against the file name of each library item.
    /// </remarks>
    public void OnLibrarySearchTextChanged(object sender, TextChangedEventArgs e)
    {
        if (WorkingCsXFLDoc == null) { return; }

        string searchText = "";
        var textBox = sender as TextBox;
        if (textBox != null) { searchText = textBox.Text!; }
        SearchText = searchText;

        // Handle illegal input
        if (searchText.Contains('/') || searchText.Contains('\\'))
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
            textBox.Text = new string(searchText.Where(c => c != '/' && c != '\\').ToArray());
        }

        FilterAndUpdateFlatLibrary(searchText);
    }

    /// <summary>
    /// Filters and updates the flat library view based on the provided search text.
    /// </summary>
    /// <param name="searchText">The text to filter the library items. If null or empty, the hierarchical view is displayed instead.</param>
    /// <remarks>
    /// This method performs the following actions:
    /// - If the search text is null or empty:
    ///   - Disables the flat source.
    ///   - Displays the hierarchical tree view and clears its selection.
    ///   - Hides the flat tree view and clears its selection.
    ///   - Resets the user's library selection in the view model.
    /// - If the search text is not empty:
    ///   - Enables the flat source if not already enabled.
    ///   - Hides the hierarchical tree view and clears its selection.
    ///   - Displays the flat tree view and clears its selection.
    ///   - Filters the library items based on the search text, excluding folders.
    ///   - Updates the flat source with the filtered items and configures the columns for the flat tree view.
    /// </remarks>
    private void FilterAndUpdateFlatLibrary(string searchText)
    {
        if (string.IsNullOrEmpty(searchText))
        {
            UseFlatSource = false;
            HierarchalTreeView.IsVisible = true;
            HierarchalTreeView.RowSelection!.Clear();
            _libraryViewModel.UserLibrarySelection = null;
            FlatTreeView.IsVisible = false;
            FlatTreeView.RowSelection!.Clear();
        }
        else
        {
            if (!UseFlatSource)
            {
                UseFlatSource = true;
                HierarchalTreeView.IsVisible = false;
                HierarchalTreeView.RowSelection!.Clear();
                _libraryViewModel.UserLibrarySelection = null;
                FlatTreeView.IsVisible = true;
                FlatTreeView.RowSelection!.Clear();
                FlatTreeView.Source = _libraryViewModel.FlatSource;
            }

            // Filter items based on the search text and exclude folders
            var filteredItems = _libraryViewModel.Items
                .Where(item => Path.GetFileName(item.Name).Contains(searchText, StringComparison.OrdinalIgnoreCase)
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
        }
    }

    // Todo: Double check performance here for SKXamlCanvas, we shouldn't have a stuttering issue with such basic vectors
    // This is almost certainly due to getting the SVG every time the canvas is invalidated.
    /// <summary>
    /// Handles the library preview canvas and renders an SVG image onto it.
    /// </summary>
    private void OnCanvasPaint(object sender, SKPaintSurfaceEventArgs e)
    {
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
            string audioFilePath = (_libraryViewModel.Sound as SoundItem)!.Href;

            if (string.IsNullOrEmpty(audioFilePath))
            {
                return;
            }

            byte[] audioData;

            if (WorkingCsXFLDoc.IsXFL)
            {
                // If the document is not a Zip archive, read the audio file directly
                string fullPath = Path.Combine(Path.GetDirectoryName(WorkingCsXFLDoc.Filename)!, Library.LIBRARY_PATH, audioFilePath);
                if (!File.Exists(fullPath))
                {
                    throw new FileNotFoundException($"Audio file not found: {fullPath}");
                }
                audioData = File.ReadAllBytes(fullPath);
            }
            else
            {
                // If the document is a Zip archive, extract the audio file
                using (ZipArchive archive = ZipFile.Open(WorkingCsXFLDoc.Filename, ZipArchiveMode.Read))
                {
                    string zipPath = Path.Combine(Library.LIBRARY_PATH, audioFilePath).Replace("\\", "/");
                    ZipArchiveEntry? entry = archive.GetEntry(zipPath);

                    if (entry is null)
                    {
                        // Try to find the entry by normalizing slashes
                        zipPath = zipPath.Replace('/', '\\').Replace('\\', '_');
                        entry = archive.Entries.Where(x => x.FullName.Replace('/', '\\').Replace('\\', '_') == zipPath).FirstOrDefault();
                        if (entry is null)
                        {
                            throw new Exception($"Audio file not found in archive: {zipPath}");
                        }
                    }

                    using (MemoryStream ms = new MemoryStream())
                    {
                        entry.Open().CopyTo(ms);
                        audioData = ms.ToArray();
                    }
                }
            }

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

    /// <summary>
    /// Retrieves the binary data of a bitmap image from the file system or a ZIP archive.
    /// </summary>
    private byte[] GetBitmapData(BitmapItem bitmap)
    {
        if (WorkingCsXFLDoc.IsXFL)
        {
            string imgPath = Path.Combine(Path.GetDirectoryName(WorkingCsXFLDoc.Filename)!, Library.LIBRARY_PATH, (bitmap as BitmapItem)!.Href);
            byte[] data = File.ReadAllBytes(imgPath);
            return data;
        }
        else
        {
            using (ZipArchive archive = ZipFile.Open(WorkingCsXFLDoc.Filename, ZipArchiveMode.Read))
            {
                string imgPath = Path.Combine(Library.LIBRARY_PATH, (bitmap as BitmapItem)!.Href).Replace("\\", "/");
                ZipArchiveEntry? entry = archive.GetEntry(imgPath);
                if (entry is null)
                {
                    // try to find it while removing slashes from both paths
                    imgPath = imgPath.Replace('/', '\\').Replace('\\', '_');
                    entry = archive.Entries.Where(x => x.FullName.Replace('/', '\\').Replace('\\', '_') == imgPath).FirstOrDefault();
                    if (entry is null) throw new Exception($"Bitmap not found: {imgPath}");
                }
                using (MemoryStream ms = new MemoryStream())
                {
                    entry.Open().CopyTo(ms);
                    byte[] imageData = ms.ToArray();
                    return imageData;
                }
            }
        }
    }

    private void UpdateBitmapPreview()
    {
        var imageControl = this.FindControl<Avalonia.Controls.Image>("LibraryBitmapPreview");

        if (_libraryViewModel.Bitmap == null)
        {
            imageControl.IsVisible = false;
            return;
        }
        else
        {
            imageControl.IsVisible = true;
        }

        var bitmapData = GetBitmapData(_libraryViewModel.Bitmap);

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