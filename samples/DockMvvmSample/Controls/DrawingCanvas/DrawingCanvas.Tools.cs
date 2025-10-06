using Avalonia.Input;
using System;
using SkiaSharp;
using CsXFL;

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

#region TRANSFORMATION
public class TransformationTool : IDrawingCanvasTool
{
    private const double _handleMargin = 10.0;
    private bool _isRotating;
    private SKPoint? _activeHandle;
    private CsXFL.Matrix _originalMatrix;

    #region ROTATION LOGIC
    private SKPoint _transformOrigin;
    private float _initialAngle;
    private float _currentRotation;

    private void StartRotation(SKPoint transformOrigin, SKPoint startPoint, float currentRotation = 0)
    {
        _transformOrigin = transformOrigin;
        _initialAngle = CalculateAngle(transformOrigin, startPoint);
        _currentRotation = currentRotation;
    }

    private float UpdateRotation(SKPoint currentPoint)
    {
        // Calculate the current angle from the transformation origin to the current point
        float currentAngle = CalculateAngle(_transformOrigin, currentPoint);

        // Calculate the delta angle relative to the last known angle
        float deltaAngle = currentAngle - _initialAngle;

        // Update the initial angle to the current angle for the next calculation
        _initialAngle = currentAngle;

        // Return the delta angle
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

    private float GetDistanceFromOrigin(SKPoint point)
    {
        float dx = point.X - _transformOrigin.X;
        float dy = point.Y - _transformOrigin.Y;
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }
    #endregion

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

                foreach (var (handlePosition, handleType) in transformHandles)
                {
                    if (IsWithinMargin(skMousePosition, handlePosition, _handleMargin))
                    {
                        // Rotate
                        _activeHandle = handlePosition;
                        _originalMatrix = canvas.SelectedElement.Matrix;
                        _isRotating = true;
                        var transformationPoint = canvas.CalculateFixedTransformationPoint(canvas.SelectedElement.Matrix, canvas.SelectedElement.TransformationPoint);
                        StartRotation(transformationPoint, skMousePosition, 0);
                        e.Handled = true;
                        return;
                    }
                }
            }

            canvas.SelectedElement = canvas.HitTest(mousePosition);

            if (canvas.SelectedElement != null)
            {
                // Duplicate selection-drag logic here
                canvas.AdorningLayer.Visible = true;
                canvas.UpdateAdorningLayer(canvas.SelectedElement);
                canvas.InvalidateVisual();

                canvas.DragStartPosition = mousePosition;
                canvas.IsDragging = true;
                e.Handled = true;
            }
            else
            {
                // Deselect
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

        if (_isRotating && _activeHandle.HasValue && canvas.SelectedElement != null)
        {
            var mousePosition = e.GetPosition(canvas);
            var skMousePosition = new SKPoint((float)mousePosition.X, (float)mousePosition.Y);

            // Calculate the transformation point (top-left of the bounding box + offset)
            var bbox = canvas.SelectedElement.BBox;
            var matrix = canvas.SelectedElement.Matrix;
            SKPoint transformationPoint = canvas.CalculateFixedTransformationPoint(canvas.SelectedElement.Matrix, canvas.SelectedElement.TransformationPoint);

            // Get the delta rotation angle
            float deltaRotation = UpdateRotation(skMousePosition);

            // Apply the delta rotation
            ApplyRotation(canvas.SelectedElement, transformationPoint, deltaRotation, _originalMatrix);


            _activeHandle = skMousePosition;

            canvas.UpdateAdorningLayer(canvas.SelectedElement);
            canvas.CompositeLayersToRenderTarget();
            canvas.InvalidateVisual();
            return;
        }

        if (canvas.SelectedElement != null)
        {
            var mousePosition = e.GetPosition(canvas);
            var skMousePosition = new SKPoint((float)mousePosition.X, (float)mousePosition.Y);
            var transformHandles = canvas.GetTransformHandles(canvas.SelectedElement.BBox, canvas.SelectedElement.Matrix);

            foreach (var (handlePosition, _) in transformHandles)
            {
                if (IsWithinMargin(skMousePosition, handlePosition, _handleMargin))
                {
                    canvas.Cursor = new Cursor(StandardCursorType.Hand); // Rotation cursor
                    return;
                }
            }
        }

        // Default cursor
        canvas.Cursor = new Cursor(StandardCursorType.Arrow);
    }

    public void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isRotating)
        {
            _isRotating = false;
            _activeHandle = null;
        }
    }
    #endregion

    #region HELPERS
    private void ApplyRotation(BlitzElement element, SKPoint transformationPoint, float angle, CsXFL.Matrix originalMatrix)
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

    private bool IsWithinMargin(SKPoint mousePosition, SKPoint handlePosition, double margin)
    {
        return Math.Abs(mousePosition.X - handlePosition.X) <= margin &&
               Math.Abs(mousePosition.Y - handlePosition.Y) <= margin;
    }

    private SKPoint TransformPoint(CsXFL.Matrix matrix, double x, double y)
    {
        return new SKPoint(
            (float)(matrix.A * x + matrix.C * y + matrix.Tx),
            (float)(matrix.B * x + matrix.D * y + matrix.Ty)
        );
    }
    #endregion
}