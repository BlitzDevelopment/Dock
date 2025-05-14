using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.PanAndZoom;
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
using System.Linq;

namespace Blitz.Views.Documents;

public partial class DocumentView : UserControl
{
    #region Data
    private readonly DocumentViewModel _documentViewModel;
    private CsXFL.Document _workingCsXFLDoc;
    #endregion

    #region UI Elements
    private readonly ZoomBorder? _zoomBorder;
    private DrawingCanvas? _drawingCanvas;
    #endregion

    #region Caching
    private readonly Dictionary<string, SKPicture> _svgPictureCache = new();
    private SKPicture? _cachedSvgPicture;
    #endregion

    public DocumentView()
    {
        InitializeComponent();

        App.EventAggregator.Subscribe<DocumentFlyoutRequestedEvent>(OnDocumentFlyoutRequested);
        App.EventAggregator.Subscribe<DocumentProgressChangedEvent>(OnDocumentProgressChanged);
        App.EventAggregator.Subscribe<ActiveDocumentChangedEvent>(OnActiveDocumentChanged);

        // Initialize ZoomBorder and make it transparent
        _zoomBorder = this.Find<ZoomBorder>("ZoomBorder");
        _zoomBorder.Background = new SolidColorBrush(Colors.Transparent);
        if (_zoomBorder != null)
        {
            _zoomBorder.ZoomChanged += ZoomBorder_ZoomChanged;
        }

        // ProgressRing control and flyout modal
        SetProgressRingState(true);
        Task.Run(async () =>
            {
                await Task.Delay(5000);
                await Dispatcher.UIThread.InvokeAsync(() => SetProgressRingState(false));
            });
        ShowFlyoutAsync("Loaded " + Path.GetFileName(An.GetActiveDocument().Filename)).ConfigureAwait(false);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    #region Event Handlers
    private void ZoomBorder_ZoomChanged(object sender, ZoomChangedEventArgs e)
    {
        //Console.WriteLine($"ZoomX: {e.ZoomX}, ZoomY: {e.ZoomY}");
        NumericUpDown.Value = (decimal)Math.Round(e.ZoomX, 2);
        //MainSkXamlCanvas.Invalidate();
    }

    private void OnActiveDocumentChanged(ActiveDocumentChangedEvent e)
    {
        e.Document.ZoomBorder = _zoomBorder;
        _workingCsXFLDoc = An.GetDocument(e.Document.DocumentIndex);
        PopulateSceneSelector();
        //MainSkXamlCanvas.PaintSurface += ClearCanvas;
        ViewFrame();
    }
    #endregion

    private void ClearCanvas(object sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
    }

    public void PopulateSceneSelector()
    {
        if (_workingCsXFLDoc?.Timelines == null || SceneSelector == null)
        {
            return;
        }

        // Populate ComboBox with timeline names using ItemsSource
        SceneSelector.ItemsSource = _workingCsXFLDoc.Timelines.Select(t => t.Name).ToList();

        // Optionally set the selected item to the first timeline
        if (SceneSelector.ItemsSource is IList<string> items && items.Count > 0)
        {
            SceneSelector.SelectedIndex = 0;
        }
    }

    #region Canvas Rendering
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
                    _drawingCanvas = this.Find<DrawingCanvas>("DrawingCanvas");
                    if (_drawingCanvas == null)
                    {
                        Console.WriteLine("Error: DrawingCanvas not found in the XAML.");
                        return;
                    }
                    _drawingCanvas.AddSvgLayer(renderedSvgDoc);
                    //ApplyTransformAndDraw(renderedSvgDoc, element);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error rendering symbol: {ex.Message}" + ex.StackTrace);
                }
            }
        }

        //MainSkXamlCanvas.Width = _workingCsXFLDoc.Width;
        //MainSkXamlCanvas.Height = _workingCsXFLDoc.Height;
    }

    private void ApplyTransformAndDraw(XDocument renderedSvgDoc, Element element)
    {
        try
        {
            //MainSkXamlCanvas.PaintSurface -= OnCanvasPaintWrapper;
            //MainSkXamlCanvas.PaintSurface += OnCanvasPaintWrapper;

            void OnCanvasPaintWrapper(object sender, SKPaintSurfaceEventArgs e)
            {
                OnCanvasPaint(sender, e, renderedSvgDoc, element);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error applying transform and drawing: {ex.Message}" + ex.StackTrace);
        }
    }

    void OnCanvasPaint(object sender, SKPaintSurfaceEventArgs e, XDocument renderedSvgDoc, Element element)
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

        // Not supporting text runs right now

        // Custom handling for text elements in the SVG
        var textElements = renderedSvgDoc.Descendants().Where(el => el.Name.LocalName == "text");
        if (textElements.Any())
        {
            foreach (var textElement in textElements)
            {
                var tspan = textElement.Descendants().FirstOrDefault(el => el.Name.LocalName == "tspan");
                if (tspan != null)
                {
                    // Extract font settings from the tspan attributes
                    string fontFamily = tspan.Attribute("font-family")?.Value ?? "Times New Roman";
                    float fontSize = float.TryParse(tspan.Attribute("font-size")?.Value, out var size) ? size : 24;
                    string fillColor = tspan.Attribute("fill")?.Value ?? "#000000";

                    // Convert fill color to SKColor
                    SKColor skFillColor = SKColor.Parse(fillColor);

                    using SKPaint paint = new SKPaint
                    {
                        Color = skFillColor,
                        IsAntialias = true,
                        TextSize = fontSize,
                        Typeface = SKTypeface.FromFamilyName(
                            familyName: fontFamily,
                            weight: SKFontStyleWeight.SemiBold,
                            width: SKFontStyleWidth.Normal,
                            slant: SKFontStyleSlant.Italic),
                    };

                    string textContent = tspan.Value;

                    canvas.DrawText(textContent, (float)element.Matrix.Tx, (float)element.Matrix.Ty, paint);
                }
            }
        }
    }
    #endregion

    #region Ring & Flyout
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
    #endregion
}