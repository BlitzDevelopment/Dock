using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.PanAndZoom;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Blitz.Events;
using Blitz.ViewModels.Documents;
using CsXFL;
using Rendering;
using SkiaSharp;
using Svg.Skia;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Blitz.Views.Documents;

public partial class DocumentView : UserControl
{
    private readonly DocumentViewModel _documentViewModel;
    private readonly ZoomBorder? _zoomBorder;
    private CsXFL.Document _workingCsXFLDoc;
    private readonly Dictionary<string, SKPicture> _svgPictureCache = new();
    private SKPicture? _cachedSvgPicture;

    public DocumentView()
    {
        InitializeComponent();

        App.EventAggregator.Subscribe<DocumentFlyoutRequestedEvent>(OnDocumentFlyoutRequested);
        App.EventAggregator.Subscribe<DocumentProgressChangedEvent>(OnDocumentProgressChanged);
        App.EventAggregator.Subscribe<ActiveDocumentChangedEvent>(OnActiveDocumentChanged);

        _zoomBorder = this.Find<ZoomBorder>("ZoomBorder");
        _zoomBorder.Background = new SolidColorBrush(Colors.Transparent);
        if (_zoomBorder != null)
        {
            _zoomBorder.KeyDown += ZoomBorder_KeyDown;
            _zoomBorder.ZoomChanged += ZoomBorder_ZoomChanged;
        }

        SetProgressRingState(true);
        Task.Run(async () =>
            {
                await Task.Delay(5000);
                await Dispatcher.UIThread.InvokeAsync(() => SetProgressRingState(false));
            });
        ShowFlyoutAsync("Loaded " + Path.GetFileName(An.GetActiveDocument().Filename)).ConfigureAwait(false);
    }

    private void ZoomBorder_KeyDown(object? sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.F:
                    _zoomBorder?.Fill();
                    break;
                case Key.U:
                    _zoomBorder?.Uniform();
                    break;
                case Key.R:
                    _zoomBorder?.ResetMatrix();
                    break;
                case Key.T:
                    _zoomBorder?.ToggleStretchMode();
                    _zoomBorder?.AutoFit();
                    break;
            }
        }

    private void ZoomBorder_ZoomChanged(object sender, ZoomChangedEventArgs e)
    {
        Console.WriteLine($"ZoomX: {e.ZoomX}, ZoomY: {e.ZoomY}");
        NumericUpDown.Value = (decimal)Math.Round(e.ZoomX, 2);
        MainSkXamlCanvas.Invalidate();
    }

    private void OnActiveDocumentChanged(ActiveDocumentChangedEvent e)
    {
        _workingCsXFLDoc = An.GetDocument(e.Document.DocumentIndex);
        MainSkXamlCanvas.PaintSurface += ClearCanvas;
        ViewFrame();
    }

    public void ViewFrame()
    {
        var operatingTimeline = _workingCsXFLDoc.Timelines[0];
        var operatingFrame = 0;
        var layers = operatingTimeline.Layers;

        for (int i = layers.Count - 1; i >= 0; i--)
        {
            var layer = layers[i];
            if (layer.GetFrameCount() <= operatingFrame)
            {
                continue;
            }

            var frame = layer.GetFrame(operatingFrame);
            foreach (var element in frame.Elements)
            {
                try
                {
                    string appDataFolder = App.BlitzAppData.GetTmpFolder();

                    SVGRenderer renderer = new SVGRenderer(_workingCsXFLDoc!, appDataFolder, true);
                    string elementIdentifier =  _workingCsXFLDoc.Timelines[0].Name + "_" + layer.Name + "_" + element.Name;

                    // No support for color effects yet
                    (Dictionary<string, XElement> d, List<XElement> b) = renderer.RenderElement(element, elementIdentifier, (operatingFrame-layer.GetFrame(operatingFrame).StartFrame), CsXFL.Color.DefaultColor(), false);

                    // Create the root SVG element
                    XNamespace svgNamespace = "http://www.w3.org/2000/svg";
                    var svgRoot = new XElement(svgNamespace + "svg",
                        new XAttribute("xmlns", svgNamespace.NamespaceName),
                        new XAttribute("version", "1.1"),
                        new XAttribute("width", "100%"),
                        new XAttribute("height", "100%")
                    );

                    // Add the defs (d) to the SVG
                    if (d != null)
                    {
                        var defsElement = new XElement(svgNamespace + "defs");
                        foreach (var def in d.Values)
                        {
                            defsElement.Add(def);
                        }
                        svgRoot.Add(defsElement);
                    }

                    // Add the body (b) to the SVG
                    if (b != null)
                    {
                        foreach (var bodyElement in b)
                        {
                            svgRoot.Add(bodyElement);
                        }
                    }

                    // Create the XDocument
                    XDocument renderedSvgDoc = new XDocument(svgRoot);
                    ApplyTransformAndDraw(renderedSvgDoc);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error rendering symbol: {ex.Message}" + ex.StackTrace);
                }
            }
        }

        MainSkXamlCanvas.Width = _workingCsXFLDoc.Width;
        MainSkXamlCanvas.Height = _workingCsXFLDoc.Height;
    }

    private void ApplyTransformAndDraw(XDocument renderedSvgDoc)
    {
        try
        {
            MainSkXamlCanvas.PaintSurface -= OnCanvasPaintWrapper;
            MainSkXamlCanvas.PaintSurface += OnCanvasPaintWrapper;

            void OnCanvasPaintWrapper(object sender, SKPaintSurfaceEventArgs e)
            {
                OnCanvasPaint(sender, e, renderedSvgDoc);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error applying transform and drawing: {ex.Message}" + ex.StackTrace);
        }
    }

    void OnCanvasPaint(object sender, SKPaintSurfaceEventArgs e, XDocument renderedSvgDoc)
    {
        var canvas = e.Surface.Canvas;

        // Generate a unique key for the SVG content
        string svgKey = renderedSvgDoc.ToString().GetHashCode().ToString();

        // Check if the SKPicture is already cached
        if (!_svgPictureCache.TryGetValue(svgKey, out var cachedPicture))
        {
            using (var stream = new MemoryStream())
            {
                var writer = new StreamWriter(stream);
                writer.Write(renderedSvgDoc.ToString());
                writer.Flush();
                stream.Position = 0;

                var svg = new SKSvg();
                svg.Load(stream);
                cachedPicture = svg.Picture;

                // Cache the SKPicture
                if (cachedPicture != null)
                {
                    _svgPictureCache[svgKey] = cachedPicture;
                }
            }
        }

        _cachedSvgPicture = cachedPicture;

        // Apply transformations and draw the picture
        var matrix = SKMatrix.CreateTranslation((float)_zoomBorder.OffsetX, (float)_zoomBorder.OffsetY);
        matrix = SKMatrix.Concat(matrix, SKMatrix.CreateScale((float)_zoomBorder.ZoomX, (float)_zoomBorder.ZoomY));

        canvas.SetMatrix(matrix);
        if (_cachedSvgPicture != null)
        {
            canvas.DrawPicture(_cachedSvgPicture);
        }
    }

    private void ClearCanvas(object sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public async void SetProgressRingState(bool isActive)
    {
        if (ProgressRingControl != null)
        {
            // Create an animation for fading opacity
            var animation = new Animation
            {
                Duration = TimeSpan.FromSeconds(0.5),
                Children =
                {
                    new KeyFrame
                    {
                        Cue = new Cue(0),
                        Setters =
                        {
                            new Setter(Control.OpacityProperty, isActive ? 0 : 1) // Start opacity
                        }
                    },
                    new KeyFrame
                    {
                        Cue = new Cue(1),
                        Setters =
                        {
                            new Setter(Control.OpacityProperty, isActive ? 1 : 0) // End opacity
                        }
                    }
                }
            };

            // Run the animation asynchronously
            await animation.RunAsync(ProgressRingControl);

            // Toggle visibility after the animation completes
            ProgressRingControl.IsVisible = isActive;
        }
    }

    private void OnDocumentFlyoutRequested(DocumentFlyoutRequestedEvent e)
    {
        if (e.Document == _documentViewModel)
        {
            ShowFlyoutAsync(e.FlyoutMessage).ConfigureAwait(false);
        }
    }

    private void OnDocumentProgressChanged(DocumentProgressChangedEvent e)
    {
        if (e.Document == _documentViewModel)
        {
            SetProgressRingState(e.IsInProgress);
        }
    }

    public async Task ShowFlyoutAsync(string message, int durationInMilliseconds = 3000)
    {
        FlyoutText.Text = message;
        FlyoutContainer.IsVisible = true;
        await Task.Delay(durationInMilliseconds);
        FlyoutContainer.IsVisible = false;
    }
}