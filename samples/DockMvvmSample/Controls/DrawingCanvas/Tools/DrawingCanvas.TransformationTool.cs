using System;
using SkiaSharp;
using System.Collections.Generic;
using Avalonia.Input;
using System.Linq;

namespace Avalonia.Controls;

// Todo:
// CLEANUP
// - Implement Shearing
public class TransformationTool : IDrawingCanvasTool
{
    private SKPoint _transformOrigin;
    private SKPoint _initialShearPoint;
    private ShearAxis _shearAxis;
    DrawingCanvas.TransformHandleType _activeHandle;

    private const float _minScaleValue = 0.001f;
    private const double _handleMargin = 10.0;
    private const double _rotationMarginFactor = 3;
    private const double _edgeMargin = 5;
    private const double _transformPointMargin = 8.0;
    private const double _transformationPointSnapMargin = 15.0;


    private float _initialDistance;
    private float _initialAngle;
    private float _initialDistanceX;
    private float _initialDistanceY;
    private float _initialShearOffsetX;
    private float _initialShearOffsetY;
    private bool _uniformDoubleScaling = false;
    private bool _isSnapping = false;

    private enum TransformationState
    {
        None,
        DoubleScaling,
        SingleScaling,
        Rotating,
        Shearing,
        MovingTransformPoint
    };

    private enum ShearAxis
    {
        Horizontal,
        Vertical
    };

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
            _initialDistance = localHandlePos.X - localTransformPoint.X;

            // Ensure minimum distance to prevent division by zero
            if (Math.Abs(_initialDistance) < float.Epsilon)
                _initialDistance = _initialDistance >= 0 ? float.Epsilon : -float.Epsilon;
        }
        else if (handleType == DrawingCanvas.TransformHandleType.TopCenter || handleType == DrawingCanvas.TransformHandleType.BottomCenter)
        {
            _initialDistance = localHandlePos.Y - localTransformPoint.Y;

            // Ensure minimum distance to prevent division by zero
            if (Math.Abs(_initialDistance) < float.Epsilon)
                _initialDistance = _initialDistance >= 0 ? float.Epsilon : -float.Epsilon;
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

            // Prevent division by zero and ensure valid scale values
            if (Math.Abs(_initialDistance) > float.Epsilon)
            {
                scaleX = currentDistance / _initialDistance;

                // Clamp scale to prevent extreme values and NaN
                if (float.IsNaN(scaleX) || float.IsInfinity(scaleX))
                    scaleX = 1.0f;
                else
                    scaleX = Math.Max(Math.Min(scaleX, 1000f), -1000f); // Reasonable limits
            }
        }
        else if (_activeHandle == DrawingCanvas.TransformHandleType.TopCenter || _activeHandle == DrawingCanvas.TransformHandleType.BottomCenter)
        {
            float currentDistance = localMousePos.Y - localTransformPoint.Y;

            // Prevent division by zero and ensure valid scale values
            if (Math.Abs(_initialDistance) > float.Epsilon)
            {
                scaleY = currentDistance / _initialDistance;

                // Clamp scale to prevent extreme values and NaN
                if (float.IsNaN(scaleY) || float.IsInfinity(scaleY))
                    scaleY = 1.0f;
                else
                    scaleY = Math.Max(Math.Min(scaleY, 1000f), -1000f); // Reasonable limits
            }
        }

        return (scaleX, scaleY);
    }

    private void StartDoubleScale(SKPoint transformationPoint, SKPoint handlePosition, DrawingCanvas.TransformHandleType handleType, CsXFL.Matrix? currentMatrix = null)
    {
        _currentState = TransformationState.DoubleScaling;

        if (currentMatrix == null)
            return;

        // Transform points to local coordinates for consistent calculation
        if (!TryInvertMatrix(currentMatrix, out var inverseMatrix))
            return;

        var localHandlePos = TransformPoint(handlePosition, inverseMatrix);
        var localTransformPoint = TransformPoint(transformationPoint, inverseMatrix);

        if (_uniformDoubleScaling)
        {
            // Calculate the initial distance from transformation point to handle in local coordinates
            float deltaX = localHandlePos.X - localTransformPoint.X;
            float deltaY = localHandlePos.Y - localTransformPoint.Y;
            _initialDistance = (float)Math.Sqrt(deltaX * deltaX + deltaY * deltaY);

            // Ensure minimum distance to prevent division by zero
            if (Math.Abs(_initialDistance) < float.Epsilon)
                _initialDistance = float.Epsilon;
        }
        else
        {
            // Calculate initial signed distances for each axis separately
            _initialDistanceX = localHandlePos.X - localTransformPoint.X;
            _initialDistanceY = localHandlePos.Y - localTransformPoint.Y;

            // Ensure minimum distances to prevent division by zero
            if (Math.Abs(_initialDistanceX) < float.Epsilon)
                _initialDistanceX = _initialDistanceX >= 0 ? float.Epsilon : -float.Epsilon;
            if (Math.Abs(_initialDistanceY) < float.Epsilon)
                _initialDistanceY = _initialDistanceY >= 0 ? float.Epsilon : -float.Epsilon;
        }
    }

    private (float scaleX, float scaleY) UpdateDoubleScale(SKPoint transformationPoint, SKPoint currentMousePosition, CsXFL.Matrix? currentMatrix = null)
    {
        float scaleX = 1.0F, scaleY = 1.0F;

        if (currentMatrix == null)
            return (scaleX, scaleY);

        // Transform mouse position and transformation point to local coordinates
        if (!TryInvertMatrix(currentMatrix, out var inverseMatrix))
            return (scaleX, scaleY);

        var localMousePos = TransformPoint(currentMousePosition, inverseMatrix);
        var localTransformPoint = TransformPoint(transformationPoint, inverseMatrix);

        if (_uniformDoubleScaling)
        {
            // Calculate current distance from transformation point to mouse position
            float deltaX = localMousePos.X - localTransformPoint.X;
            float deltaY = localMousePos.Y - localTransformPoint.Y;
            float currentDistance = (float)Math.Sqrt(deltaX * deltaX + deltaY * deltaY);

            // Prevent division by zero and ensure valid scale values
            if (Math.Abs(_initialDistance) > float.Epsilon)
            {
                float uniformScale = currentDistance / _initialDistance;

                // Clamp scale to prevent extreme values and NaN
                if (float.IsNaN(uniformScale) || float.IsInfinity(uniformScale))
                    uniformScale = 1.0f;
                else
                    uniformScale = Math.Max(Math.Min(uniformScale, 1000f), -1000f); // Reasonable limits

                scaleX = uniformScale;
                scaleY = uniformScale;
            }
        }
        else
        {
            // Calculate current signed distances for each axis
            float currentDistanceX = localMousePos.X - localTransformPoint.X;
            float currentDistanceY = localMousePos.Y - localTransformPoint.Y;

            // Calculate scale factors for each axis independently
            if (Math.Abs(_initialDistanceX) > float.Epsilon)
            {
                scaleX = currentDistanceX / _initialDistanceX;

                // Clamp scale to prevent extreme values and NaN
                if (float.IsNaN(scaleX) || float.IsInfinity(scaleX))
                    scaleX = 1.0f;
                else
                    scaleX = Math.Max(Math.Min(scaleX, 1000f), -1000f); // Reasonable limits
            }

            if (Math.Abs(_initialDistanceY) > float.Epsilon)
            {
                scaleY = currentDistanceY / _initialDistanceY;

                // Clamp scale to prevent extreme values and NaN
                if (float.IsNaN(scaleY) || float.IsInfinity(scaleY))
                    scaleY = 1.0f;
                else
                    scaleY = Math.Max(Math.Min(scaleY, 1000f), -1000f); // Reasonable limits
            }
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

    #region SHEARING LOGIC
    private void StartShearing(SKPoint transformationPoint, SKPoint startPoint, ShearAxis axis)
    {
        _currentState = TransformationState.Shearing;
        _transformOrigin = transformationPoint;
        _shearAxis = axis;
        _initialShearPoint = startPoint;
        
        // Calculate initial offset from transformation point in world coordinates
        _initialShearOffsetX = startPoint.X - transformationPoint.X;
        _initialShearOffsetY = startPoint.Y - transformationPoint.Y;
        
        Console.WriteLine($"Shearing started on {axis} axis");
    }

    private (float shearX, float shearY) UpdateShearing(SKPoint currentPoint, CsXFL.Matrix? currentMatrix = null)
    {
        float shearX = 0.0f, shearY = 0.0f;
        
        if (currentMatrix == null)
            return (shearX, shearY);

        // Work in world coordinates to avoid cumulative transformation issues
        float currentOffsetX = currentPoint.X - _transformOrigin.X;
        float currentOffsetY = currentPoint.Y - _transformOrigin.Y;
        
        // Calculate shear based on axis and movement difference from initial position
        switch (_shearAxis)
        {
            case ShearAxis.Horizontal:
                // Horizontal shear: X displacement relative to Y distance from transform origin
                if (Math.Abs(_initialShearOffsetY) > float.Epsilon)
                {
                    float deltaX = currentOffsetX - _initialShearOffsetX;
                    shearX = deltaX / Math.Abs(_initialShearOffsetY);
                }
                break;
                
            case ShearAxis.Vertical:
                // Vertical shear: Y displacement relative to X distance from transform origin
                if (Math.Abs(_initialShearOffsetX) > float.Epsilon)
                {
                    float deltaY = currentOffsetY - _initialShearOffsetY;
                    shearY = deltaY / Math.Abs(_initialShearOffsetX);
                }
                break;
        }
        
        // Clamp shear values to reasonable limits
        shearX = Math.Max(Math.Min(shearX, 2f), -2f);  // Reduced limits for more stable shearing
        shearY = Math.Max(Math.Min(shearY, 2f), -2f);
        
        return (shearX, shearY);
    }

    private void ApplyShearing(BlitzElement element, SKPoint transformationPoint, float shearX, float shearY)
    {
        if (element.Picture == null || element.Matrix == null)
            return;

        if (!TryInvertMatrix(element.Matrix, out var inverseMatrix))
            return;

        // Transform the transformation point to local coordinates
        var localTransformPoint = TransformPoint(transformationPoint, inverseMatrix);

        // Create shear matrix around the local transformation point
        // This ensures the transformation point remains fixed during shearing
        var translateToOrigin = SKMatrix.CreateTranslation(-localTransformPoint.X, -localTransformPoint.Y);
        var skewMatrix = SKMatrix.CreateSkew(shearX, shearY);
        var translateBack = SKMatrix.CreateTranslation(localTransformPoint.X, localTransformPoint.Y);

        // Combine the matrices in the correct order: translateBack * skewMatrix * translateToOrigin
        var shearMatrix = translateToOrigin;
        SKMatrix.Concat(ref shearMatrix, skewMatrix, shearMatrix);
        SKMatrix.Concat(ref shearMatrix, translateBack, shearMatrix);

        // Apply shearing to the element's matrix using local coordinates
        element.Matrix = PreConcatMatrix(element.Matrix, shearMatrix);

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

        // Record the sheared picture according to the sheared element matrix
        using var shearedRecorder = new SKPictureRecorder();
        var shearedCanvas = shearedRecorder.BeginRecording(element.Picture.CullRect);

        var shearedMatrix = new SKMatrix
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
        shearedCanvas.SetMatrix(shearedMatrix);
        shearedCanvas.DrawPicture(element.Picture);
        element.Picture = shearedRecorder.EndRecording();
    }

    private ShearAxis DetermineShearAxis(SKPoint mousePosition, List<SKRect> edges)
    {
        // Determine which edge was clicked to set shear axis
        // Top/Bottom edges = Horizontal shear, Left/Right edges = Vertical shear
        for (int i = 0; i < edges.Count; i++)
        {
            if (IsWithinEdge(mousePosition, edges[i], _edgeMargin))
            {
                switch (i)
                {
                    case 0: // Top edge
                    case 1: // Bottom edge
                        return ShearAxis.Horizontal;
                    case 2: // Left edge  
                    case 3: // Right edge
                        return ShearAxis.Vertical;
                }
            }
        }
        return ShearAxis.Horizontal; // Default
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

                if (IsWithinMargin(skMousePosition, transformationPoint, (_transformPointMargin / canvas.Scale)))
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
                        // Check if transformation point invalidates this scaling operation
                        if (IsCornerTransformationPointInvalid(transformationPoint, handleType, canvas.SelectedElement.BBox, canvas.SelectedElement.Matrix))
                        {
                            Console.WriteLine($"Double scaling blocked: Transformation point is on an edge adjacent to corner handle ({handleType})");
                            e.Handled = true;
                            return;
                        }
                        Console.WriteLine("Double Axis Scaling Start.");
                        _activeHandle = handleType;
                        _currentState = TransformationState.DoubleScaling;
                        StartDoubleScale(transformationPoint, handlePosition, handleType, canvas.SelectedElement.Matrix);
                        e.Handled = true;
                        return;
                    }

                    // IF middle handles, DO Single Axis Scaling
                    if (Array.Exists(middleHandles, h => h == handleType) && IsWithinMargin(skMousePosition, handlePosition, (_handleMargin / canvas.Scale)))
                    {
                        // Check if transformation point invalidates this scaling operation
                        if (IsTransformationPointOnEdge(transformationPoint, handleType, canvas.SelectedElement.BBox, canvas.SelectedElement.Matrix))
                        {
                            Console.WriteLine($"Single scaling blocked: Transformation point is on the edge being scaled ({handleType})");
                            e.Handled = true;
                            return;
                        }
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
                    if (IsWithinEdge(skMousePosition, edge, (_edgeMargin / canvas.Scale)))
                    {
                        Console.WriteLine("Shearing Start");
                        _currentState = TransformationState.Shearing;
                        var shearAxis = DetermineShearAxis(skMousePosition, edges.ToList());
                        StartShearing(transformationPoint, skMousePosition, shearAxis);
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

            // Check for snapping to transform handles first
            var snapPoint = GetSnapPoint(skMousePosition, transformHandles, canvas.Scale);
            var targetPoint = snapPoint ?? skMousePosition;

            // Convert target position to local coordinates relative to the element
            if (canvas.SelectedElement.Matrix != null && TryInvertMatrix(canvas.SelectedElement.Matrix, out var inverseMatrix))
            {
                var localPoint = TransformPoint(targetPoint, inverseMatrix);

                // Update the transformation point
                canvas.SelectedElement.TransformationPoint.X = localPoint.X;
                canvas.SelectedElement.TransformationPoint.Y = localPoint.Y;

                Console.WriteLine($"Transform Point moved to: {localPoint.X}, {localPoint.Y}" + (snapPoint.HasValue ? " (SNAPPED)" : ""));

                canvas.UpdateAdorningLayer(canvas.SelectedElement);
                canvas.InvalidateVisual();
                return;
            }
        }

        // IF Double Scaling
        if (_currentState.Equals(TransformationState.DoubleScaling) && canvas.SelectedElement != null)
        {
            var mousePosition = e.GetPosition(canvas);
            var skMousePosition = new SKPoint((float)mousePosition.X, (float)mousePosition.Y);

            // Update scaling factors for both axes uniformly
            var (scaleX, scaleY) = UpdateDoubleScale(transformationPoint, skMousePosition, canvas.SelectedElement.Matrix);

            // Apply scaling relative to the transformation point
            Console.WriteLine($"Double Scaling X: {scaleX}, Y: {scaleY}");
            ApplyScaling(canvas.SelectedElement, transformationPoint, scaleX, scaleY);

            canvas.UpdateAdorningLayer(canvas.SelectedElement);
            canvas.CompositeLayersToRenderTarget();
            canvas.InvalidateVisual();
            return;
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

        // IF Shearing
        if (_currentState.Equals(TransformationState.Shearing) && canvas.SelectedElement != null)
        {
            var mousePosition = e.GetPosition(canvas);
            var skMousePosition = new SKPoint((float)mousePosition.X, (float)mousePosition.Y);

            // Update shearing factors
            var (shearX, shearY) = UpdateShearing(skMousePosition, canvas.SelectedElement.Matrix);

            //float shearX = 0.001F, shearY = 0.001F;

            // Apply shearing relative to the transformation point
            Console.WriteLine($"Shearing X: {shearX}, Y: {shearY}");
            ApplyShearing(canvas.SelectedElement, transformationPoint, shearX, shearY);

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
            if (IsWithinEdge(skMousePosition, edge, (_edgeMargin / canvas.Scale)))
            {
                canvas.Cursor = CustomCursorFactory.CreateCursor(CursorType.Skew);
                return true;
            }
        }

        // Default cursor
        canvas.Cursor = new Cursor(StandardCursorType.Arrow);
        return false;
    }

    private SKPoint? GetSnapPoint(SKPoint mousePosition, List<(SKPoint Center, DrawingCanvas.TransformHandleType Type)> transformHandles, double canvasScale)
    {
        double snapMargin = _transformationPointSnapMargin / canvasScale; // Adjust for zoom level
        SKPoint? closestHandle = null;
        double closestDistance = double.MaxValue;

        // Check distance to each transform handle
        foreach (var (handlePosition, handleType) in transformHandles)
        {
            double distance = Math.Sqrt(
                Math.Pow(mousePosition.X - handlePosition.X, 2) + 
                Math.Pow(mousePosition.Y - handlePosition.Y, 2)
            );

            if (distance <= snapMargin && distance < closestDistance)
            {
                closestDistance = distance;
                closestHandle = handlePosition;
            }
        }

        return closestHandle;
    }

    public IEnumerable<SKRect> GetEdgeRegions(CsXFL.Rectangle boundingBox, CsXFL.Matrix matrix)
    {
        // Transform the corners of the bounding box using the matrix
        var topLeft = TransformPoint(new SKPoint((float)boundingBox.Left, (float)boundingBox.Top), matrix);
        var topRight = TransformPoint(new SKPoint((float)boundingBox.Right, (float)boundingBox.Top), matrix);
        var bottomLeft = TransformPoint(new SKPoint((float)boundingBox.Left, (float)boundingBox.Bottom), matrix);
        var bottomRight = TransformPoint(new SKPoint((float)boundingBox.Right, (float)boundingBox.Bottom), matrix);

        // Calculate edge thickness that scales with zoom level
        float edgeThickness = (float)(_edgeMargin * 2); // Make edges thicker for easier hitting

        // Define edge regions as rectangles with proper thickness along the edges
        var edgeRegions = new List<SKRect>
        {
            // Top edge - horizontal rectangle
            new SKRect(
                Math.Min(topLeft.X, topRight.X),
                Math.Min(topLeft.Y, topRight.Y) - edgeThickness/2,
                Math.Max(topLeft.X, topRight.X),
                Math.Max(topLeft.Y, topRight.Y) + edgeThickness/2
            ),
            
            // Bottom edge - horizontal rectangle  
            new SKRect(
                Math.Min(bottomLeft.X, bottomRight.X),
                Math.Min(bottomLeft.Y, bottomRight.Y) - edgeThickness/2,
                Math.Max(bottomLeft.X, bottomRight.X),
                Math.Max(bottomLeft.Y, bottomRight.Y) + edgeThickness/2
            ),
            
            // Left edge - vertical rectangle
            new SKRect(
                Math.Min(topLeft.X, bottomLeft.X) - edgeThickness/2,
                Math.Min(topLeft.Y, bottomLeft.Y),
                Math.Max(topLeft.X, bottomLeft.X) + edgeThickness/2,
                Math.Max(topLeft.Y, bottomLeft.Y)
            ),
            
            // Right edge - vertical rectangle
            new SKRect(
                Math.Min(topRight.X, bottomRight.X) - edgeThickness/2,
                Math.Min(topRight.Y, bottomRight.Y),
                Math.Max(topRight.X, bottomRight.X) + edgeThickness/2,
                Math.Max(topRight.Y, bottomRight.Y)
            )
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
    #region HANDLE INVALIDTY
    private bool IsTransformationPointOnEdge(SKPoint transformationPoint, DrawingCanvas.TransformHandleType handleType, CsXFL.Rectangle boundingBox, CsXFL.Matrix matrix)
    {
        // Transform bounding box corners to world coordinates
        var topLeft = TransformPoint(new SKPoint((float)boundingBox.Left, (float)boundingBox.Top), matrix);
        var topRight = TransformPoint(new SKPoint((float)boundingBox.Right, (float)boundingBox.Top), matrix);
        var bottomLeft = TransformPoint(new SKPoint((float)boundingBox.Left, (float)boundingBox.Bottom), matrix);
        var bottomRight = TransformPoint(new SKPoint((float)boundingBox.Right, (float)boundingBox.Bottom), matrix);

        double margin = _edgeMargin;

        switch (handleType)
        {
            // Top edge handles
            case DrawingCanvas.TransformHandleType.TopLeft:
            case DrawingCanvas.TransformHandleType.TopCenter:
            case DrawingCanvas.TransformHandleType.TopRight:
                return IsPointOnLineSegment(transformationPoint, topLeft, topRight, margin);

            // Bottom edge handles
            case DrawingCanvas.TransformHandleType.BottomLeft:
            case DrawingCanvas.TransformHandleType.BottomCenter:
            case DrawingCanvas.TransformHandleType.BottomRight:
                return IsPointOnLineSegment(transformationPoint, bottomLeft, bottomRight, margin);

            // Left edge handles
            case DrawingCanvas.TransformHandleType.LeftCenter:
                return IsPointOnLineSegment(transformationPoint, topLeft, bottomLeft, margin);

            // Right edge handles
            case DrawingCanvas.TransformHandleType.RightCenter:
                return IsPointOnLineSegment(transformationPoint, topRight, bottomRight, margin);

            default:
                return false;
        }
    }

    private bool IsCornerTransformationPointInvalid(SKPoint transformationPoint, DrawingCanvas.TransformHandleType handleType, CsXFL.Rectangle boundingBox, CsXFL.Matrix matrix)
    {
        // For corner handles, check if transformation point is on either adjacent edge
        double margin = _edgeMargin;

        // Transform bounding box corners to world coordinates
        var topLeft = TransformPoint(new SKPoint((float)boundingBox.Left, (float)boundingBox.Top), matrix);
        var topRight = TransformPoint(new SKPoint((float)boundingBox.Right, (float)boundingBox.Top), matrix);
        var bottomLeft = TransformPoint(new SKPoint((float)boundingBox.Left, (float)boundingBox.Bottom), matrix);
        var bottomRight = TransformPoint(new SKPoint((float)boundingBox.Right, (float)boundingBox.Bottom), matrix);

        switch (handleType)
        {
            case DrawingCanvas.TransformHandleType.TopLeft:
                // Check if on top edge OR left edge
                return IsPointOnLineSegment(transformationPoint, topLeft, topRight, margin) ||
                    IsPointOnLineSegment(transformationPoint, topLeft, bottomLeft, margin);

            case DrawingCanvas.TransformHandleType.TopRight:
                // Check if on top edge OR right edge
                return IsPointOnLineSegment(transformationPoint, topLeft, topRight, margin) ||
                    IsPointOnLineSegment(transformationPoint, topRight, bottomRight, margin);

            case DrawingCanvas.TransformHandleType.BottomLeft:
                // Check if on bottom edge OR left edge
                return IsPointOnLineSegment(transformationPoint, bottomLeft, bottomRight, margin) ||
                    IsPointOnLineSegment(transformationPoint, topLeft, bottomLeft, margin);

            case DrawingCanvas.TransformHandleType.BottomRight:
                // Check if on bottom edge OR right edge
                return IsPointOnLineSegment(transformationPoint, bottomLeft, bottomRight, margin) ||
                    IsPointOnLineSegment(transformationPoint, topRight, bottomRight, margin);

            default:
                return false;
        }
    }

    private bool IsPointOnLineSegment(SKPoint point, SKPoint lineStart, SKPoint lineEnd, double margin)
    {
        // Calculate the distance from point to the line segment
        double A = point.X - lineStart.X;
        double B = point.Y - lineStart.Y;
        double C = lineEnd.X - lineStart.X;
        double D = lineEnd.Y - lineStart.Y;

        double dot = A * C + B * D;
        double lenSq = C * C + D * D;
        
        if (lenSq < double.Epsilon) // Line segment is actually a point
            return Math.Sqrt(A * A + B * B) <= margin;

        double param = dot / lenSq;

        double xx, yy;
        if (param < 0)
        {
            xx = lineStart.X;
            yy = lineStart.Y;
        }
        else if (param > 1)
        {
            xx = lineEnd.X;
            yy = lineEnd.Y;
        }
        else
        {
            xx = lineStart.X + param * C;
            yy = lineStart.Y + param * D;
        }

        double dx = point.X - xx;
        double dy = point.Y - yy;
        return Math.Sqrt(dx * dx + dy * dy) <= margin;
    }
    #endregion
}