using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Input;
using System;
using System.Collections.Generic;

public enum CursorType
{
    Rotate,
    Skew,
    Default
}


public static class VisualCursorFactory
{
    public static Cursor Create(Visual visual, PixelSize size, PixelPoint hotSpot)
    {
        using var rtb = new RenderTargetBitmap(size, new(96, 96));
        rtb.Render(visual);
        return new Cursor(rtb, hotSpot);
    }
}

public class CustomCursorFactory
{
    private static readonly Dictionary<CursorType, string> SvgPaths = new()
    {
        { CursorType.Rotate, "M480-160q-134 0-227-93t-93-227q0-134 93-227t227-93q69 0 132 28.5T720-690v-110h80v280H520v-80h168q-32-56-87.5-88T480-720q-100 0-170 70t-70 170q0 100 70 170t170 70q77 0 139-44t87-116h84q-28 106-114 173t-196 67Z" },
        { CursorType.Skew, "M 5.95 8.5 L 2.95 14.45 29.05 14.45 M 26.05 23.75 L 29.05 17.8 2.95 17.8" },
        { CursorType.Default, "M0 0 L10 0 L5 10 Z" }
    };
    public static Cursor CreateCursor(CursorType cursorType)
    {
        const int fixedCursorSize = 16; // Fixed size for the cursor

        // Get the SVG path for the specified cursor type
        if (!SvgPaths.TryGetValue(cursorType, out var svgPath))
        {
            throw new ArgumentException($"No SVG path found for cursor type: {cursorType}");
        }

        // Parse the SVG path
        var geometry = StreamGeometry.Parse(svgPath);
        var bounds = geometry.Bounds;
        double scale = Math.Min(fixedCursorSize / bounds.Width, fixedCursorSize / bounds.Height);

        // Create a transform to scale and center the geometry
        var transformGroup = new TransformGroup();
        transformGroup.Children.Add(new ScaleTransform(scale, scale));
        transformGroup.Children.Add(new TranslateTransform(
            (fixedCursorSize - bounds.Width * scale) / 2 - bounds.X * scale,
            (fixedCursorSize - bounds.Height * scale) / 2 - bounds.Y * scale
        ));

        geometry.Transform = transformGroup;

        var originalPath = new Avalonia.Controls.Shapes.Path
        {
            Data = geometry,
            StrokeThickness = 1,
            Fill = Brushes.Black,
            Stroke = Brushes.LightGray,
        };

        var vb = new Viewbox
        {
            Width = fixedCursorSize,
            Height = fixedCursorSize,
            Child = originalPath,
        };

        vb.Measure(new Size(fixedCursorSize, fixedCursorSize));
        vb.Arrange(new Avalonia.Rect(0, 0, fixedCursorSize, fixedCursorSize));

        return VisualCursorFactory.Create(vb, new Avalonia.PixelSize(fixedCursorSize, fixedCursorSize), new Avalonia.PixelPoint(fixedCursorSize / 2, fixedCursorSize / 2));
    }
}