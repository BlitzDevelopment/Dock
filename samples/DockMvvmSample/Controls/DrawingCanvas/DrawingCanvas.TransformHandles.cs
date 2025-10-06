using SkiaSharp;
using System.Collections.Generic;

namespace Avalonia.Controls;

public interface ITransformHandlesProvider
{
    List<(SKPoint Center, DrawingCanvas.TransformHandleType Type)> GetTransformHandles(CsXFL.Rectangle bbox, CsXFL.Matrix matrix);
}

public partial class DrawingCanvas: ITransformHandlesProvider
{
    public enum TransformHandleType
    {
        TopLeft,
        TopCenter,
        TopRight,
        RightCenter,
        BottomRight,
        BottomCenter,
        BottomLeft,
        LeftCenter
    }

    public List<(SKPoint Center, TransformHandleType Type)> GetTransformHandles(CsXFL.Rectangle bbox, CsXFL.Matrix matrix)
    {
        var topLeft = TransformPoint(matrix, bbox.Left, bbox.Top);
        var topRight = TransformPoint(matrix, bbox.Right, bbox.Top);
        var bottomLeft = TransformPoint(matrix, bbox.Left, bbox.Bottom);
        var bottomRight = TransformPoint(matrix, bbox.Right, bbox.Bottom);

        return new List<(SKPoint, TransformHandleType)>
        {
            (topLeft, TransformHandleType.TopLeft),
            (new SKPoint((topLeft.X + topRight.X) / 2, (topLeft.Y + topRight.Y) / 2), TransformHandleType.TopCenter),
            (topRight, TransformHandleType.TopRight),
            (new SKPoint((topRight.X + bottomRight.X) / 2, (topRight.Y + bottomRight.Y) / 2), TransformHandleType.RightCenter),
            (bottomRight, TransformHandleType.BottomRight),
            (new SKPoint((bottomLeft.X + bottomRight.X) / 2, (bottomLeft.Y + bottomRight.Y) / 2), TransformHandleType.BottomCenter),
            (bottomLeft, TransformHandleType.BottomLeft),
            (new SKPoint((topLeft.X + bottomLeft.X) / 2, (topLeft.Y + bottomLeft.Y) / 2), TransformHandleType.LeftCenter)
        };
    }
}