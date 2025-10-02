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
            var canvas = recorder.BeginRecording(new SKRect(float.MinValue / 2, float.MinValue / 2, float.MaxValue / 2, float.MaxValue / 2));

            foreach (var element in layer.Elements)
            {
                canvas.DrawPicture(element.Picture);
            }

            pictures.Add(recorder.EndRecording());
        }

        return pictures;
    }
}