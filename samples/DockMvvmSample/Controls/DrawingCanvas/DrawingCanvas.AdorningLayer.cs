using SixLabors.ImageSharp;
using SkiaSharp;
using System;
using System.Linq;

namespace Avalonia.Controls;

public partial class DrawingCanvas
{
    public TransformationToolConfig TransformConfig { get; set; } = TransformationToolConfig.Default;

    private SKColor _selectionColor = SKColor.Parse("#388ff9");
    private float _defaultStrokeWidth = 2.0f;

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
        var skRect = element.GetTransformedBBox();

        using var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(skRect);

        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = _selectionColor,
            StrokeWidth = _defaultStrokeWidth / (float)_scale,
            IsAntialias = true
        };

        canvas.DrawRect(skRect, paint);

        return recorder.EndRecording();
    }

    private SKPicture CreateTransformationAdorner(BlitzElement element)
    {
        var bbox = element.BBox;
        var matrix = element.Matrix;

        // Get the transformed bounding box
        var transformedRect = element.GetTransformedBBox();

        // Get the transformation handles using the TransformHandleProvider
        var handles = GetTransformHandles(bbox, matrix);

        using var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(transformedRect);

        // Draw the transformed bounding box
        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = TransformConfig.TransformAdornerInteriorColor,
            StrokeWidth = TransformConfig.TransformAdornerStrokeWidth / (float)_scale,
            IsAntialias = true
        };

        using var path = new SKPath();
        path.MoveTo(handles.First(h => h.Type == TransformHandleType.TopLeft).Center);
        path.LineTo(handles.First(h => h.Type == TransformHandleType.TopRight).Center);
        path.LineTo(handles.First(h => h.Type == TransformHandleType.BottomRight).Center);
        path.LineTo(handles.First(h => h.Type == TransformHandleType.BottomLeft).Center);
        path.Close();

        canvas.DrawPath(path, paint);

        DrawZeroPointCrosshair(canvas, element);

        // Define the size of the scale-independent boxes
        float boxSize = TransformConfig.TransformAdornerHandleSize / (float)_scale;
        float strokeWidth = TransformConfig.TransformAdornerStrokeWidth / (float)_scale;

        using var boxPaint = new SKPaint
        {
            Style = SKPaintStyle.StrokeAndFill,
            Color = TransformConfig.TransformAdornerInteriorColor,
            StrokeWidth = strokeWidth,
            IsAntialias = true
        };

        using var strokePaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = TransformConfig.TransformAdornerBorderColor,
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
        float circleRadius = TransformConfig.TransformationPointRadius / (float)_scale;
        float circleStrokeWidth = TransformConfig.TransformAdornerStrokeWidth / (float)_scale;

        using var circleFillPaint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            Color = TransformConfig.TransformAdornerBorderColor,
            IsAntialias = true
        };

        using var circleStrokePaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = TransformConfig.TransformAdornerInteriorColor,
            StrokeWidth = circleStrokeWidth,
            IsAntialias = true
        };

        canvas.DrawCircle(transformationPoint, circleRadius, circleFillPaint);
        canvas.DrawCircle(transformationPoint, circleRadius, circleStrokePaint);

        return recorder.EndRecording();
    }

    private void DrawZeroPointCrosshair(SKCanvas canvas, BlitzElement element)
    {
        // Get the zero point in parent coordinates
        var zeroPoint = GetElementZeroPoint(element);
        
        using var debugPaint = new SKPaint
        {
            Color = TransformConfig.TransformAdornerInteriorColor,
            StrokeWidth = 2.0f / (float)_scale, // Scale-independent stroke width
            Style = SKPaintStyle.Stroke,
            IsAntialias = true
        };
        
        // Draw crosshairs at zero point
        float crossSize = 10f / (float)_scale; // Scale-independent size
        canvas.DrawLine(
            zeroPoint.X - crossSize, zeroPoint.Y, 
            zeroPoint.X + crossSize, zeroPoint.Y, 
            debugPaint);
        canvas.DrawLine(
            zeroPoint.X, zeroPoint.Y - crossSize, 
            zeroPoint.X, zeroPoint.Y + crossSize, 
            debugPaint);
        
        // Draw a small circle with fill and border
        using var fillPaint = new SKPaint
        {
            Color = TransformConfig.TransformAdornerInteriorColor,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        
        using var borderPaint = new SKPaint
        {
            Color = TransformConfig.TransformAdornerBorderColor,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.0f / (float)_scale,
            IsAntialias = true
        };
        
        canvas.DrawCircle(zeroPoint, 3f / (float)_scale, fillPaint);
        canvas.DrawCircle(zeroPoint, 3f / (float)_scale, borderPaint);
    }

    private SKPoint GetElementZeroPoint(BlitzElement element)
    {
        // The zero point is always (0, 0) in the element's coordinate system
        // Transform it through the current matrix to get parent coordinates
        if (element.Matrix == null)
            return new SKPoint(0, 0);
        
        // Transform the zero point through the current matrix
        var zeroPoint = new SKPoint(0, 0);
        var skMatrix = ConvertToSKMatrix(element.Matrix);
        return skMatrix.MapPoint(zeroPoint);
    }

    private SKMatrix ConvertToSKMatrix(CsXFL.Matrix matrix)
    {
        return new SKMatrix
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
    }

    public SKPoint CalculateFixedTransformationPoint(CsXFL.Matrix matrix, CsXFL.Point originalTransformationPoint)
    {
        return TransformPoint(matrix, originalTransformationPoint.X, originalTransformationPoint.Y);
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