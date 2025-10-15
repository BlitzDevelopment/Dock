using System;
using SkiaSharp;
using System.Collections.Generic;
using Avalonia.Input;
using System.Linq;
using Serilog;

namespace Avalonia.Controls;

/// <summary>
/// Configuration settings shared between TransformationTool and DrawingCanvas.Adorner
/// for consistent transformation behavior and visual feedback.
/// </summary>
public class TransformationToolConfig
{
    public SKColor TransformAdornerInteriorColor { get; set; } = SKColors.Black;
    public SKColor TransformAdornerBorderColor { get; set; } = SKColors.White;

    public float TransformAdornerStrokeWidth { get; set; } = 2f;
    public float TransformAdornerHandleSize { get; set; } = 10f;
    public float TransformationPointRadius { get; set; } = 5f;

    public float TransformHandleMargin { get; set; } = 10f;
    public float TransformEdgeMargin { get; set; } = 5f;
    public float TransformPointMargin { get; set; } = 8f;
    public float TransformPointSnapMargin { get; set; } = 15f;
    public float RotationHandleMultiplierFactor { get; set; } = 3f;

    // Rotation modifiers
    public KeyModifiers RotationSnapModifier { get; set; } = KeyModifiers.Shift;
    public KeyModifiers RotationOppositeCornerModifier { get; set; } = KeyModifiers.Alt;
    
    // Middle scaling modifiers  
    public KeyModifiers MiddleScaleAdjacentEdgesModifier { get; set; } = KeyModifiers.Alt;
    
    // Corner scaling modifiers
    public KeyModifiers CornerScaleUniformModifier { get; set; } = KeyModifiers.Shift;
    public KeyModifiers CornerScaleOppositeCornerModifier { get; set; } = KeyModifiers.Alt;
    public KeyModifiers CornerPinModifier { get; set; } = KeyModifiers.Control | KeyModifiers.Shift;
    
    // Shearing modifiers
    public KeyModifiers ShearSelectedAxisModifier { get; set; } = KeyModifiers.Alt;

    public static TransformationToolConfig Default => new();
}

public class TransformationTool : IDrawingCanvasTool
{
    private SelectionTool? _selectionTool;
    public SelectionTool? SelectionTool 
    { 
        get => _selectionTool; 
        set => _selectionTool = value; 
    }

    // Transform state
    private SKPoint _transformOrigin;
    private ShearAxis _shearAxis;
    private DrawingCanvas.TransformHandleType _activeHandle;
    private TransformationState _currentState = TransformationState.None;

    // Values for margins and snapping
    public TransformationToolConfig Config { get; set; } = TransformationToolConfig.Default;
    private const float _matrixClampEpsilon = 0.001f; // Minimum RELATIVE size that a matrix is allowed to be scaled
    private float _initialDistance, _initialAngle, _initialDistanceX, _initialDistanceY,
                _initialShearOffsetX, _initialShearOffsetY;
    private bool _uniformDoubleScaling = false;

    // Enums
    private enum TransformationState { None, DoubleScaling, SingleScaling, Rotating, Shearing, MovingTransformSKPoint, Selection }
    private enum ShearAxis { Horizontal, Vertical }

    // Handle collections
    private static readonly DrawingCanvas.TransformHandleType[] cornerHandles =
        { DrawingCanvas.TransformHandleType.BottomLeft, DrawingCanvas.TransformHandleType.TopLeft,
        DrawingCanvas.TransformHandleType.BottomRight, DrawingCanvas.TransformHandleType.TopRight };

    private static readonly DrawingCanvas.TransformHandleType[] middleHandles =
        { DrawingCanvas.TransformHandleType.BottomCenter, DrawingCanvas.TransformHandleType.TopCenter,
        DrawingCanvas.TransformHandleType.RightCenter, DrawingCanvas.TransformHandleType.LeftCenter };

    #region SCALING LOGIC
    /// <summary>
    /// Applies scaling transformation to a BlitzElement around a specified transformation point. Updates the element's transformation matrix by applying scaling around the local transformation point, then regenerates the element's Picture by first resetting it with the inverse matrix, then applying the scaled matrix. The transformation point is converted to local coordinates before creating the scale matrix to ensure the scaling occurs around the correct pivot point regardless of the element's current transformation.
    /// </summary>
    /// <param name="element">The BlitzElement to scale. Must have a valid Picture and Matrix.</param>
    /// <param name="transformationPoint">The point around which scaling is applied in world coordinates.</param>
    /// <param name="horizontalScale">The scaling factor for the horizontal axis.</param>
    /// <param name="verticalScale">The scaling factor for the vertical axis.</param>
    private void ApplyScaling(BlitzElement element, SKPoint transformationPoint, float horizontalScale, float verticalScale)
    {
        if (element.Picture == null || element.Matrix == null || !TryInvertMatrix(element.Matrix, out var inverseMatrix))
            return;

        var localPivot = TransformSKPoint(transformationPoint, inverseMatrix);
        var localScaleMatrix = SKMatrix.CreateScale(horizontalScale, verticalScale, localPivot.X, localPivot.Y);

        element.Matrix = ClampMatrix(PreConcatMatrix(element.Matrix, localScaleMatrix));

        // Helper method to create SKMatrix from CsXFL.Matrix
        SKMatrix CreateSKMatrix(CsXFL.Matrix m) => new()
        {
            ScaleX = (float)m.A,
            SkewY = (float)m.B,
            SkewX = (float)m.C,
            ScaleY = (float)m.D,
            TransX = (float)m.Tx,
            TransY = (float)m.Ty,
            Persp0 = 0,
            Persp1 = 0,
            Persp2 = 1
        };

        // Reset picture with inverse matrix
        using (var recorder = new SKPictureRecorder())
        {
            var canvas = recorder.BeginRecording(element.Picture.CullRect);
            canvas.SetMatrix(CreateSKMatrix(inverseMatrix));
            canvas.DrawPicture(element.Picture);
            element.Picture = recorder.EndRecording();
        }

        // Apply scaled matrix to picture  
        using (var scaledRecorder = new SKPictureRecorder())
        {
            var scaledCanvas = scaledRecorder.BeginRecording(element.Picture.CullRect);
            scaledCanvas.SetMatrix(CreateSKMatrix(element.Matrix));
            scaledCanvas.DrawPicture(element.Picture);
            element.Picture = scaledRecorder.EndRecording();
        }
    }

    /// <summary>
    /// Initializes single-axis scaling transformation by calculating the initial distance from the transformation point to the handle position in local coordinates.
    /// </summary>
    /// <param name="transformationPoint">The fixed point around which scaling occurs in world coordinates.</param>
    /// <param name="handlePosition">The position of the transform handle being dragged in world coordinates.</param>
    /// <param name="handleType">The type of handle that determines the scaling axis (horizontal for left/right center handles, vertical for top/bottom center handles).</param>
    /// <param name="currentMatrix">The current transformation matrix of the element. If null or non-invertible, the operation is aborted.</param>
    private void StartSingleScale(SKPoint transformationPoint, SKPoint handlePosition, DrawingCanvas.TransformHandleType handleType, CsXFL.Matrix? currentMatrix = null)
    {
        _currentState = TransformationState.SingleScaling;

        if (currentMatrix == null || !TryInvertMatrix(currentMatrix, out var inverseMatrix))
            return;

        var localHandlePos = TransformSKPoint(handlePosition, inverseMatrix);
        var localTransformSKPoint = TransformSKPoint(transformationPoint, inverseMatrix);

        // Calculate initial signed distance based on handle type
        bool isHorizontal = handleType == DrawingCanvas.TransformHandleType.RightCenter || handleType == DrawingCanvas.TransformHandleType.LeftCenter;
        _initialDistance = isHorizontal ? localHandlePos.X - localTransformSKPoint.X : localHandlePos.Y - localTransformSKPoint.Y;

        // Ensure minimum distance to prevent division by zero
        if (Math.Abs(_initialDistance) < float.Epsilon)
            _initialDistance = _initialDistance >= 0 ? float.Epsilon : -float.Epsilon;
    }

    /// <summary>
    /// Calculates the current scaling factors for single-axis scaling based on mouse movement from the initial handle position.
    /// </summary>
    /// <param name="transformationPoint">The fixed point around which scaling occurs in world coordinates.</param>
    /// <param name="currentMousePosition">The current mouse position in world coordinates.</param>
    /// <param name="currentMatrix">The current transformation matrix of the element. If null or non-invertible, returns default scale values.</param>
    /// <returns>A tuple containing (scaleX, scaleY) where one value represents the calculated scale factor and the other is 1.0f based on the active handle type.</returns>
    private (float scaleX, float scaleY) UpdateSingleScale(SKPoint transformationPoint, SKPoint currentMousePosition, CsXFL.Matrix? currentMatrix = null)
    {
        if (currentMatrix == null || !TryInvertMatrix(currentMatrix, out var inverseMatrix))
            return (1.0f, 1.0f);

        var localMousePos = TransformSKPoint(currentMousePosition, inverseMatrix);
        var localTransformSKPoint = TransformSKPoint(transformationPoint, inverseMatrix);

        float scale = 1.0f;
        bool isHorizontal = _activeHandle == DrawingCanvas.TransformHandleType.RightCenter || _activeHandle == DrawingCanvas.TransformHandleType.LeftCenter;

        if (Math.Abs(_initialDistance) > float.Epsilon)
        {
            float currentDistance = isHorizontal ? localMousePos.X - localTransformSKPoint.X : localMousePos.Y - localTransformSKPoint.Y;
            scale = currentDistance / _initialDistance;

            // Clamp scale to prevent extreme values and NaN
            scale = float.IsNaN(scale) || float.IsInfinity(scale) ? 1.0f : Math.Max(Math.Min(scale, 1000f), -1000f);
        }

        return isHorizontal ? (scale, 1.0f) : (1.0f, scale);
    }

    /// <summary>
    /// Initializes dual-axis scaling transformation by calculating initial distances from the transformation point to the handle position in local coordinates.
    /// </summary>
    /// <param name="transformationPoint">The fixed point around which scaling occurs in world coordinates.</param>
    /// <param name="handlePosition">The position of the corner transform handle being dragged in world coordinates.</param>
    /// <param name="handleType">The type of corner handle that determines the scaling behavior.</param>
    /// <param name="currentMatrix">The current transformation matrix of the element. If null or non-invertible, the operation is aborted.</param>
    /// <remarks>
    /// For uniform scaling, calculates the euclidean distance to the handle. For non-uniform scaling, calculates separate X and Y distances.
    /// Ensures minimum distance values to prevent division by zero in subsequent scaling calculations.
    /// </remarks>
    private void StartDoubleScale(SKPoint transformationPoint, SKPoint handlePosition, DrawingCanvas.TransformHandleType handleType, CsXFL.Matrix? currentMatrix = null)
    {
        _currentState = TransformationState.DoubleScaling;

        if (currentMatrix == null || !TryInvertMatrix(currentMatrix, out var inverseMatrix))
            return;

        var localHandlePos = TransformSKPoint(handlePosition, inverseMatrix);
        var localTransformSKPoint = TransformSKPoint(transformationPoint, inverseMatrix);

        if (_uniformDoubleScaling)
        {
            float deltaX = localHandlePos.X - localTransformSKPoint.X;
            float deltaY = localHandlePos.Y - localTransformSKPoint.Y;
            _initialDistance = (float)Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
            if (Math.Abs(_initialDistance) < float.Epsilon)
                _initialDistance = float.Epsilon;
        }
        else
        {
            _initialDistanceX = localHandlePos.X - localTransformSKPoint.X;
            _initialDistanceY = localHandlePos.Y - localTransformSKPoint.Y;

            if (Math.Abs(_initialDistanceX) < float.Epsilon)
                _initialDistanceX = _initialDistanceX >= 0 ? float.Epsilon : -float.Epsilon;
            if (Math.Abs(_initialDistanceY) < float.Epsilon)
                _initialDistanceY = _initialDistanceY >= 0 ? float.Epsilon : -float.Epsilon;
        }
    }

    /// <summary>
    /// Calculates the current scaling factors for dual-axis scaling based on mouse movement from the initial handle position.
    /// </summary>
    /// <param name="transformationPoint">The fixed point around which scaling occurs in world coordinates.</param>
    /// <param name="currentMousePosition">The current mouse position in world coordinates.</param>
    /// <param name="currentMatrix">The current transformation matrix of the element. If null or non-invertible, returns default scale values.</param>
    /// <returns>A tuple containing (scaleX, scaleY) where both values represent calculated scale factors. For uniform scaling, both values are identical. For non-uniform scaling, each axis is calculated independently.</returns>
    private (float scaleX, float scaleY) UpdateDoubleScale(SKPoint transformationPoint, SKPoint currentMousePosition, CsXFL.Matrix? currentMatrix = null)
    {
        if (currentMatrix == null || !TryInvertMatrix(currentMatrix, out var inverseMatrix))
            return (1.0f, 1.0f);

        var localMousePos = TransformSKPoint(currentMousePosition, inverseMatrix);
        var localTransformSKPoint = TransformSKPoint(transformationPoint, inverseMatrix);

        float ClampScale(float scale) => float.IsNaN(scale) || float.IsInfinity(scale) ? 1.0f : Math.Max(Math.Min(scale, 1000f), -1000f);

        if (_uniformDoubleScaling)
        {
            if (Math.Abs(_initialDistance) <= float.Epsilon)
                return (1.0f, 1.0f);

            float deltaX = localMousePos.X - localTransformSKPoint.X;
            float deltaY = localMousePos.Y - localTransformSKPoint.Y;
            float currentDistance = (float)Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
            float uniformScale = ClampScale(currentDistance / _initialDistance);

            return (uniformScale, uniformScale);
        }
        else
        {
            float scaleX = Math.Abs(_initialDistanceX) > float.Epsilon ?
                ClampScale((localMousePos.X - localTransformSKPoint.X) / _initialDistanceX) : 1.0f;
            float scaleY = Math.Abs(_initialDistanceY) > float.Epsilon ?
                ClampScale((localMousePos.Y - localTransformSKPoint.Y) / _initialDistanceY) : 1.0f;

            return (scaleX, scaleY);
        }
    }
    #endregion

    #region ROTATION LOGIC
    // Initialize rotation by calculating the initial angle from the transformation origin to the start point
    private void StartRotation(SKPoint transformOrigin, SKPoint startPoint)
    {
        _transformOrigin = transformOrigin;
        _initialAngle = CalculateAngle(transformOrigin, startPoint);
    }

    // Calculate the change in angle based on current mouse position
    private float UpdateRotation(SKPoint currentPoint)
    {
        float currentAngle = CalculateAngle(_transformOrigin, currentPoint);
        float deltaAngle = currentAngle - _initialAngle;
        _initialAngle = currentAngle;
        return deltaAngle;
    }

    // Calculate angle in degrees from origin to point
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

    /// <summary>
    /// Applies rotation transformation to a BlitzElement around a specified transformation point by updating both the element's transformation matrix and Picture.
    /// </summary>
    /// <param name="element">The BlitzElement to rotate. Must have valid Picture and Matrix properties.</param>
    /// <param name="transformationPoint">The fixed point around which rotation occurs in world coordinates.</param>
    /// <param name="angle">The rotation angle in degrees. Positive values rotate clockwise.</param>
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

    // Create a rotation matrix around a specific point
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
                    // Invert shear for top edge (negative Y offset)
                    float shearMultiplier = _initialShearOffsetY < 0 ? -1.0f : 1.0f;
                    shearX = (deltaX / Math.Abs(_initialShearOffsetY)) * shearMultiplier;

                    // Update the initial offset to prevent accumulation
                    _initialShearOffsetX = currentOffsetX;
                }
                break;

            case ShearAxis.Vertical:
                // Vertical shear: Y displacement relative to X distance from transform origin
                if (Math.Abs(_initialShearOffsetX) > float.Epsilon)
                {
                    float deltaY = currentOffsetY - _initialShearOffsetY;
                    // Invert shear for left edge (negative X offset)
                    float shearMultiplier = _initialShearOffsetX < 0 ? -1.0f : 1.0f;
                    shearY = (deltaY / Math.Abs(_initialShearOffsetX)) * shearMultiplier;

                    // Update the initial offset to prevent accumulation
                    _initialShearOffsetY = currentOffsetY;
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
        var localTransformSKPoint = TransformSKPoint(transformationPoint, inverseMatrix);

        // Create shear matrix around the local transformation point
        // This ensures the transformation point remains fixed during shearing
        var translateToOrigin = SKMatrix.CreateTranslation(-localTransformSKPoint.X, -localTransformSKPoint.Y);
        var skewMatrix = SKMatrix.CreateSkew(shearX, shearY);
        var translateBack = SKMatrix.CreateTranslation(localTransformSKPoint.X, localTransformSKPoint.Y);

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
            if (IsWithinEdge(mousePosition, edges[i], Config.TransformEdgeMargin))
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

    // MARK: Pressed
    public void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not DrawingCanvas canvas || e.Handled || !e.GetCurrentPoint(canvas).Properties.IsLeftButtonPressed)
            return;

        var mousePosition = e.GetPosition(canvas);
        var skMousePosition = new SKPoint((float)mousePosition.X, (float)mousePosition.Y);

        if (canvas.SelectedElement != null)
        {
            var transformHandles = canvas.GetTransformHandles(canvas.SelectedElement.BBox, canvas.SelectedElement.Matrix);
            var transformationPoint = canvas.CalculateFixedTransformationPoint(canvas.SelectedElement.Matrix, canvas.SelectedElement.TransformationPoint);

            // Handle transformation point interaction
            if (IsWithinMargin(skMousePosition, transformationPoint, (Config.TransformPointMargin / canvas.Scale)))
            {
                if (e.ClickCount == 2)
                {
                    CenterTransformationPoint(canvas.SelectedElement);
                    canvas.UpdateAdorningLayer(canvas.SelectedElement);
                    canvas.InvalidateVisual();
                }
                else
                {
                    _currentState = TransformationState.MovingTransformSKPoint;
                }
                e.Handled = true;
                return;
            }

            // Handle transform handles
            foreach (var (handlePosition, handleType) in transformHandles)
            {
                bool isCorner = Array.Exists(cornerHandles, h => h == handleType);
                bool isMiddle = Array.Exists(middleHandles, h => h == handleType);
                bool withinHandle = IsWithinMargin(skMousePosition, handlePosition, (Config.TransformHandleMargin / canvas.Scale));
                bool withinRotation = isCorner && IsWithinMargin(skMousePosition, handlePosition, (Config.TransformHandleMargin / canvas.Scale) * Config.RotationHandleMultiplierFactor);

                // Corner handles for scaling
                if (isCorner && withinHandle && !IsCornerTransformationPointInvalid(transformationPoint, handleType, canvas.SelectedElement.BBox, canvas.SelectedElement.Matrix))
                {
                    _activeHandle = handleType;
                    _currentState = TransformationState.DoubleScaling;
                    StartDoubleScale(transformationPoint, handlePosition, handleType, canvas.SelectedElement.Matrix);
                    e.Handled = true;
                    return;
                }

                // Middle handles for single-axis scaling
                if (isMiddle && withinHandle && !IsTransformationPointOnEdge(transformationPoint, handleType, canvas.SelectedElement.BBox, canvas.SelectedElement.Matrix))
                {
                    _activeHandle = handleType;
                    _currentState = TransformationState.SingleScaling;
                    StartSingleScale(transformationPoint, handlePosition, handleType, canvas.SelectedElement.Matrix);
                    e.Handled = true;
                    return;
                }

                // Corner handles for rotation (wider margin)
                if (withinRotation)
                {
                    _activeHandle = handleType;
                    _currentState = TransformationState.Rotating;
                    StartRotation(transformationPoint, skMousePosition);
                    e.Handled = true;
                    return;
                }

                // Handle blocked operations with single console output
                if ((isCorner && withinHandle && IsCornerTransformationPointInvalid(transformationPoint, handleType, canvas.SelectedElement.BBox, canvas.SelectedElement.Matrix)) ||
                    (isMiddle && withinHandle && IsTransformationPointOnEdge(transformationPoint, handleType, canvas.SelectedElement.BBox, canvas.SelectedElement.Matrix)))
                {
                    Console.WriteLine($"Scaling blocked: Transformation point invalidates {handleType} operation");
                    e.Handled = true;
                    return;
                }
            }

            // Handle edge regions for shearing
            var edges = GetEdgeRegions(canvas.SelectedElement.BBox, canvas.SelectedElement.Matrix);
            foreach (var edge in edges.Where(edge => IsWithinEdge(skMousePosition, edge, (Config.TransformEdgeMargin / canvas.Scale))))
            {
                var shearAxis = DetermineShearAxis(skMousePosition, edges.ToList());

                // Check if transformation point invalidates shearing operation for the specific edge
                if (IsShearingInvalid(transformationPoint, skMousePosition, canvas.SelectedElement.BBox, canvas.SelectedElement.Matrix))
                {
                    Console.WriteLine($"Shearing blocked: Transformation point is on the target edge");
                    e.Handled = true;
                    return;
                }

                _currentState = TransformationState.Shearing;
                StartShearing(transformationPoint, skMousePosition, shearAxis);
                e.Handled = true;
                return;
            }

            // NEW: If we clicked on the selected element but not on any handles/edges,
            // enter selection state and delegate to SelectionTool for dragging
            var hitElement = canvas.HitTest(mousePosition);
            if (hitElement == canvas.SelectedElement && _selectionTool != null)
            {
                _currentState = TransformationState.Selection;
                _selectionTool.OnPointerPressed(sender, e);
                return; // Don't set e.Handled here, let SelectionTool handle it
            }

        }
        
        // If no element is selected or we clicked outside the selected element, perform hit test
        canvas.SelectedElement = canvas.HitTest(mousePosition);
        bool hasSelection = canvas.SelectedElement != null;

        canvas.AdorningLayer.Visible = hasSelection;
        if (hasSelection)
        {
            canvas.UpdateAdorningLayer(canvas.SelectedElement);
            canvas.InvalidateVisual();
            e.Handled = true;
        }
        else
        {
            canvas.AdorningLayer.Visible = false;
            canvas.AdorningLayer.Elements.Clear();
            canvas.InvalidateVisual();
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

        var mousePosition = e.GetPosition(canvas);
        var skMousePosition = new SKPoint((float)mousePosition.X, (float)mousePosition.Y);

        // Delegate to SelectionTool if in selection state
        if (_currentState == TransformationState.Selection && _selectionTool != null)
        {
            _selectionTool.OnPointerMoved(sender, e);
            // Update adorning layer after SelectionTool moves the element
            if (canvas.SelectedElement != null)
            {
                canvas.UpdateAdorningLayer(canvas.SelectedElement);
                canvas.InvalidateVisual();
            }
            return;
        }

        // Handle transformation states
        switch (_currentState)
        {
            case TransformationState.MovingTransformSKPoint when canvas.SelectedElement != null:
                var snapPoint = GetSnapPoint(skMousePosition, transformHandles, canvas.Scale);
                var targetPoint = snapPoint ?? skMousePosition;

                if (canvas.SelectedElement.Matrix != null && TryInvertMatrix(canvas.SelectedElement.Matrix, out var inverseMatrix))
                {
                    var localPoint = TransformSKPoint(targetPoint, inverseMatrix);
                    canvas.SelectedElement.TransformationPoint.X = localPoint.X;
                    canvas.SelectedElement.TransformationPoint.Y = localPoint.Y;
                    canvas.UpdateAdorningLayer(canvas.SelectedElement);
                    canvas.InvalidateVisual();
                }
                return;

            case TransformationState.DoubleScaling when canvas.SelectedElement != null:
                var (doubleScaleX, doubleScaleY) = UpdateDoubleScale(transformationPoint, skMousePosition, canvas.SelectedElement.Matrix);
                ApplyScaling(canvas.SelectedElement, transformationPoint, doubleScaleX, doubleScaleY);
                break;

            case TransformationState.SingleScaling when canvas.SelectedElement != null:
                var (singleScaleX, singleScaleY) = UpdateSingleScale(transformationPoint, skMousePosition, canvas.SelectedElement.Matrix);
                ApplyScaling(canvas.SelectedElement, transformationPoint, singleScaleX, singleScaleY);
                break;

            case TransformationState.Rotating when canvas.SelectedElement != null:
                float deltaRotation = UpdateRotation(skMousePosition);
                ApplyRotation(canvas.SelectedElement, transformationPoint, deltaRotation);
                break;

            case TransformationState.Shearing when canvas.SelectedElement != null:
                var (shearX, shearY) = UpdateShearing(skMousePosition, canvas.SelectedElement.Matrix);
                Console.WriteLine($"Shearing X: {shearX}, Y: {shearY}");
                ApplyShearing(canvas.SelectedElement, transformationPoint, shearX, shearY);
                break;

            default:
                // Handle cursors or set default cursor
                if (canvas.SelectedElement != null)
                {
                    TryHandleCursorAndRegions(canvas, skMousePosition, transformHandles);
                }
                else
                {
                    canvas.Cursor = new Cursor(StandardCursorType.Arrow);
                }
                return;
        }

        // Common updates for transformation states (except MovingTransformSKPoint)
        if (canvas.SelectedElement != null)
        {
            canvas.UpdateAdorningLayer(canvas.SelectedElement);
            canvas.CompositeLayersToRenderTarget();
            canvas.InvalidateVisual();
        }
    }

    // MARK: Released
    public void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is not DrawingCanvas canvas)
            return;

        // NEW: Handle selection state passthrough
        if (_currentState == TransformationState.Selection && _selectionTool != null)
        {
            _selectionTool.OnPointerReleased(sender, e);
            _currentState = TransformationState.None;
            return;
        }

        _currentState = TransformationState.None;

        if (canvas.SelectedElement != null && canvas.SelectedElement.Model != null)
        {
            // Update the model's matrix translation
            canvas.SelectedElement.Model.Matrix = canvas.SelectedElement.Matrix;
        }
    }

    #region HELPERS
    /// <summary>
    /// Handles cursor updates and region detection for transformation operations based on mouse position relative to transform handles and element edges.
    /// </summary>
    /// <param name="canvas">The drawing canvas containing the selected element and cursor context.</param>
    /// <param name="skMousePosition">The current mouse position in canvas coordinates.</param>
    /// <param name="transformHandles">Collection of transform handle positions and types for hit testing.</param>
    /// <returns>True if a cursor was set based on the mouse position; otherwise, false.</returns>
    private bool TryHandleCursorAndRegions(DrawingCanvas canvas, SKPoint skMousePosition, List<(SKPoint Center, DrawingCanvas.TransformHandleType Type)> transformHandles)
    {
        var transformationPoint = canvas.CalculateFixedTransformationPoint(canvas.SelectedElement.Matrix, canvas.SelectedElement.TransformationPoint);

        // Handle transformation point region
        if (IsWithinMargin(skMousePosition, transformationPoint, (Config.TransformPointMargin / canvas.Scale)))
        {
            canvas.Cursor = new Cursor(StandardCursorType.SizeAll);
            return true;
        }

        // Handle transform handles (corners and middle handles)
        foreach (var (handlePosition, handleType) in transformHandles)
        {
            bool isCorner = Array.Exists(cornerHandles, h => h == handleType);
            bool isMiddle = Array.Exists(middleHandles, h => h == handleType);
            bool withinHandle = IsWithinMargin(skMousePosition, handlePosition, (Config.TransformHandleMargin / canvas.Scale));
            bool withinRotation = isCorner && IsWithinMargin(skMousePosition, handlePosition, (Config.TransformHandleMargin / canvas.Scale) * Config.RotationHandleMultiplierFactor);

            // Check for scaling regions (corner handles) - only if valid
            if (isCorner && withinHandle && !IsCornerTransformationPointInvalid(transformationPoint, handleType, canvas.SelectedElement.BBox, canvas.SelectedElement.Matrix))
            {
                canvas.Cursor = new Cursor(StandardCursorType.SizeAll);
                return true;
            }

            // Check for scaling regions (middle handles) - only if valid
            if (isMiddle && withinHandle && !IsTransformationPointOnEdge(transformationPoint, handleType, canvas.SelectedElement.BBox, canvas.SelectedElement.Matrix))
            {
                canvas.Cursor = new Cursor(StandardCursorType.SizeNorthSouth);
                return true;
            }

            // Check for rotation region - only show if scaling is not available or invalid
            if (withinRotation)
            {
                canvas.Cursor = CustomCursorFactory.CreateCursor(CursorType.Rotate);
                return true;
            }
        }

        // Handle edge regions for shearing - only if valid
        var edges = GetEdgeRegions(canvas.SelectedElement.BBox, canvas.SelectedElement.Matrix);
        foreach (var edge in edges)
        {
            if (IsWithinEdge(skMousePosition, edge, (Config.TransformEdgeMargin / canvas.Scale)))
            {
                // Only show shear cursor if shearing is not invalid for this specific edge
                if (!IsShearingInvalid(transformationPoint, skMousePosition, canvas.SelectedElement.BBox, canvas.SelectedElement.Matrix))
                {
                    canvas.Cursor = CustomCursorFactory.CreateCursor(CursorType.Skew);
                    return true;
                }
            }
        }

        // Default cursor
        canvas.Cursor = new Cursor(StandardCursorType.Arrow);
        return false;
    }

    /// <summary>
    /// Finds the closest transform handle within snapping distance of the mouse position for transformation point snapping.
    /// </summary>
    /// <param name="mousePosition">The current mouse position in canvas coordinates.</param>
    /// <param name="transformHandles">Collection of transform handle positions and types to check for snapping.</param>
    /// <param name="canvasScale">The current canvas scale factor used to adjust snap margin for zoom level.</param>
    /// <returns>The position of the closest transform handle within snap distance, or null if no handle is close enough.</returns>
    private SKPoint? GetSnapPoint(SKPoint mousePosition, List<(SKPoint Center, DrawingCanvas.TransformHandleType Type)> transformHandles, double canvasScale)
    {
        double snapMargin = Config.TransformPointSnapMargin / canvasScale; // Adjust for zoom level
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

    /// <summary>
    /// Generates edge regions as rectangles around the transformed bounding box edges for hit testing during shearing operations.
    /// </summary>
    /// <param name="boundingBox">The element's bounding box in local coordinates.</param>
    /// <param name="matrix">The transformation matrix used to convert bounding box points to world coordinates.</param>
    /// <returns>An enumerable collection of SKRect objects representing the top, bottom, left, and right edge regions with appropriate thickness for interaction.</returns>
    public IEnumerable<SKRect> GetEdgeRegions(CsXFL.Rectangle boundingBox, CsXFL.Matrix matrix)
    {
        // Transform the corners of the bounding box using the matrix
        var topLeft = TransformSKPoint(new SKPoint((float)boundingBox.Left, (float)boundingBox.Top), matrix);
        var topRight = TransformSKPoint(new SKPoint((float)boundingBox.Right, (float)boundingBox.Top), matrix);
        var bottomLeft = TransformSKPoint(new SKPoint((float)boundingBox.Left, (float)boundingBox.Bottom), matrix);
        var bottomRight = TransformSKPoint(new SKPoint((float)boundingBox.Right, (float)boundingBox.Bottom), matrix);

        // Calculate edge thickness that scales with zoom level
        float edgeThickness = (float)(Config.TransformEdgeMargin * 2); // Make edges thicker for easier hitting

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

    // Invert a CsXFL.Matrix
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

    // Pre-concatenate a CsXFL.Matrix with an SKMatrix
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

    // Apply a transformation matrix an SKPoint
    private SKPoint TransformSKPoint(SKPoint point, CsXFL.Matrix matrix)
    {
        return new SKPoint(
            (float)(matrix.A * point.X + matrix.C * point.Y + matrix.Tx),
            (float)(matrix.B * point.X + matrix.D * point.Y + matrix.Ty)
        );
    }

    // Check if a point is within a certain margin of another point
    private bool IsWithinMargin(SKPoint mousePosition, SKPoint handlePosition, double margin)
    {
        return Math.Abs(mousePosition.X - handlePosition.X) <= margin &&
               Math.Abs(mousePosition.Y - handlePosition.Y) <= margin;
    }

    // Check if a point is on a line segment within a certain margin
    private bool IsWithinEdge(SKPoint mousePosition, SKRect edge, double margin)
    {
        return mousePosition.X >= edge.Left - margin && mousePosition.X <= edge.Right + margin &&
            mousePosition.Y >= edge.Top - margin && mousePosition.Y <= edge.Bottom + margin;
    }

    // Center the transformation point of an element to the center of its bounding box
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

    /// <summary>
    /// Clamps the transformation matrix to ensure that its scale values do not fall below a minimum threshold.
    /// </summary>
    /// <param name="matrix">The transformation matrix to be clamped.</param>
    /// <returns>
    /// A <see cref="CsXFL.Matrix"/> object with adjusted scale values if they were below the minimum threshold.
    /// </returns>
    /// <remarks>
    /// The method calculates the scale values along the X and Y axes using the determinant of the matrix.
    /// If the scale values are smaller than the specified minimum scale value, the matrix components are adjusted
    /// proportionally to meet the minimum scale requirement.
    /// </remarks>
    private CsXFL.Matrix ClampMatrix(CsXFL.Matrix matrix)
    {
        // Calculate the actual scale from the matrix determinant
        double scaleX = Math.Sqrt(matrix.A * matrix.A + matrix.B * matrix.B);
        double scaleY = Math.Sqrt(matrix.C * matrix.C + matrix.D * matrix.D);

        // Only clamp if the actual scale is too small
        if (scaleX < _matrixClampEpsilon)
        {
            double factor = _matrixClampEpsilon / scaleX;
            matrix.A *= factor;
            matrix.B *= factor;
        }

        if (scaleY < _matrixClampEpsilon)
        {
            double factor = _matrixClampEpsilon / scaleY;
            matrix.C *= factor;
            matrix.D *= factor;
        }

        return matrix;
    }
    #endregion

    #region HANDLE INVALIDTY
    /// <summary>
    /// Determines if a transformation point lies on a specific edge of the element's bounding box based on the handle type.
    /// </summary>
    /// <param name="transformationPoint">The transformation point to test in world coordinates.</param>
    /// <param name="handleType">The handle type that determines which edge to check against.</param>
    /// <param name="boundingBox">The element's bounding box in local coordinates.</param>
    /// <param name="matrix">The transformation matrix used to convert bounding box points to world coordinates.</param>
    /// <returns>True if the transformation point lies on the edge corresponding to the handle type; otherwise, false.</returns>
    private bool IsTransformationPointOnEdge(SKPoint transformationPoint, DrawingCanvas.TransformHandleType handleType, CsXFL.Rectangle boundingBox, CsXFL.Matrix matrix)
    {
        // Transform bounding box corners to world coordinates
        var topLeft = TransformSKPoint(new SKPoint((float)boundingBox.Left, (float)boundingBox.Top), matrix);
        var topRight = TransformSKPoint(new SKPoint((float)boundingBox.Right, (float)boundingBox.Top), matrix);
        var bottomLeft = TransformSKPoint(new SKPoint((float)boundingBox.Left, (float)boundingBox.Bottom), matrix);
        var bottomRight = TransformSKPoint(new SKPoint((float)boundingBox.Right, (float)boundingBox.Bottom), matrix);

        double margin = Config.TransformEdgeMargin;

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

    /// <summary>
    /// Determines if a corner transformation point is positioned on either of the two edges adjacent to the specified corner handle, which would invalidate scaling operations.
    /// </summary>
    /// <param name="transformationPoint">The transformation point to test in world coordinates.</param>
    /// <param name="handleType">The corner handle type that determines which adjacent edges to check.</param>
    /// <param name="boundingBox">The element's bounding box in local coordinates.</param>
    /// <param name="matrix">The transformation matrix used to convert bounding box points to world coordinates.</param>
    /// <returns>True if the transformation point lies on either adjacent edge to the corner handle; otherwise, false.</returns>
    private bool IsCornerTransformationPointInvalid(SKPoint transformationPoint, DrawingCanvas.TransformHandleType handleType, CsXFL.Rectangle boundingBox, CsXFL.Matrix matrix)
    {
        // For corner handles, check if transformation point is on either adjacent edge
        double margin = Config.TransformEdgeMargin;

        // Transform bounding box corners to world coordinates
        var topLeft = TransformSKPoint(new SKPoint((float)boundingBox.Left, (float)boundingBox.Top), matrix);
        var topRight = TransformSKPoint(new SKPoint((float)boundingBox.Right, (float)boundingBox.Top), matrix);
        var bottomLeft = TransformSKPoint(new SKPoint((float)boundingBox.Left, (float)boundingBox.Bottom), matrix);
        var bottomRight = TransformSKPoint(new SKPoint((float)boundingBox.Right, (float)boundingBox.Bottom), matrix);

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

    /// <summary>
    /// Determines if a point lies within a specified margin distance from a line segment.
    /// </summary>
    /// <param name="point">The point to test.</param>
    /// <param name="lineStart">The starting point of the line segment.</param>
    /// <param name="lineEnd">The ending point of the line segment.</param>
    /// <param name="margin">The maximum distance from the line segment for the point to be considered "on" the line.</param>
    /// <returns>True if the point is within the margin distance from the line segment; otherwise, false.</returns>
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

    /// <summary>
    /// Determines if a shearing operation is invalid based on the transformation point's position relative to the specific edge being sheared.
    /// Shearing is invalid when the transformation point lies on the specific edge that would be used for shearing.
    /// </summary>
    /// <param name="transformationPoint">The transformation point to test in world coordinates.</param>
    /// <param name="mousePosition">The mouse position to determine which specific edge is being targeted.</param>
    /// <param name="boundingBox">The element's bounding box in local coordinates.</param>
    /// <param name="matrix">The transformation matrix used to convert bounding box points to world coordinates.</param>
    /// <returns>True if the transformation point lies on the specific edge being targeted for shearing; otherwise, false.</returns>
    private bool IsShearingInvalid(SKPoint transformationPoint, SKPoint mousePosition, CsXFL.Rectangle boundingBox, CsXFL.Matrix matrix)
    {
        // Transform bounding box corners to world coordinates
        var topLeft = TransformSKPoint(new SKPoint((float)boundingBox.Left, (float)boundingBox.Top), matrix);
        var topRight = TransformSKPoint(new SKPoint((float)boundingBox.Right, (float)boundingBox.Top), matrix);
        var bottomLeft = TransformSKPoint(new SKPoint((float)boundingBox.Left, (float)boundingBox.Bottom), matrix);
        var bottomRight = TransformSKPoint(new SKPoint((float)boundingBox.Right, (float)boundingBox.Bottom), matrix);

        double margin = Config.TransformEdgeMargin;

        // Get the edge regions and determine which specific edge is being targeted
        var edges = GetEdgeRegions(boundingBox, matrix).ToList();

        for (int i = 0; i < edges.Count; i++)
        {
            if (IsWithinEdge(mousePosition, edges[i], margin))
            {
                // Check if transformation point is on the specific edge being targeted
                switch (i)
                {
                    case 0: // Top edge
                        return IsPointOnLineSegment(transformationPoint, topLeft, topRight, margin);
                    case 1: // Bottom edge
                        return IsPointOnLineSegment(transformationPoint, bottomLeft, bottomRight, margin);
                    case 2: // Left edge
                        return IsPointOnLineSegment(transformationPoint, topLeft, bottomLeft, margin);
                    case 3: // Right edge
                        return IsPointOnLineSegment(transformationPoint, topRight, bottomRight, margin);
                }
            }
        }

        return false; // No edge is being targeted
    }
    #endregion
}