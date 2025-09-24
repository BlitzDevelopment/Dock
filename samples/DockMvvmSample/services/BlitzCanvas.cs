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
using System.Diagnostics;
using Avalonia.Threading;
using Avalonia.Input;
using System.Runtime.InteropServices;

namespace Avalonia.Controls;

#region Avalonia Drawing
class CustomDrawOp : Avalonia.Rendering.SceneGraph.ICustomDrawOperation
{
    private readonly SKPicture _compositedPicture;
    private readonly double _scale;

    public CustomDrawOp(Rect bounds, SKPicture compositedPicture, double scale)
    {
        _compositedPicture = compositedPicture;
        Bounds = bounds;
        _scale = scale;
    }

    public void Dispose()
    {
        // No-op
    }

    public Rect Bounds { get; }
    public bool HitTest(Point p) => false;
    public bool Equals(Avalonia.Rendering.SceneGraph.ICustomDrawOperation other) => false;

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

    public BlitzLayer AdorningLayer { get; set; } = new BlitzLayer
    {
        Visible = false
    };

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

        // Adorning layer
        if (AdorningLayer.Visible)
        {
            using var recorder = new SKPictureRecorder();
            var canvas = recorder.BeginRecording(new SKRect(0, 0, 1920, 1080)); // Adjust canvas size as needed

            foreach (var element in AdorningLayer.Elements)
            {
                if (element.Picture != null)
                {
                    canvas.DrawPicture(element.Picture);
                }
            }

            pictures.Add(recorder.EndRecording());
        }

        return pictures;
    }

    public void UpdateAdorningLayer(Rect bounds, SKColor color)
    {
        // Clear existing elements in the adorning layer
        AdorningLayer.Elements.Clear();

        // Create a new element representing the bounding box
        var boundingBoxElement = new BlitzElement
        {
            Picture = CreateBoundingBoxPicture(bounds, color),
            Name = "BoundingBox"
        };

        AdorningLayer.Elements.Add(boundingBoxElement);
    }

    private SKPicture CreateBoundingBoxPicture(Rect bounds, SKColor color)
    {
        Console.WriteLine($"Creating bounding box with bounds: {bounds}"); // Log the bounds

        using var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(new SKRect((float)bounds.Left, (float)bounds.Top, (float)bounds.Right, (float)bounds.Bottom));

        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Stroke, // Use Stroke to draw the rectangle outline
            Color = color,               // Use the provided color
            StrokeWidth = 2,             // Set the stroke width
            IsAntialias = true           // Enable anti-aliasing for smooth edges
        };

        // Draw a rectangle matching the bounds
        canvas.DrawRect(new SKRect((float)bounds.Left, (float)bounds.Top, (float)bounds.Right, (float)bounds.Bottom), paint);

        return recorder.EndRecording();
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

    //Elements
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

    public void LoadSvg(XDocument svgDocument, int width, int height)
    {
        var svg = new SKSvg();
        using (var stream = new MemoryStream())
        {
            svgDocument.Save(stream);
            stream.Seek(0, SeekOrigin.Begin);
            svg.Load(stream);
        }

        Picture = svg.Picture; // Store the SKPicture

        var info = new SKImageInfo(width, height);
        Surface = SKSurface.Create(info);
        var canvas = Surface.Canvas;

        canvas.Clear(SKColors.Transparent);
        canvas.DrawPicture(Picture); // Use the SKPicture for rendering
        canvas.Flush();

        Image = Surface.Snapshot();
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

    //Our render target we compile everything to and present to the user
    private RenderTargetBitmap RenderTarget;
    private SKPicture _compositedPicture;

    // Add a StageColor property to manage the background color
    private SKColor _stageColor = SKColors.White; // Default to white
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

    // Add a Scale property to manage zoom level
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
        InvalidateVisual(); // Invalidate the control to trigger a re-render
    }

    // Add a property to toggle Avalonia's ClipToBounds behavior
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
        Background = Brushes.Transparent;
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

        // Use the CustomDrawOp to render the scaled SKPicture
        context.Custom(new CustomDrawOp(new Rect(0, 0, Width, Height), _compositedPicture, _scale));
    }

    //MARK: Hit Testing
    public void HitTest(Point mousePosition)
    {
        var transformedPoint = new SKPoint((float)(mousePosition.X / _scale), (float)(mousePosition.Y / _scale));
        Console.WriteLine($"HitTest at position: {transformedPoint} with scale: {_scale}");

        foreach (var layer in CanvasController.Layers.Reverse<BlitzLayer>()) // Reverse to check topmost layers first
        {
            if (!layer.Visible)
                continue;

            foreach (var element in layer.Elements)
            {
                if (element.Picture == null)
                    continue;

                // Set custom bounds as a 50x50 rectangle centered on element.Matrix.Tx and element.Matrix.Ty
                var centerX = (float)element.Matrix.Tx;
                var centerY = (float)element.Matrix.Ty;
                var bounds = new SKRect(centerX - 225, centerY - 225, centerX + 225, centerY + 225);

                if (bounds.Contains(transformedPoint))
                {
                    Console.WriteLine($"Hit Layer: {layer.Name}, Element: {element.Name}");

                    // Update the adorning layer with the bounding box
                    CanvasController.AdorningLayer.Visible = true;
                    CanvasController.UpdateAdorningLayer(new Rect(bounds.Left, bounds.Top, bounds.Width, bounds.Height), SKColors.Blue);

                    // Update the render target and invalidate the visual
                    CompositeLayersToRenderTarget();
                    InvalidateVisual();
                    return;
                }
            }
        }

        Console.WriteLine("No hit detected.");
        CanvasController.AdorningLayer.Elements.Clear(); // Clear the adorning layer if no hit
        CanvasController.AdorningLayer.Visible = false;

        // Update the render target and invalidate the visual
        CompositeLayersToRenderTarget();
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (e.Handled)
        {
            Console.WriteLine("PointerPressed event was already handled.");
            return;
        }

        base.OnPointerPressed(e);
        var mousePosition = e.GetPosition(this);
        HitTest(mousePosition);
    }

    //MARK: Selection Tool
    
    
}
#endregion