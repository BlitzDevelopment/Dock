using SkiaSharp;
using System;

namespace Avalonia.Controls;

public partial class DrawingCanvas
{
    private const float DefaultStrokeWidth = 2f;

    public void UpdateAdorningLayer(BlitzElement element, SKColor color)
    {
        AdorningLayer.Elements.Clear();

        var boundingBoxElement = new BlitzElement()
        {
            Picture = CreateSelectionAdorner(element, color)
        };

        AdorningLayer.Elements.Add(boundingBoxElement);
        AdorningLayer.Visible = true;

        InvalidateVisual();
    }

    private SKPicture CreateSelectionAdorner(BlitzElement element, SKColor color)
    {
        var bbox = element.BBox;
        var matrix = element.Matrix;

        var topLeft = TransformPoint(matrix, bbox.Left, bbox.Top);
        var topRight = TransformPoint(matrix, bbox.Right, bbox.Top);
        var bottomLeft = TransformPoint(matrix, bbox.Left, bbox.Bottom);
        var bottomRight = TransformPoint(matrix, bbox.Right, bbox.Bottom);

        float minX = Math.Min(Math.Min(topLeft.X, topRight.X), Math.Min(bottomLeft.X, bottomRight.X));
        float maxX = Math.Max(Math.Max(topLeft.X, topRight.X), Math.Max(bottomLeft.X, bottomRight.X));
        float minY = Math.Min(Math.Min(topLeft.Y, topRight.Y), Math.Min(bottomLeft.Y, bottomRight.Y));
        float maxY = Math.Max(Math.Max(topLeft.Y, topRight.Y), Math.Max(bottomLeft.Y, bottomRight.Y));

        using var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(new SKRect(minX, minY, maxX, maxY));

        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = color,
            StrokeWidth = DefaultStrokeWidth / (float)_scale,
            IsAntialias = true
        };

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
        if (!AdorningLayer.Visible || AdorningLayer.Elements.Count == 0)
            return null;

        using var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(new SKRect(0, 0, (float)Width, (float)Height));

        foreach (var element in AdorningLayer.Elements)
        {
            if (element.Picture != null)
            {
                canvas.DrawPicture(element.Picture);
            }
        }

        return recorder.EndRecording();
    }
}