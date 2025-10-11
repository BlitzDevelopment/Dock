using Avalonia.Media;
using SkiaSharp;

namespace Avalonia.Controls;

public partial class DrawingCanvas
{
    public override void Render(DrawingContext context)
    {
        if (_compositedPicture == null)
        {
            CompositeLayersToRenderTarget();
        }

        context.Custom(new CustomDrawOp(new Rect(0, 0, Width, Height), _compositedPicture));

        var adorningPicture = GenerateAdorningLayer();
        if (adorningPicture != null)
        {
            context.Custom(new CustomDrawOp(new Rect(0, 0, Width, Height), adorningPicture));
        }
    }

    public void CompositeLayersToRenderTarget()
    {
        var pictureLayers = CanvasController.GenerateCompositedPictures();

        using var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(new SKRect(0, 0, (float)Width, (float)Height));

        canvas.Clear(StageColor);

        foreach (var picture in pictureLayers)
        {
            if (picture != null)
            {
                canvas.DrawPicture(picture);
            }
        }

        _compositedPicture = recorder.EndRecording();
    }
}