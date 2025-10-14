using Avalonia.Rendering.SceneGraph;
using Avalonia.Media;
using Avalonia.Skia;
using SkiaSharp;

namespace Avalonia.Controls;

class CustomDrawOp : Avalonia.Rendering.SceneGraph.ICustomDrawOperation
{
    public Rect Bounds { get; }
    public bool HitTest(Point p) => false;
    public bool Equals(ICustomDrawOperation other) => false;
    private readonly SKPicture _compositedPicture;

    public CustomDrawOp(Rect bounds, SKPicture compositedPicture)
    {
        _compositedPicture = compositedPicture;
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
            canvas.DrawPicture(_compositedPicture);
        }
    }
}