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
    private DocumentViewModel _documentViewModel;
    private CsXFL.Document _workingCsXFLDoc;
    #endregion

    #region UI Elements
    private readonly ZoomBorder? _zoomBorder;
    private DrawingCanvas? _drawingCanvas;
    #endregion

    public DocumentView()
    {
        InitializeComponent();

        App.EventAggregator.Subscribe<DocumentFlyoutRequestedEvent>(OnDocumentFlyoutRequested);
        App.EventAggregator.Subscribe<DocumentProgressChangedEvent>(OnDocumentProgressChanged);
        App.EventAggregator.Subscribe<ActiveDocumentChangedEvent>(OnActiveDocumentChanged);

        App.EventAggregator.Subscribe<CanvasActionCenterEvent>(RequestCanvasActionCenter);
        App.EventAggregator.Subscribe<CanvasActionToggleClipEvent>(RequestCanvasActionToggleClip);

        _drawingCanvas = this.Find<DrawingCanvas>("DrawingCanvas");
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
    void RequestCanvasActionCenter(CanvasActionCenterEvent e)
    {
        if (e.DocumentIndex != _documentViewModel.DocumentIndex)
            return;

        _zoomBorder.Uniform();
    }

    void RequestCanvasActionToggleClip(CanvasActionToggleClipEvent e)
    {
        if (e.DocumentIndex != _documentViewModel.DocumentIndex)
            return;

        _drawingCanvas.ClipToBounds = !_drawingCanvas.ClipToBounds;
    }

    private double _previousZoomX = -1; // Initialize with an invalid value
    private void ZoomBorder_ZoomChanged(object sender, ZoomChangedEventArgs e)
    {
        // Check if the ZoomX value has changed
        if (Math.Abs(e.ZoomX - _previousZoomX) > 0.0001) // Epsilon award
        {
            _previousZoomX = e.ZoomX; // Update the stored value
            NumericUpDown.Value = (decimal)Math.Round(e.ZoomX, 2);
            _drawingCanvas.SetScale(e.ZoomX);
        }
    }
    private void OnActiveDocumentChanged(ActiveDocumentChangedEvent e)
    {
        _workingCsXFLDoc = An.GetDocument(e.Document.DocumentIndex);
        _documentViewModel = e.Document;
        PopulateSceneSelector();

        _drawingCanvas.Width = _workingCsXFLDoc.Width;
        _drawingCanvas.Height = _workingCsXFLDoc.Height;
        _drawingCanvas.StageColor = SKColor.Parse(_workingCsXFLDoc.BackgroundColor);

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

        // Clear existing layers in the controller
        _drawingCanvas.CanvasController.ClearAllLayers();

        for (int i = layers.Count - 1; i >= 0; i--)
        {
            var layer = layers[i];
            if (layer.GetFrameCount() <= operatingFrame)
            {
                continue;
            }

            // Create a new BlitzLayer
            var blitzLayer = LayerConverter.ConvertToBlitzLayer(layer);

            // Add elements to the BlitzLayer
            var frame = layer.GetFrame(operatingFrame);
            foreach (var element in frame.Elements)
            {
                try
                {
                    string appDataFolder = App.BlitzAppData.GetTmpFolder();
                    string elementIdentifier = _workingCsXFLDoc.Timelines[0].Name + "_" + layer.Name + "_" + element.Name;
                    SVGRenderer renderer = new SVGRenderer(_workingCsXFLDoc!, appDataFolder, true);

                    // Render the element
                    CsXFL.Rectangle bbox = renderer.GetNormalizedElementBoundingBox(element, operatingFrame, false);

                    (Dictionary<string, XElement> d, List<XElement> b) = renderer.RenderElement(
                        element,
                        elementIdentifier,
                        (operatingFrame - layer.GetFrame(operatingFrame).StartFrame),
                        CsXFL.Color.DefaultColor(),
                        insideMask: false,
                        returnIdentityTransformation: true
                    );

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

                    // Create a BlitzElement and add it to the layer
                    var blitzElement = ElementConverter.ConvertToBlitzElement(element);
                    blitzElement.LoadSvg(renderedSvgDoc, bbox);
                    blitzLayer.Elements.Add(blitzElement);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error rendering element: {ex.Message}" + ex.StackTrace);
                }
            }

            // Add the layer to the controller
            _drawingCanvas.CanvasController.AddLayer(blitzLayer);
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