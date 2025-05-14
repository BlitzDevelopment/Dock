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

namespace Avalonia.Controls;

public class Layer
{
    public SKImage Image { get; set; }
    public SKSurface Surface { get; set; }

    public void LoadSvg(XDocument svgDocument, int width, int height)
    {
        var svg = new SKSvg();
        using (var stream = new MemoryStream())
        {
            svgDocument.Save(stream);
            stream.Seek(0, SeekOrigin.Begin);
            svg.Load(stream);
        }

        var info = new SKImageInfo(width, height);
        Surface = SKSurface.Create(info);
        var canvas = Surface.Canvas;

        canvas.Clear(SKColors.Transparent);
        canvas.DrawPicture(svg.Picture);
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
        var info = new SKImageInfo((int)Width, (int)Height);
        using (var surface = SKSurface.Create(info))
        {
            var canvas = surface.Canvas;
            canvas.Clear(SKColors.White);

            foreach (var layer in ImageLayers)
            {
                if (layer.Image != null)
                {
                    canvas.DrawImage(layer.Image, 0, 0);
                }
            }

            canvas.Flush();

            // Copy the composited image to the RenderTargetBitmap
            using (var snapshot = surface.Snapshot())
            using (var data = snapshot.Encode(SKEncodedImageFormat.Png, 100))
            using (var stream = new MemoryStream())
            {
                data.SaveTo(stream);
                stream.Seek(0, SeekOrigin.Begin);

                // Create a new RenderTargetBitmap and load the composited image into it
                var bitmap = new Bitmap(stream);
                RenderTarget = new RenderTargetBitmap(
                    new PixelSize(bitmap.PixelSize.Width, bitmap.PixelSize.Height),
                    new Vector(96, 96)
                );

                using (var drawingContext = RenderTarget.CreateDrawingContext())
                {
                    drawingContext.DrawImage(
                        bitmap,
                        new Rect(0, 0, bitmap.PixelSize.Width, bitmap.PixelSize.Height), // sourceRect
                        new Rect(0, 0, bitmap.PixelSize.Width, bitmap.PixelSize.Height)  // destRect
                    );
                }
            }
        }
    }

    public override void Render(DrawingContext context)
    {
        try
        {
            // Ensure the RenderTarget is updated with the composited layers
            CompositeLayersToRenderTarget();

            if (RenderTarget == null)
            {
                Console.WriteLine("RenderTarget is null");
                return;
            }

            Console.WriteLine($"RenderTarget dimensions: {RenderTarget.PixelSize.Width}x{RenderTarget.PixelSize.Height}");
            Console.WriteLine($"Number of layers: {ImageLayers.Count}");

            // Draw the finalized RenderTarget to the screen
            var sourceRect = new Rect(0, 0, RenderTarget.PixelSize.Width, RenderTarget.PixelSize.Height);
            var destRect = new Rect(0, 0, RenderTarget.PixelSize.Width, RenderTarget.PixelSize.Height);

            context.DrawImage(RenderTarget, sourceRect, destRect);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during Render: {ex.Message}");
        }
    }
}