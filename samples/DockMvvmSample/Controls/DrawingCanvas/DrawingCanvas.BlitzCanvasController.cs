using SkiaSharp;
using System;
using System.Collections.Generic;

namespace Avalonia.Controls;

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
            var canvas = recorder.BeginRecording(new SKRect(0, 0, float.MaxValue, float.MaxValue));

            // Create paint for the debug red box
            using var debugPaint = new SKPaint
            {
                Color = SKColors.Red.WithAlpha(64), // Quarter opacity (255 * 0.25 = 64)
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2.0f,
                IsAntialias = true
            };

            foreach (var element in layer.Elements)
            {
                canvas.DrawPicture(element.Picture);
                
                // Draw the quarter opacity red box around the picture's cull rect
                var cullRect = element.Picture.CullRect;
                canvas.DrawRect(cullRect, debugPaint);
            }

            pictures.Add(recorder.EndRecording());
        }

        return pictures;
    }
}