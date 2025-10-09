using SkiaSharp;
using System;
using System.Linq;

namespace Avalonia.Controls;

public partial class DrawingCanvas
{
    private const float _defaultStrokeWidth = 2f;
    private const float _transformHandleSize = 10f;
    private SKColor _selectionColor = SKColor.Parse("#388ff9");

    public void UpdateAdorningLayer(BlitzElement element)
    {
        AdorningLayer.Elements.Clear();

        // Use ActiveToolType to determine which adorner to create
        SKPicture adornerPicture = ActiveToolType switch
        {
            DrawingCanvasToolType.Selection => CreateSelectionAdorner(element),
            DrawingCanvasToolType.Transformation => CreateTransformationAdorner(element),
            _ => throw new InvalidOperationException("Unsupported tool type")
        };

        var adorningElement = new BlitzElement()
        {
            Picture = adornerPicture
        };

        AdorningLayer.Elements.Add(adorningElement);
        AdorningLayer.Visible = true;

        InvalidateVisual();
    }

    private SKPicture CreateSelectionAdorner(BlitzElement element)
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
            Color = _selectionColor,
            StrokeWidth = _defaultStrokeWidth / (float)_scale,
            IsAntialias = true
        };

        canvas.DrawRect(new SKRect(minX, minY, maxX, maxY), paint);

        return recorder.EndRecording();
    }

    private SKPicture CreateTransformationAdorner(BlitzElement element)
    {
        var bbox = element.BBox;
        var matrix = element.Matrix;

        // Calculate the width and height of the bounding box
        var bboxWidth = bbox.Right - bbox.Left;
        var bboxHeight = bbox.Bottom - bbox.Top;

        // Get the transformation handles using the TransformHandleProvider
        var handles = GetTransformHandles(bbox, matrix);

        using var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(new SKRect(0, 0, (float)Width, (float)Height));

        // Draw the transformed bounding box
        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = SKColor.Parse("#000000"),
            StrokeWidth = _defaultStrokeWidth / (float)_scale,
            IsAntialias = true
        };

        using var path = new SKPath();
        path.MoveTo(handles.First(h => h.Type == TransformHandleType.TopLeft).Center);
        path.LineTo(handles.First(h => h.Type == TransformHandleType.TopRight).Center);
        path.LineTo(handles.First(h => h.Type == TransformHandleType.BottomRight).Center);
        path.LineTo(handles.First(h => h.Type == TransformHandleType.BottomLeft).Center);
        path.Close();

        canvas.DrawPath(path, paint);

        // Define the size of the scale-independent boxes
        float boxSize = _transformHandleSize / (float)_scale;
        float strokeWidth = _defaultStrokeWidth / (float)_scale;

        using var boxPaint = new SKPaint
        {
            Style = SKPaintStyle.StrokeAndFill,
            Color = SKColors.Black,
            StrokeWidth = strokeWidth,
            IsAntialias = true
        };

        using var strokePaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = SKColors.White,
            StrokeWidth = strokeWidth,
            IsAntialias = true
        };

        // Helper function to draw a box
        void DrawBox(SKPoint center)
        {
            var halfSize = boxSize / 2;
            var rect = new SKRect(center.X - halfSize, center.Y - halfSize, center.X + halfSize, center.Y + halfSize);
            canvas.DrawRect(rect, boxPaint);
            canvas.DrawRect(rect, strokePaint);
        }

        // Draw the transformation handles
        foreach (var handle in handles)
        {
            DrawBox(handle.Center);
        }

        // Calculate the fixed transformation point using the provided function
        var transformationPoint = CalculateFixedTransformationPoint(matrix, element.TransformationPoint);

        // Draw the circle at the element's transformation point
        float circleRadius = 5f / (float)_scale; // 5px radius
        float circleStrokeWidth = 2f / (float)_scale; // 2px stroke width

        using var circleFillPaint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            Color = SKColors.White,
            IsAntialias = true
        };

        using var circleStrokePaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = SKColors.Black,
            StrokeWidth = circleStrokeWidth,
            IsAntialias = true
        };

        canvas.DrawCircle(transformationPoint, circleRadius, circleFillPaint);
        canvas.DrawCircle(transformationPoint, circleRadius, circleStrokePaint);

        return recorder.EndRecording();
    }

    public SKPoint CalculateFixedTransformationPoint(CsXFL.Matrix matrix, CsXFL.Point originalTransformationPoint)
    {
        // Apply the matrix transformations to the original transformation point
        float transformedX = (float)(matrix.A * originalTransformationPoint.X + matrix.C * originalTransformationPoint.Y + matrix.Tx);
        float transformedY = (float)(matrix.B * originalTransformationPoint.X + matrix.D * originalTransformationPoint.Y + matrix.Ty);

        return new SKPoint(transformedX, transformedY);
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