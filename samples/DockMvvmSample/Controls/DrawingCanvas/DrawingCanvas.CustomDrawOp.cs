using Avalonia.Rendering.SceneGraph;
using Avalonia.Media;
using Avalonia.Skia;
using SkiaSharp;

namespace Avalonia.Controls;

class CustomDrawOp : Avalonia.Rendering.SceneGraph.ICustomDrawOperation
{
    private readonly SKPicture _compositedPicture;
    private readonly double _scale;

    public Rect Bounds { get; }
    public bool HitTest(Point p) => false;
    public bool Equals(ICustomDrawOperation other) => false;

    public CustomDrawOp(Rect bounds, SKPicture compositedPicture, double scale)
    {
        _compositedPicture = compositedPicture;
        _scale = scale;
        Bounds = bounds;
    }

    public void Dispose()
    {
        // No-op
    }

    public void Render(ImmediateDrawingContext context)
    {
        var leaseFeature = context.TryGetFeature(typeof(ISkiaSharpApiLeaseFeature)) as ISkiaSharpApiLeaseFeature;
        if (leaseFeature == null || _compositedPicture == null)
            return;

        using var lease = leaseFeature.Lease();
        var canvas = lease.SkCanvas;

        if (canvas != null)
        {
            canvas.DrawPicture(_compositedPicture, 0, 0);
            canvas.Scale((float)_scale, (float)_scale);
        }
    }
}