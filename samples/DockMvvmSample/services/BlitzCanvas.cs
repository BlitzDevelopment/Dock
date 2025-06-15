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
using System.Diagnostics;
using Avalonia.Threading;

namespace Avalonia.Controls;

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

public class Layer
{
    public SKImage Image { get; set; }
    public SKSurface Surface { get; set; }
    public SKPicture Picture { get; set; } 

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

public class DrawingCanvas : UserControl
{
    //This is where we keep the bitmaps that comprise individual layers
    //we use to composite what we present to the user
    private Layer UILayer;
    private int ActiveLayer;
    private Layer CachedActiveLayer;
    private List<Layer> ImageLayers = new List<Layer>();

    //Our render target we compile everything to and present to the user
    private RenderTargetBitmap RenderTarget;
    private SKPicture _compositedPicture;
    private bool IsRenderTargetDirty = true;

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
                IsRenderTargetDirty = true; // Mark RenderTarget as dirty when StageColor changes
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
                IsRenderTargetDirty = true; // Mark RenderTarget as dirty when scale changes
            }
        }
    }

    // Add a property to toggle Avalonia's ClipToBounds behavior
    public static readonly StyledProperty<bool> ClipToBoundsProperty =
        AvaloniaProperty.Register<DrawingCanvas, bool>(nameof(ClipToBounds), true);

    public bool ClipToBounds
    {
        get => GetValue(ClipToBoundsProperty);
        set => SetValue(ClipToBoundsProperty, value);
    }

    public void SetScale(double scale)
    {
        if (scale <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(scale), "Scale must be greater than zero.");
        }

        Scale = scale; // Update the scale property
        InvalidateVisual(); // Invalidate the control to trigger a re-render
    }

    public DrawingCanvas()
    {
        // Bind the Avalonia ClipToBounds property to this control
        this.Bind(UserControl.ClipToBoundsProperty, this.GetObservable(ClipToBoundsProperty));
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

    public void AddSvgLayer(XDocument svgDocument)
    {
        var layer = new Layer();
        layer.LoadSvg(svgDocument, (int)Width, (int)Height);

        // Dispose of the previous active layer if necessary
        CachedActiveLayer?.Surface?.Dispose();
        CachedActiveLayer?.Image?.Dispose();

        ImageLayers.Add(layer);
        ActiveLayer = ImageLayers.Count - 1;

        IsRenderTargetDirty = true; // Mark RenderTarget as dirty
    }

    public void ClearAllLayers()
    {
        // Dispose of all layers and their resources
        foreach (var layer in ImageLayers)
        {
            layer.Surface?.Dispose();
            layer.Image?.Dispose();
        }

        // Clear the list of layers
        ImageLayers.Clear();

        // Reset the active layer and cached layer
        ActiveLayer = -1;
        CachedActiveLayer?.Surface?.Dispose();
        CachedActiveLayer?.Image?.Dispose();
        CachedActiveLayer = null;

        // Dispose of the RenderTarget and reset it
        RenderTarget?.Dispose();
        RenderTarget = null;

        IsRenderTargetDirty = true; // Mark RenderTarget as dirty
    }

    public Task<bool> SaveAsync(string path)
    {
        return Task.Run(() =>
        {
            try
            {
                RenderTarget.Save(path);
            }
            catch(Exception)
            {
                return false;
            }

            return true;
        });
    }

    private void CompositeLayersToRenderTarget()
    {
        using var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(new SKRect(0, 0, (float)Width, (float)Height));

        canvas.Clear(StageColor);

        // Composite all layers as vector graphics
        foreach (var layer in ImageLayers)
        {
            if (layer.Picture != null)
            {
                canvas.Save();
                canvas.DrawPicture(layer.Picture); // Draw vector graphics
                canvas.Restore();
            }
        }

        _compositedPicture = recorder.EndRecording();
        IsRenderTargetDirty = false; // Mark composited picture as up-to-date
    }

    public override void Render(DrawingContext context)
    {
        if (IsRenderTargetDirty)
        {
            CompositeLayersToRenderTarget();
        }

        context.Custom(new CustomDrawOp(new Rect(0, 0, Width, Height), _compositedPicture, _scale));
    }
}