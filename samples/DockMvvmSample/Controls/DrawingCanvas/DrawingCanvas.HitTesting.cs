using SkiaSharp;
using System;

namespace Avalonia.Controls;

public partial class DrawingCanvas
{
    public BlitzElement HitTest(Point mousePosition)
    {
        var transformedPoint = new SKPoint((float)(mousePosition.X), (float)(mousePosition.Y));
        Console.WriteLine($"HitTest at position: {transformedPoint} with scale: {_scale}");

        foreach (var layer in CanvasController.Layers)
        {
            if (!layer.Visible)
                continue;

            foreach (var element in layer.Elements)
            {
                if (element.Matrix == null)
                    continue;

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

                var localPoint = inverseMatrix.MapPoint(transformedPoint);
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
}