using System;
using System.IO;
using SkiaSharp;
using Svg.Skia;
using Avalonia.Skia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Media.Imaging;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Linq;
using Avalonia.Input;

namespace Avalonia.Controls;

#region Custom Drawing
class CustomDrawOp : Avalonia.Rendering.SceneGraph.ICustomDrawOperation
{
    private readonly SKPicture _compositedPicture;
    private readonly double _scale;

    public Rect Bounds { get; }
    public bool HitTest(Point p) => false;
    public bool Equals(Avalonia.Rendering.SceneGraph.ICustomDrawOperation other) => false;

    public CustomDrawOp(Rect bounds, SKPicture compositedPicture, double scale)
    {
        _compositedPicture = compositedPicture;
        _scale = scale;
        Bounds = bounds;
    }

    public void Dispose()
    {
        // No-op
    }

    public void Render(ImmediateDrawingContext context)
    {
        var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
        if (leaseFeature == null || _compositedPicture == null)
            return;

        using var lease = leaseFeature.Lease();
        var canvas = lease.SkCanvas;

        if (canvas != null)
        {
            canvas.DrawPicture(_compositedPicture, 0, 0);
            canvas.Scale((float)_scale, (float)_scale);
        }
    }
}
#endregion

#region BlitzCanvasController
public class BlitzCanvasController
{
    private readonly List<BlitzLayer> _layers = new();
    public IReadOnlyList<BlitzLayer> Layers => _layers;

    public BlitzCanvasController()
    {
    }

    public void AddLayer(BlitzLayer layer)
    {
        if (layer == null)
            throw new ArgumentNullException(nameof(layer));

        _layers.Add(layer);
    }

    public void RemoveLayer(BlitzLayer layer)
    {
        if (layer == null)
            throw new ArgumentNullException(nameof(layer));

        _layers.Remove(layer);
    }

    public void ClearAllLayers()
    {
        _layers.Clear();
    }

    public List<SKPicture> GenerateCompositedPictures()
    {
        var pictures = new List<SKPicture>();

        // Composite all elements from visible layers
        foreach (var layer in _layers)
        {
            if (!layer.Visible)
                continue;

            using var recorder = new SKPictureRecorder();
            var canvas = recorder.BeginRecording(new SKRect(0, 0, 1920, 1080)); // TODO: Adjust size as needed

            foreach (var element in layer.Elements)
            {
                canvas.DrawPicture(element.Picture);
            }

            pictures.Add(recorder.EndRecording());
        }

        return pictures;
    }
}
#endregion

#region Blitz Layers
public class BlitzLayer
{
    //CsXFL Properties
    public string Color { get; set; }
    public string LayerType { get; set; }
    public string Name { get; set; }
    public bool Locked { get; set; }
    public bool Current { get; set; }
    public bool Selected { get; set; }
    public bool Visible { get; set; }

    public List<BlitzElement> Elements { get; set; } = new List<BlitzElement>();
}

public static class LayerConverter
{
    public static BlitzLayer ConvertToBlitzLayer(CsXFL.Layer csxflLayer)
    {
        if (csxflLayer == null)
            throw new ArgumentNullException(nameof(csxflLayer));

        var blitzLayer = new BlitzLayer
        {
            Color = csxflLayer.Color,
            LayerType = csxflLayer.LayerType,
            Name = csxflLayer.Name,
            Locked = csxflLayer.Locked,
            Current = csxflLayer.Current,
            Selected = csxflLayer.Selected,
            Visible = csxflLayer.Visible
        };

        return blitzLayer;
    }
}
#endregion

#region Blitz Elements
public class BlitzElement
{
    public SKImage Image { get; set; }
    public SKSurface Surface { get; set; }
    public SKPicture Picture { get; set; }

    public CsXFL.Element Model { get; set; }

    //CsXFL Properties
    public string ElementType { get; set; }
    public string Name { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public bool Selected { get; set; }
    public CsXFL.Matrix Matrix { get; set; }
    public double ScaleX { get; set; }
    public double ScaleY { get; set; }
    public CsXFL.Point TransformationPoint { get; set; }

    public CsXFL.Rectangle BBox { get; private set; }

    public void LoadSvg(XDocument svgDocument, int width, int height, CsXFL.Rectangle bbox)
    {
        var svg = new SKSvg();
        using (var stream = new MemoryStream())
        {
            svgDocument.Save(stream);
            stream.Seek(0, SeekOrigin.Begin);
            svg.Load(stream);
        }

        Picture = svg.Picture;

        var info = new SKImageInfo(width, height);
        Surface = SKSurface.Create(info);
        var canvas = Surface.Canvas;

        canvas.Clear(SKColors.Transparent);
        canvas.DrawPicture(Picture);
        canvas.Flush();

        Image = Surface.Snapshot();

        BBox = bbox;
    }
}

public static class ElementConverter
{
    public static BlitzElement ConvertToBlitzElement(CsXFL.Element csxflelement)
    {
        if (csxflelement == null)
            throw new ArgumentNullException(nameof(csxflelement));

        // TODO: Being patient for width and height helpers...
        var blitzelement = new BlitzElement
        {
            Model = csxflelement,
            ElementType = csxflelement.ElementType,
            Name = csxflelement.Name,
            Width = 0,
            Height = 0,
            Selected = csxflelement.Selected,
            Matrix = csxflelement.Matrix,
            ScaleX = csxflelement.ScaleX,
            ScaleY = csxflelement.ScaleY,
            TransformationPoint = csxflelement.TransformationPoint   
        };

        return blitzelement;
    }
}
#endregion

#region Canvas Compositing
public class DrawingCanvas : UserControl
{
    public BlitzCanvasController CanvasController = new BlitzCanvasController();
    private BlitzLayer _adorningLayer = new BlitzLayer
    {
        Visible = false
    };

    #region Selection State
    private BlitzElement _selectedElement;
    private Point _dragStartPosition;
    private bool _isDragging;
    #endregion

    //Our render target we compile everything to and present to the user
    private RenderTargetBitmap RenderTarget;
    private SKPicture _compositedPicture;

    private SKColor _stageColor = SKColors.White;
    public SKColor StageColor
    {
        get => _stageColor;
        set
        {
            if (_stageColor != value)
            {
                _stageColor = value;
                InvalidateVisual();
            }
        }
    }

    private double _scale = 1.0;
    public double Scale
    {
        get => _scale;
        set
        {
            if (_scale != value)
            {
                _scale = value;
            }
        }
    }

    public void SetScale(double scale)
    {
        if (scale <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(scale), "Scale must be greater than zero.");
        }

        Scale = scale;

        Console.WriteLine($"Scale set to: {scale}");
        if (_selectedElement != null)
        {
            Console.WriteLine("Updating adoring layer due to scale change.");
            UpdateAdorningLayer(_selectedElement, SKColor.Parse("#388ff9"));
        }

        InvalidateVisual(); // Invalidate the control to trigger a re-render
    }

    // Todo: redo this, it sucks
    public static readonly StyledProperty<bool> ClipToBoundsProperty =
        AvaloniaProperty.Register<DrawingCanvas, bool>(nameof(ClipToBounds), true);

    public bool ClipToBounds
    {
        get => GetValue(ClipToBoundsProperty);
        set => SetValue(ClipToBoundsProperty, value);
    }

    public DrawingCanvas()
    {
        // Bind the Avalonia ClipToBounds property to this control
        this.Bind(UserControl.ClipToBoundsProperty, this.GetObservable(ClipToBoundsProperty));
        Focusable = true;
        IsHitTestVisible = true;
        Background = Avalonia.Media.Brushes.Transparent;
    }

    public override void EndInit()
    {
        SKPaint SKBrush = new SKPaint();
        SKBrush.IsAntialias = true;
        SKBrush.Color = new SKColor(0, 0, 0);
        SKBrush.Shader = SKShader.CreateColor(SKBrush.Color);
        RenderTarget = new RenderTargetBitmap(new PixelSize((int)Width, (int)Height), new Vector(96, 96));

        var drawingContext = RenderTarget.CreateDrawingContext();
        if (drawingContext is IDrawingContextImpl contextImpl)
        {
            var skiaFeature = contextImpl.GetFeature<ISkiaSharpApiLeaseFeature>();
            if (skiaFeature != null)
            {
                using (var lease = skiaFeature.Lease())
                {
                    SKCanvas canvas = lease.SkCanvas;
                    if (canvas != null)
                    {
                        canvas.Clear(new SKColor(255, 255, 255));
                    }
                }
            }
        }

        base.EndInit();
    }

    private void CompositeLayersToRenderTarget()
    {
        // Generate composited pictures from the BlitzCanvasController
        var pictureLayers = CanvasController.GenerateCompositedPictures();

        using var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(new SKRect(0, 0, (float)Width, (float)Height));

        // Clear the canvas with the stage color
        canvas.Clear(StageColor);

        // Iterate over all SKPicture layers
        foreach (var picture in pictureLayers)
        {
            if (picture != null)
            {
                canvas.DrawPicture(picture);
            }
        }

        // Finalize the composited picture
        _compositedPicture = recorder.EndRecording();
    }

    // MARK: Adorning
    public void UpdateAdorningLayer(BlitzElement element, SKColor color)
    {
        // Clear existing elements in the adorning layer
        _adorningLayer.Elements.Clear();

        // Create a new element representing the bounding box
        var boundingBoxElement = new BlitzElement
        {
            Picture = CreateSelectionAdorner(element, color),
            Name = "BoundingBox"
        };

        _adorningLayer.Elements.Add(boundingBoxElement);
        _adorningLayer.Visible = true;

        InvalidateVisual(); // Trigger a re-render
    }

    private SKPicture CreateSelectionAdorner(BlitzElement element, SKColor color)
    {
        // Extract the bounding box and transformation matrix
        CsXFL.Rectangle bbox = element.BBox;
        CsXFL.Matrix matrix = element.Matrix;

        // Transform the corners of the bounding box using the matrix
        var topLeft = TransformPoint(matrix, bbox.Left, bbox.Top);
        var topRight = TransformPoint(matrix, bbox.Right, bbox.Top);
        var bottomLeft = TransformPoint(matrix, bbox.Left, bbox.Bottom);
        var bottomRight = TransformPoint(matrix, bbox.Right, bbox.Bottom);

        // Compute the axis-aligned bounding box (AABB)
        float minX = Math.Min(Math.Min(topLeft.X, topRight.X), Math.Min(bottomLeft.X, bottomRight.X));
        float maxX = Math.Max(Math.Max(topLeft.X, topRight.X), Math.Max(bottomLeft.X, bottomRight.X));
        float minY = Math.Min(Math.Min(topLeft.Y, topRight.Y), Math.Min(bottomLeft.Y, bottomRight.Y));
        float maxY = Math.Max(Math.Max(topLeft.Y, topRight.Y), Math.Max(bottomLeft.Y, bottomRight.Y));

        // Create the SKPicture
        using var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(new SKRect(minX, minY, maxX, maxY));

        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,        // Use Stroke to draw the rectangle outline
            Color = color,                      // Use the provided color
            StrokeWidth = 2 / (float)_scale,    // Adjust stroke width based on the scale
            IsAntialias = true                  // Enable anti-aliasing for smooth edges
        };

        // Draw the axis-aligned bounding box
        canvas.DrawRect(new SKRect(minX, minY, maxX, maxY), paint);

        return recorder.EndRecording();
    }

    private SKPoint TransformPoint(CsXFL.Matrix matrix, double x, double y)
    {
        float transformedX = (float)(matrix.A * x + matrix.C * y + matrix.Tx);
        float transformedY = (float)(matrix.B * x + matrix.D * y + matrix.Ty);
        return new SKPoint(transformedX, transformedY);
    }

    private SKPicture GenerateAdorningLayer()
    {
        if (!_adorningLayer.Visible || _adorningLayer.Elements.Count == 0)
            return null;

        using var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(new SKRect(0, 0, (float)Width, (float)Height));

        foreach (var element in _adorningLayer.Elements)
        {
            if (element.Picture != null)
            {
                canvas.DrawPicture(element.Picture);
            }
        }

        return recorder.EndRecording();
    }

    //MARK: Element Hit Testing
    public BlitzElement HitTest(Point mousePosition)
    {
        var transformedPoint = new SKPoint((float)(mousePosition.X), (float)(mousePosition.Y));
        Console.WriteLine($"HitTest at position: {transformedPoint} with scale: {_scale}");

        foreach (var layer in CanvasController.Layers.Reverse<BlitzLayer>()) // Reverse to check topmost layers first
        {
            if (!layer.Visible)
                continue;

            foreach (var element in layer.Elements)
            {
                if (element.Picture == null || element.Matrix == null)
                    continue;

                // Transform the point into the element's local coordinate space
                var matrix = element.Matrix;
                var skMatrix = new SKMatrix
                {
                    ScaleX = (float)matrix.A,
                    SkewY = (float)matrix.B,
                    SkewX = (float)matrix.C,
                    ScaleY = (float)matrix.D,
                    TransX = (float)matrix.Tx,
                    TransY = (float)matrix.Ty,
                    Persp0 = 0,
                    Persp1 = 0,
                    Persp2 = 1
                };

                if (!skMatrix.TryInvert(out var inverseMatrix))
                {
                    Console.WriteLine($"Failed to invert matrix for element: {element.Name}");
                    continue;
                }

                // Use the `inverseMatrix` for further operations
                var localPoint = inverseMatrix.MapPoint(transformedPoint);

                // Check if the local point is within the element's bounding box
                var bounds = new SKRect((float)element.BBox.Left, (float)element.BBox.Top, (float)element.BBox.Right, (float)element.BBox.Bottom);

                if (bounds.Contains(localPoint))
                {
                    Console.WriteLine($"Hit Layer: {layer.Name}, Element: {element.ElementType}");
                    return element;
                }
            }
        }

        Console.WriteLine("No hit detected.");
        return null;
    }

    //MARK: Pointer Events
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (e.Handled)
        {
            Console.WriteLine("PointerPressed event was already handled.");
            return;
        }

        // Check if the left mouse button was pressed
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            base.OnPointerPressed(e);
            var mousePosition = e.GetPosition(this);
            _selectedElement = HitTest(mousePosition);

            if (_selectedElement != null)
            {
                _adorningLayer.Visible = true;
                UpdateAdorningLayer(_selectedElement, SKColor.Parse("#388ff9"));
                InvalidateVisual();

                _dragStartPosition = mousePosition;
                _isDragging = true;
                e.Handled = true;
            }
            else
            {
                _selectedElement = null;
                _adorningLayer.Elements.Clear();
                _adorningLayer.Visible = false;
                InvalidateVisual();
            }
        }
        else
        {
            // Ignore if not left click
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        if (_isDragging && _selectedElement != null)
        {
            var currentPosition = e.GetPosition(this);
            var deltaX = currentPosition.X - _dragStartPosition.X;
            var deltaY = currentPosition.Y - _dragStartPosition.Y;

            // Update the element's transformation matrix
            _selectedElement.Matrix.Tx += deltaX;
            _selectedElement.Matrix.Ty += deltaY;

            // Update the drag start position
            _dragStartPosition = currentPosition;

            // Redraw the canvas
            CompositeLayersToRenderTarget();
            InvalidateVisual();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;

            if (_selectedElement != null)
            {
                // Update the model's matrix with the new Tx and Ty values
                if (_selectedElement.Model != null)
                {
                    _selectedElement.Model.Matrix.Tx = _selectedElement.Matrix.Tx;
                    _selectedElement.Model.Matrix.Ty = _selectedElement.Matrix.Ty;
                }
            }

            e.Handled = true;
        }
    }

    //MARK: Rendering
    public Task<bool> SaveAsync(string path)
    {
        return Task.Run(() =>
        {
            try
            {
                RenderTarget.Save(path);
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        });
    }

    public override void Render(DrawingContext context)
    {
        if (_compositedPicture == null)
        {
            // If the composited picture is null, we need to generate it
            CompositeLayersToRenderTarget();
        }

        // Render the composited picture
        context.Custom(new CustomDrawOp(new Rect(0, 0, Width, Height), _compositedPicture, _scale));

        // Render the adorning layer on top
        var adorningPicture = GenerateAdorningLayer();
        if (adorningPicture != null)
        {
            context.Custom(new CustomDrawOp(new Rect(0, 0, Width, Height), adorningPicture, _scale));
        }
    }
}
#endregion