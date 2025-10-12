using System;
using SkiaSharp;
using System.Collections.Generic;
using Avalonia.Input;

namespace Avalonia.Controls;

// Todo:
// - Implement Double Axis Scaling
// - Implement Shearing
// Flickering when click-drag transform past transformation point (causes sign change)
// Edge skewing regions are hard to hit when zoomed in
// Does the matrix clamp intelligently when scaling?
public class TransformationTool : IDrawingCanvasTool
{
    private SKPoint _transformOrigin;
    DrawingCanvas.TransformHandleType _activeHandle;
    private const float _minScaleValue = 0.001f;
    private const double _handleMargin = 10.0;
    private const double _rotationMarginFactor = 3;
    private const double _edgeMargin = 5;
    private const double _transformPointMargin = 8.0;
    private float _initialDistance;
    private float _initialAngle;

    private enum TransformationState
    {
        None,
        DoubleScaling,
        SingleScaling,
        Rotating,
        Shearing,
        MovingTransformPoint
    }

    private TransformationState _currentState = TransformationState.None;

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
    private CsXFL.Matrix ClampMatrix(CsXFL.Matrix matrix)
    {
        // Calculate the actual scale from the matrix determinant
        double scaleX = Math.Sqrt(matrix.A * matrix.A + matrix.B * matrix.B);
        double scaleY = Math.Sqrt(matrix.C * matrix.C + matrix.D * matrix.D);
        
        // Only clamp if the actual scale is too small
        if (scaleX < _minScaleValue)
        {
            double factor = _minScaleValue / scaleX;
            matrix.A *= factor;
            matrix.B *= factor;
        }
        
        if (scaleY < _minScaleValue)
        {
            double factor = _minScaleValue / scaleY;
            matrix.C *= factor;
            matrix.D *= factor;
        }

        return matrix;
    }

    private void ApplyScaling(BlitzElement element, SKPoint transformationPoint, float horizontalScale, float verticalScale)
    {
        if (element.Picture == null || element.Matrix == null)
            return;

        if (!TryInvertMatrix(element.Matrix, out var inverseMatrix))
            return;

        var matrix = element.Matrix;

        // Transform the pivot point to local coordinates
        var localPivot = TransformPoint(transformationPoint, inverseMatrix);

        // Apply scaling in the LOCAL coordinate space without applying the current matrix
        var localScaleMatrix = SKMatrix.CreateScale(horizontalScale, verticalScale, localPivot.X, localPivot.Y);
        element.Matrix = PreConcatMatrix(element.Matrix, localScaleMatrix);

        // Enforce minimum scale constraints
        element.Matrix = ClampMatrix(element.Matrix);

        // Apply the inverse matrix to reset the SKPicture to its original state
        using var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(element.Picture.CullRect);
        var resetMatrix = new SKMatrix
        {
            ScaleX = (float)inverseMatrix.A,
            SkewY = (float)inverseMatrix.B,
            SkewX = (float)inverseMatrix.C,
            ScaleY = (float)inverseMatrix.D,
            TransX = (float)inverseMatrix.Tx,
            TransY = (float)inverseMatrix.Ty,
            Persp0 = 0,
            Persp1 = 0,
            Persp2 = 1
        };
        canvas.SetMatrix(resetMatrix);
        canvas.DrawPicture(element.Picture);
        element.Picture = recorder.EndRecording();

        // Record the scaled picture according to the scaled element matrix
        using var scaledRecorder = new SKPictureRecorder();
        var scaledCanvas = scaledRecorder.BeginRecording(element.Picture.CullRect);

        var scaledMatrix = new SKMatrix
        {
            ScaleX = (float)element.Matrix.A,
            SkewY = (float)element.Matrix.B,
            SkewX = (float)element.Matrix.C,
            ScaleY = (float)element.Matrix.D,
            TransX = (float)element.Matrix.Tx,
            TransY = (float)element.Matrix.Ty,
            Persp0 = 0,
            Persp1 = 0,
            Persp2 = 1
        };
        scaledCanvas.SetMatrix(scaledMatrix);
        scaledCanvas.DrawPicture(element.Picture);
        element.Picture = scaledRecorder.EndRecording();
    }

    private void StartSingleScale(SKPoint transformationPoint, SKPoint handlePosition, DrawingCanvas.TransformHandleType handleType, CsXFL.Matrix? currentMatrix = null)
    {
        _currentState = TransformationState.SingleScaling;

        if (currentMatrix == null)
            return;

        // Transform points to local coordinates for consistent calculation
        if (!TryInvertMatrix(currentMatrix, out var inverseMatrix))
            return;

        var localHandlePos = TransformPoint(handlePosition, inverseMatrix);
        var localTransformPoint = TransformPoint(transformationPoint, inverseMatrix);

        // Calculate the initial SIGNED distance for scaling in local coordinates
        if (handleType == DrawingCanvas.TransformHandleType.RightCenter || handleType == DrawingCanvas.TransformHandleType.LeftCenter)
        {
            _initialDistance = localHandlePos.X - localTransformPoint.X; // Keep the sign!
        }
        else if (handleType == DrawingCanvas.TransformHandleType.TopCenter || handleType == DrawingCanvas.TransformHandleType.BottomCenter)
        {
            _initialDistance = localHandlePos.Y - localTransformPoint.Y; // Keep the sign!
        }
    }

    private (float scaleX, float scaleY) UpdateSingleScale(SKPoint transformationPoint, SKPoint currentMousePosition, CsXFL.Matrix? currentMatrix = null)
    {
        float scaleX = 1.0F, scaleY = 1.0F;

        if (currentMatrix == null)
            return (scaleX, scaleY);

        // Transform mouse position and transformation point to local coordinates
        if (!TryInvertMatrix(currentMatrix, out var inverseMatrix))
            return (scaleX, scaleY);

        var localMousePos = TransformPoint(currentMousePosition, inverseMatrix);
        var localTransformPoint = TransformPoint(transformationPoint, inverseMatrix);

        // Calculate scaling factor based on the active handle using local coordinates
        if (_activeHandle == DrawingCanvas.TransformHandleType.RightCenter || _activeHandle == DrawingCanvas.TransformHandleType.LeftCenter)
        {
            float currentDistance = localMousePos.X - localTransformPoint.X;
            scaleX = currentDistance / _initialDistance; // Remove Math.Abs and handle sign properly

            // Don't update _initialDistance here - it should remain constant!
        }
        else if (_activeHandle == DrawingCanvas.TransformHandleType.TopCenter || _activeHandle == DrawingCanvas.TransformHandleType.BottomCenter)
        {
            float currentDistance = localMousePos.Y - localTransformPoint.Y;
            scaleY = currentDistance / _initialDistance; // Remove Math.Abs and handle sign properly

            // Don't update _initialDistance here - it should remain constant!
        }

        return (scaleX, scaleY);
    }
    #endregion

    #region ROTATION LOGIC
    private void StartRotation(SKPoint transformOrigin, SKPoint startPoint)
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
    // MARK: Pressed
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

                if (IsWithinMargin(skMousePosition, transformationPoint, (_transformPointMargin/canvas.Scale)))
                {
                    // Check for double click
                    if (e.ClickCount == 2)
                    {
                        Console.WriteLine("Transform Point Double Click - Centering");
                        CenterTransformationPoint(canvas.SelectedElement);
                        canvas.UpdateAdorningLayer(canvas.SelectedElement);
                        canvas.InvalidateVisual();
                        e.Handled = true;
                        return;
                    }
                    else
                    {
                        Console.WriteLine("Transform Point Move Start");
                        _currentState = TransformationState.MovingTransformPoint;
                        e.Handled = true;
                        return;
                    }
                }

                foreach (var (handlePosition, handleType) in transformHandles)
                {
                    // IF corner handles, DO Double Axis Scaling
                    if (Array.Exists(cornerHandles, h => h == handleType) && IsWithinMargin(skMousePosition, handlePosition, (_handleMargin / canvas.Scale)))
                    {
                        Console.WriteLine("Double Axis Scaling NYI.");
                        _activeHandle = handleType;
                        _currentState = TransformationState.DoubleScaling;
                        e.Handled = true;
                        return;
                    }

                    // IF middle handles, DO Single Axis Scaling
                    if (Array.Exists(middleHandles, h => h == handleType) && IsWithinMargin(skMousePosition, handlePosition, (_handleMargin / canvas.Scale)))
                    {
                        Console.WriteLine("Single Axis Scaling Start.");
                        _activeHandle = handleType;
                        _currentState = TransformationState.SingleScaling;
                        StartSingleScale(transformationPoint, handlePosition, handleType, canvas.SelectedElement.Matrix);
                        e.Handled = true;
                        return;
                    }

                    // IF 3x region around corner handles, DO Rotation
                    if (Array.Exists(cornerHandles, h => h == handleType) && IsWithinMargin(skMousePosition, handlePosition, (_handleMargin / canvas.Scale) * _rotationMarginFactor))
                    {
                        Console.WriteLine("Rotation Start");
                        _activeHandle = handleType;
                        _currentState = TransformationState.Rotating;
                        StartRotation(transformationPoint, skMousePosition);
                        e.Handled = true;
                        return;
                    }
                }

                //IF edge regions, DO Shearing
                var edges = GetEdgeRegions(canvas.SelectedElement.BBox, canvas.SelectedElement.Matrix);
                foreach (var edge in edges)
                {
                    if (IsWithinEdge(skMousePosition, edge, (_edgeMargin/canvas.Scale)))
                    {
                        Console.WriteLine("Shearing NYI.");
                        _currentState = TransformationState.Shearing;
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

    // MARK: Moved
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

        // IF Moving Transform Point
        if (_currentState.Equals(TransformationState.MovingTransformPoint) && canvas.SelectedElement != null)
        {
            var mousePosition = e.GetPosition(canvas);
            var skMousePosition = new SKPoint((float)mousePosition.X, (float)mousePosition.Y);

            // Convert mouse position to local coordinates relative to the element
            if (canvas.SelectedElement.Matrix != null && TryInvertMatrix(canvas.SelectedElement.Matrix, out var inverseMatrix))
            {
                var localPoint = TransformPoint(skMousePosition, inverseMatrix);

                // Update the transformation point
                canvas.SelectedElement.TransformationPoint.X = localPoint.X;
                canvas.SelectedElement.TransformationPoint.Y = localPoint.Y;

                Console.WriteLine($"Transform Point moved to: {localPoint.X}, {localPoint.Y}");
                
                canvas.UpdateAdorningLayer(canvas.SelectedElement);
                canvas.InvalidateVisual();
                return;
            }
        }

        // IF Single Scaling
        if (_currentState.Equals(TransformationState.SingleScaling) && canvas.SelectedElement != null)
        {
            var mousePosition = e.GetPosition(canvas);
            var skMousePosition = new SKPoint((float)mousePosition.X, (float)mousePosition.Y);

            // Update scaling factors
            var (scaleX, scaleY) = UpdateSingleScale(transformationPoint, skMousePosition, canvas.SelectedElement.Matrix);

            // Apply scaling relative to the transformation point
            Console.WriteLine($"Scaling X: {scaleX}, Y: {scaleY}");
            ApplyScaling(canvas.SelectedElement, transformationPoint, scaleX, scaleY);

            canvas.UpdateAdorningLayer(canvas.SelectedElement);
            canvas.CompositeLayersToRenderTarget();
            canvas.InvalidateVisual();
            return;
        }

        // IF Rotating
        if (_currentState.Equals(TransformationState.Rotating) && canvas.SelectedElement != null)
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

    // MARK: Released
    public void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is not DrawingCanvas canvas)
            return;

        _currentState = TransformationState.None;

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
        var transformationPoint = canvas.CalculateFixedTransformationPoint(canvas.SelectedElement.Matrix, canvas.SelectedElement.TransformationPoint);

        // Handle transformation point region
        if (IsWithinMargin(skMousePosition, transformationPoint, (_transformPointMargin / canvas.Scale)))
        {
            canvas.Cursor = new Cursor(StandardCursorType.SizeAll);
            return true;
        }

        // Handle transform handles (corners and middle handles)
        foreach (var (handlePosition, handleType) in transformHandles)
        {
            // Check for scaling regions (corner handles)
            if (Array.Exists(cornerHandles, h => h == handleType) && IsWithinMargin(skMousePosition, handlePosition, (_handleMargin / canvas.Scale)))
            {
                canvas.Cursor = new Cursor(StandardCursorType.SizeAll);
                return true;
            }

            // Check for rotation region
            if (Array.Exists(cornerHandles, h => h == handleType) && IsWithinMargin(skMousePosition, handlePosition, (_handleMargin / canvas.Scale) * _rotationMarginFactor))
            {
                canvas.Cursor = CustomCursorFactory.CreateCursor(CursorType.Rotate);
                return true;
            }

            // Check for scaling regions (middle handles)
            if (Array.Exists(middleHandles, h => h == handleType) && IsWithinMargin(skMousePosition, handlePosition, (_handleMargin / canvas.Scale)))
            {
                canvas.Cursor = new Cursor(StandardCursorType.SizeNorthSouth);
                return true;
            }
        }

        // Handle edge regions for shearing
        var edges = GetEdgeRegions(canvas.SelectedElement.BBox, canvas.SelectedElement.Matrix);
        foreach (var edge in edges)
        {
            if (IsWithinEdge(skMousePosition, edge, (_edgeMargin/canvas.Scale)))
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

    private void CenterTransformationPoint(BlitzElement element)
    {
        if (element?.BBox == null)
            return;

        // Calculate the center of the element's bounding box in local coordinates
        float centerX = (float)(element.BBox.Left + element.BBox.Right) / 2f;
        float centerY = (float)(element.BBox.Top + element.BBox.Bottom) / 2f;

        // Update the transformation point to the center
        element.TransformationPoint.X = centerX;
        element.TransformationPoint.Y = centerY;
    }
    #endregion
}