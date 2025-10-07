using Avalonia.Input;
using System;
using SkiaSharp;
using System.Collections.Generic;
using System.Linq;

namespace Avalonia.Controls;

public enum DrawingCanvasToolType
{
    Selection,
    Transformation
}

public interface IDrawingCanvasTool
{
    void OnPointerPressed(object? sender, PointerPressedEventArgs e);
    void OnPointerMoved(object? sender, PointerEventArgs e);
    void OnPointerReleased(object? sender, PointerReleasedEventArgs e);

}

#region SELECTION
public class SelectionTool : IDrawingCanvasTool
{
    public void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not DrawingCanvas canvas)
            return;

        if (e.Handled)
        {
            return;
        }

        if (e.GetCurrentPoint(canvas).Properties.IsLeftButtonPressed)
        {
            var mousePosition = e.GetPosition(canvas);
            canvas.SelectedElement = canvas.HitTest(mousePosition);

            if (canvas.SelectedElement != null)
            {
                canvas.AdorningLayer.Visible = true;
                canvas.UpdateAdorningLayer(canvas.SelectedElement);
                canvas.InvalidateVisual();

                canvas.DragStartPosition = mousePosition;
                canvas.IsDragging = true;
                e.Handled = true;
            }
            else
            {
                canvas.SelectedElement = null;
                canvas.AdorningLayer.Elements.Clear();
                canvas.AdorningLayer.Visible = false;
                canvas.InvalidateVisual();
            }
        }
    }

    public void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (sender is not DrawingCanvas canvas)
            return;

        if (canvas.IsDragging && canvas.SelectedElement != null)
        {
            var currentPosition = e.GetPosition(canvas);
            var deltaX = currentPosition.X - canvas.DragStartPosition.X;
            var deltaY = currentPosition.Y - canvas.DragStartPosition.Y;

            ApplySkiaTranslation(canvas.SelectedElement, deltaX, deltaY);

            // Update the model's matrix translation
            canvas.SelectedElement.Matrix.Tx += deltaX;
            canvas.SelectedElement.Matrix.Ty += deltaY;

            canvas.DragStartPosition = currentPosition;

            canvas.UpdateAdorningLayer(canvas.SelectedElement);
            canvas.CompositeLayersToRenderTarget();
            canvas.InvalidateVisual();
        }
    }

    public void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is not DrawingCanvas canvas)
            return;

        if (canvas.IsDragging)
        {
            canvas.IsDragging = false;

            if (canvas.SelectedElement != null && canvas.SelectedElement.Model != null)
            {
                // Update the model's matrix translation
                canvas.SelectedElement.Model.Matrix.Tx = canvas.SelectedElement.Matrix.Tx;
                canvas.SelectedElement.Model.Matrix.Ty = canvas.SelectedElement.Matrix.Ty;
            }

            e.Handled = true;
        }
    }

    private void ApplySkiaTranslation(BlitzElement element, double deltaX, double deltaY)
    {
        if (element.Picture == null)
            return;

        using var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(element.Picture.CullRect);
        canvas.Translate((float)deltaX, (float)deltaY);
        canvas.DrawPicture(element.Picture);
        element.Picture = recorder.EndRecording();
    }
}
#endregion

public class TransformationTool : IDrawingCanvasTool
{
    private const double _handleMargin = 10.0;
    private const double _rotationMarginFactor = 3;
    private const double _edgeMargin = 5;
    private SKPoint? _activeHandle;
    
    // Transformation States
    private bool _isDoubleScaling;
    private bool _isSingleScaling;
    private bool _isRotating;
    private bool _isShearing;

    DrawingCanvas.TransformHandleType[] cornerHandles = new[]
    {
        DrawingCanvas.TransformHandleType.BottomLeft,
        DrawingCanvas.TransformHandleType.TopLeft,
        DrawingCanvas.TransformHandleType.BottomRight,
        DrawingCanvas.TransformHandleType.TopRight
    };

    DrawingCanvas.TransformHandleType[] middleHandles = new[]
    {
        DrawingCanvas.TransformHandleType.BottomCenter,
        DrawingCanvas.TransformHandleType.TopCenter,
        DrawingCanvas.TransformHandleType.RightCenter,
        DrawingCanvas.TransformHandleType.LeftCenter
    };

    #region SCALING LOGIC
    private void ApplyScaling(BlitzElement element, SKPoint transformationPoint, float horizontalScale, float verticalScale)
    {
        if (element.Picture == null || element.Matrix == null)
            return;

        var matrix = element.Matrix;

        if (!TryInvertMatrix(matrix, out var inverseMatrix))
            return;

        // Transform the pivot point to local coordinates
        var localPivot = TransformPoint(transformationPoint, inverseMatrix);

        // Apply scaling in the LOCAL coordinate space without applying the current matrix
        var localScaleMatrix = SKMatrix.CreateScale(horizontalScale, verticalScale, localPivot.X, localPivot.Y);
        element.Matrix = PreConcatMatrix(matrix, localScaleMatrix);



        using var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(element.Picture.CullRect);

        // Extract the rotation angle from the matrix
        float rotationAngle = (float)Math.Atan2(element.Matrix.B, element.Matrix.A) * (180 / (float)Math.PI);
        float skewX = (float)Math.Atan2(element.Matrix.C, element.Matrix.D) * (180 / (float)Math.PI);
        float skewY = (float)Math.Atan2(element.Matrix.B, element.Matrix.A) * (180 / (float)Math.PI);
        Console.WriteLine($"Rotation Angle: {rotationAngle}");

        // Apply the opposite rotation to make the canvas upright
        canvas.RotateDegrees(rotationAngle, transformationPoint.X, transformationPoint.Y);
        canvas.Scale(horizontalScale, verticalScale, transformationPoint.X, transformationPoint.Y);
        canvas.RotateDegrees(-rotationAngle, transformationPoint.X, transformationPoint.Y);

        // Draw the picture
        canvas.DrawPicture(element.Picture);

        // End recording
        element.Picture = recorder.EndRecording();
    }

    private bool TryInvertMatrix(CsXFL.Matrix matrix, out CsXFL.Matrix inverseMatrix)
    {
        inverseMatrix = new CsXFL.Matrix();
        double determinant = matrix.A * matrix.D - matrix.B * matrix.C;
        if (Math.Abs(determinant) < double.Epsilon)
            return false;

        inverseMatrix.A = matrix.D / determinant;
        inverseMatrix.B = -matrix.B / determinant;
        inverseMatrix.C = -matrix.C / determinant;
        inverseMatrix.D = matrix.A / determinant;
        inverseMatrix.Tx = (matrix.C * matrix.Ty - matrix.D * matrix.Tx) / determinant;
        inverseMatrix.Ty = (matrix.B * matrix.Tx - matrix.A * matrix.Ty) / determinant;

        return true;
    }

    private CsXFL.Matrix PreConcatMatrix(CsXFL.Matrix matrix, SKMatrix skMatrix)
    {
        var result = new CsXFL.Matrix();
        result.A = matrix.A * skMatrix.ScaleX + matrix.C * skMatrix.SkewY;
        result.B = matrix.B * skMatrix.ScaleX + matrix.D * skMatrix.SkewY;
        result.C = matrix.A * skMatrix.SkewX + matrix.C * skMatrix.ScaleY;
        result.D = matrix.B * skMatrix.SkewX + matrix.D * skMatrix.ScaleY;
        result.Tx = matrix.A * skMatrix.TransX + matrix.C * skMatrix.TransY + matrix.Tx;
        result.Ty = matrix.B * skMatrix.TransX + matrix.D * skMatrix.TransY + matrix.Ty;
        return result;
    }
    #endregion

    #region ROTATION LOGIC
    private SKPoint _transformOrigin;
    private float _initialAngle;

    private void StartRotation(SKPoint transformOrigin, SKPoint startPoint, float currentRotation = 0)
    {
        _transformOrigin = transformOrigin;
        _initialAngle = CalculateAngle(transformOrigin, startPoint);
    }

    private float UpdateRotation(SKPoint currentPoint)
    {
        float currentAngle = CalculateAngle(_transformOrigin, currentPoint);
        float deltaAngle = currentAngle - _initialAngle;
        _initialAngle = currentAngle;
        return deltaAngle;
    }

    private float CalculateAngle(SKPoint origin, SKPoint point)
    {
        // Convert to vector from origin
        float dx = point.X - origin.X;
        float dy = point.Y - origin.Y;

        // Calculate angle in radians, then convert to degrees
        float angleRadians = (float)Math.Atan2(dy, dx);
        float angleDegrees = (float)(angleRadians * (180 / Math.PI));

        // Normalize to 0-360 range
        return (angleDegrees + 360) % 360;
    }

    private void ApplyRotation(BlitzElement element, SKPoint transformationPoint, float angle)
    {
        if (element.Picture == null)
            return;

        if (element == null || element.Matrix == null)
            return;

        // Convert angle to radians
        float radians = angle * (float)(Math.PI / 180);

        // Precompute sine and cosine of the angle
        float cos = (float)Math.Cos(radians);
        float sin = (float)Math.Sin(radians);

        // Step 1: Translate to the transformation point
        float txToOrigin = (float)(element.Matrix.Tx - transformationPoint.X);
        float tyToOrigin = (float)(element.Matrix.Ty - transformationPoint.Y);

        // Step 2: Apply rotation
        double rotatedA = element.Matrix.A * cos - element.Matrix.B * sin;
        double rotatedB = element.Matrix.A * sin + element.Matrix.B * cos;
        double rotatedC = element.Matrix.C * cos - element.Matrix.D * sin;
        double rotatedD = element.Matrix.C * sin + element.Matrix.D * cos;

        float rotatedTx = txToOrigin * cos - tyToOrigin * sin;
        float rotatedTy = txToOrigin * sin + tyToOrigin * cos;

        // Step 3: Translate back from the transformation point
        rotatedTx += transformationPoint.X;
        rotatedTy += transformationPoint.Y;

        // Update the element's matrix
        element.Matrix = new CsXFL.Matrix
        {
            A = rotatedA,
            B = rotatedB,
            C = rotatedC,
            D = rotatedD,
            Tx = rotatedTx,
            Ty = rotatedTy
        };

        // Get the transformation matrix for the rotation
        var skMatrix = GetRotationMatrix(transformationPoint, angle);

        // Update the element's Picture using the SkiaSharp transformation
        using var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(element.Picture.CullRect);
        canvas.SetMatrix(skMatrix);
        canvas.DrawPicture(element.Picture);
        element.Picture = recorder.EndRecording();
    }

    private SKMatrix GetRotationMatrix(SKPoint transformationPoint, float angle)
    {
        var translateToPoint = SKMatrix.CreateTranslation(transformationPoint.X, transformationPoint.Y);
        var rotationMatrix = SKMatrix.CreateRotationDegrees(angle);
        var translateBack = SKMatrix.CreateTranslation(-transformationPoint.X, -transformationPoint.Y);

        SKMatrix result = translateToPoint;
        SKMatrix.Concat(ref result, result, rotationMatrix);
        SKMatrix.Concat(ref result, result, translateBack);

        return result;
    }
    #endregion

    #region TRANSFORMATION
    public void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not DrawingCanvas canvas)
            return;

        if (e.Handled)
        {
            return;
        }

        if (e.GetCurrentPoint(canvas).Properties.IsLeftButtonPressed)
        {
            var mousePosition = e.GetPosition(canvas);
            var skMousePosition = new SKPoint((float)mousePosition.X, (float)mousePosition.Y);

            if (canvas.SelectedElement != null)
            {
                var transformHandles = canvas.GetTransformHandles(canvas.SelectedElement.BBox, canvas.SelectedElement.Matrix);
                var transformationPoint = canvas.CalculateFixedTransformationPoint(canvas.SelectedElement.Matrix, canvas.SelectedElement.TransformationPoint);

                foreach (var (handlePosition, handleType) in transformHandles)
                {
                    // IF corner handles, DO Double Axis Scaling
                    if (Array.Exists(cornerHandles, h => h == handleType) && IsWithinMargin(skMousePosition, handlePosition, _handleMargin))
                    {
                        Console.WriteLine("Double Axis Scaling NYI.");
                        _activeHandle = handlePosition;
                        _isDoubleScaling = true;
                        e.Handled = true;
                        return;
                    }

                    // IF middle handles, DO Single Axis Scaling
                    if (Array.Exists(middleHandles, h => h == handleType) && IsWithinMargin(skMousePosition, handlePosition, _handleMargin))
                    {
                        Console.WriteLine("Single Axis Scaling Start.");
                        _activeHandle = handlePosition;
                        _isSingleScaling = true;
                        e.Handled = true;
                        return;
                    }

                    // IF 3x region around corner handles, DO Rotation
                    if (Array.Exists(cornerHandles, h => h == handleType) && IsWithinMargin(skMousePosition, handlePosition, _handleMargin * _rotationMarginFactor))
                    {
                        Console.WriteLine("Rotation Start");
                        _activeHandle = handlePosition;
                        _isRotating = true;
                        StartRotation(transformationPoint, skMousePosition, 0);
                        e.Handled = true;
                        return;
                    }
                }

                //IF edge regions, DO Shearing
                var edges = GetEdgeRegions(canvas.SelectedElement.BBox, canvas.SelectedElement.Matrix);
                foreach (var edge in edges)
                {
                    if (IsWithinEdge(skMousePosition, edge, _edgeMargin))
                    {
                        Console.WriteLine("Shearing NYI.");
                        e.Handled = true;
                        return;
                    }
                }
                
                // TODO: Add selection-drag logic.
            }

            // Default selection logic
            canvas.SelectedElement = canvas.HitTest(mousePosition);

            if (canvas.SelectedElement != null)
            {
                canvas.AdorningLayer.Visible = true;
                canvas.UpdateAdorningLayer(canvas.SelectedElement);
                canvas.InvalidateVisual();
                e.Handled = true;
            }
            else
            {
                canvas.SelectedElement = null;
                canvas.AdorningLayer.Elements.Clear();
                canvas.AdorningLayer.Visible = false;
                canvas.InvalidateVisual();
            }
        }
    }

    public void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (sender is not DrawingCanvas canvas)
            return;

        var transformHandles = new List<(SKPoint Center, DrawingCanvas.TransformHandleType Type)>();
        var transformationPoint = new SKPoint();
        if (canvas.SelectedElement != null)
        {
            transformHandles = canvas.GetTransformHandles(canvas.SelectedElement.BBox, canvas.SelectedElement.Matrix);
            transformationPoint = canvas.CalculateFixedTransformationPoint(canvas.SelectedElement.Matrix, canvas.SelectedElement.TransformationPoint);
        }

        // IF Single Scaling
        if (_isSingleScaling && canvas.SelectedElement != null)
        {
            var mousePosition = e.GetPosition(canvas);
            var skMousePosition = new SKPoint((float)mousePosition.X, (float)mousePosition.Y);

            // TODO: You suck
            float scaleX = 1.01F, scaleY = 1.00F;

            // Apply scaling relative to the bounding box
            Console.WriteLine($"Scaling X: {scaleX}, Y: {scaleY}");
            ApplyScaling(canvas.SelectedElement, transformationPoint, scaleX, scaleY);

            canvas.UpdateAdorningLayer(canvas.SelectedElement);
            canvas.CompositeLayersToRenderTarget();
            canvas.InvalidateVisual();
            return;
        }

        // IF Rotating
        if (_isRotating && _activeHandle.HasValue && canvas.SelectedElement != null)
        {
            var mousePosition = e.GetPosition(canvas);
            var skMousePosition = new SKPoint((float)mousePosition.X, (float)mousePosition.Y);

            // Calculate the transformation point (top-left of the bounding box + offset)
            var bbox = canvas.SelectedElement.BBox;
            var matrix = canvas.SelectedElement.Matrix;

            // Get the delta rotation angle
            float deltaRotation = UpdateRotation(skMousePosition);

            // Apply the delta rotation
            ApplyRotation(canvas.SelectedElement, transformationPoint, deltaRotation);

            _activeHandle = skMousePosition;

            canvas.UpdateAdorningLayer(canvas.SelectedElement);
            canvas.CompositeLayersToRenderTarget();
            canvas.InvalidateVisual();
            return;
        }

        // Handle Cursors
        if (canvas.SelectedElement != null)
        {
            var mousePosition = e.GetPosition(canvas);
            var skMousePosition = new SKPoint((float)mousePosition.X, (float)mousePosition.Y);
            TryHandleCursorAndRegions(canvas, skMousePosition, transformHandles);
            return;
        }

        // Default cursor if no selection
        canvas.Cursor = new Cursor(StandardCursorType.Arrow);
    }

    public void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is not DrawingCanvas canvas)
            return;

        _isDoubleScaling = false;
        _isSingleScaling = false;
        _isRotating = false;
        _isShearing = false;
        _activeHandle = null;

        if (canvas.SelectedElement != null && canvas.SelectedElement.Model != null)
        {
            // Update the model's matrix translation
            canvas.SelectedElement.Model.Matrix.Tx = canvas.SelectedElement.Matrix.Tx;
            canvas.SelectedElement.Model.Matrix.Ty = canvas.SelectedElement.Matrix.Ty;
        }
    }
    #endregion

    #region HELPERS
    private bool TryHandleCursorAndRegions(DrawingCanvas canvas, SKPoint skMousePosition, List<(SKPoint Center, DrawingCanvas.TransformHandleType Type)> transformHandles)
    {
        // Handle transform handles (corners and middle handles)
        foreach (var (handlePosition, handleType) in transformHandles)
        {
            // Check for scaling regions (corner handles)
            if (Array.Exists(cornerHandles, h => h == handleType) && IsWithinMargin(skMousePosition, handlePosition, _handleMargin))
            {
                canvas.Cursor = new Cursor(StandardCursorType.SizeAll);
                return true;
            }

            // Check for rotation region
            if (Array.Exists(cornerHandles, h => h == handleType) && IsWithinMargin(skMousePosition, handlePosition, _handleMargin * _rotationMarginFactor))
            {
                canvas.Cursor = CustomCursorFactory.CreateCursor(CursorType.Rotate);
                return true;
            }

            // Check for scaling regions (middle handles)
            if (Array.Exists(middleHandles, h => h == handleType) && IsWithinMargin(skMousePosition, handlePosition, _handleMargin))
            {
                canvas.Cursor = new Cursor(StandardCursorType.SizeNorthSouth);
                return true;
            }
        }

        // Handle edge regions for shearing
        var edges = GetEdgeRegions(canvas.SelectedElement.BBox, canvas.SelectedElement.Matrix);
        foreach (var edge in edges)
        {
            if (IsWithinEdge(skMousePosition, edge, _edgeMargin))
            {
                canvas.Cursor = CustomCursorFactory.CreateCursor(CursorType.Skew);
                return true;
            }
        }

        // Default cursor
        canvas.Cursor = new Cursor(StandardCursorType.Arrow);
        return false;
    }

    public IEnumerable<SKRect> GetEdgeRegions(CsXFL.Rectangle boundingBox, CsXFL.Matrix matrix)
    {
        // Transform the corners of the bounding box using the matrix
        var topLeft = TransformPoint(new SKPoint((float)boundingBox.Left, (float)boundingBox.Top), matrix);
        var topRight = TransformPoint(new SKPoint((float)boundingBox.Right, (float)boundingBox.Top), matrix);
        var bottomLeft = TransformPoint(new SKPoint((float)boundingBox.Left, (float)boundingBox.Bottom), matrix);
        var bottomRight = TransformPoint(new SKPoint((float)boundingBox.Right, (float)boundingBox.Bottom), matrix);

        // Define edge regions as thin rectangles along the edges of the bounding box
        var edgeRegions = new List<SKRect>
        {
            new SKRect(topLeft.X, topLeft.Y, topRight.X, topRight.Y), // Top edge
            new SKRect(bottomLeft.X, bottomLeft.Y, bottomRight.X, bottomRight.Y), // Bottom edge
            new SKRect(topLeft.X, topLeft.Y, bottomLeft.X, bottomLeft.Y), // Left edge
            new SKRect(topRight.X, topRight.Y, bottomRight.X, bottomRight.Y) // Right edge
        };

        return edgeRegions;
    }

    private SKPoint TransformPoint(SKPoint point, CsXFL.Matrix matrix)
    {
        // Apply the transformation matrix to the point
        return new SKPoint(
            (float)(matrix.A * point.X + matrix.C * point.Y + matrix.Tx),
            (float)(matrix.B * point.X + matrix.D * point.Y + matrix.Ty)
        );
    }

    private bool IsWithinMargin(SKPoint mousePosition, SKPoint handlePosition, double margin)
    {
        return Math.Abs(mousePosition.X - handlePosition.X) <= margin &&
               Math.Abs(mousePosition.Y - handlePosition.Y) <= margin;
    }

    private bool IsWithinEdge(SKPoint mousePosition, SKRect edge, double margin)
    {
        return mousePosition.X >= edge.Left - margin && mousePosition.X <= edge.Right + margin &&
            mousePosition.Y >= edge.Top - margin && mousePosition.Y <= edge.Bottom + margin;
    }
    #endregion
}