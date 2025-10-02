using Avalonia.Input;
using System;
using SkiaSharp;
using Avalonia.Controls.Primitives;

namespace Avalonia.Controls;

public interface IDrawingCanvasTool
{
    void OnPointerPressed(DrawingCanvas canvas, PointerPressedEventArgs e);
    void OnPointerMoved(DrawingCanvas canvas, PointerEventArgs e);
    void OnPointerReleased(DrawingCanvas canvas, PointerReleasedEventArgs e);
    
}

#region SELECTION
public class SelectionTool : IDrawingCanvasTool
{
    public void OnPointerPressed(DrawingCanvas canvas, PointerPressedEventArgs e)
    {
        if (e.Handled)
        {
            Console.WriteLine("PointerPressed event was already handled.");
            return;
        }

        if (e.GetCurrentPoint(canvas).Properties.IsLeftButtonPressed)
        {
            var mousePosition = e.GetPosition(canvas);
            canvas.SelectedElement = canvas.HitTest(mousePosition);

            if (canvas.SelectedElement != null)
            {
                canvas.AdorningLayer.Visible = true;
                canvas.UpdateAdorningLayer(canvas.SelectedElement, SKColor.Parse("#388ff9")); // #388ff9 is a nice hardcoded blue color
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

    public void OnPointerMoved(DrawingCanvas canvas, PointerEventArgs e)
    {
        if (canvas.IsDragging && canvas.SelectedElement != null)
        {
            var currentPosition = e.GetPosition(canvas);
            var deltaX = currentPosition.X - canvas.DragStartPosition.X;
            var deltaY = currentPosition.Y - canvas.DragStartPosition.Y;

            ApplySkiaTranslation(canvas.SelectedElement, deltaX, deltaY);

            canvas.SelectedElement.Matrix.Tx += deltaX;
            canvas.SelectedElement.Matrix.Ty += deltaY;

            canvas.DragStartPosition = currentPosition;

            canvas.UpdateAdorningLayer(canvas.SelectedElement, SKColor.Parse("#388ff9"));
            canvas.CompositeLayersToRenderTarget();
            canvas.InvalidateVisual();
        }
    }

    public void OnPointerReleased(DrawingCanvas canvas, PointerReleasedEventArgs e)
    {
        if (canvas.IsDragging)
        {
            canvas.IsDragging = false;

            if (canvas.SelectedElement != null && canvas.SelectedElement.Model != null)
            {
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
    public void OnPointerPressed(DrawingCanvas canvas, PointerPressedEventArgs e)
    {
        // Implement transformation logic here
    }

    public void OnPointerMoved(DrawingCanvas canvas, PointerEventArgs e)
    {
        // Implement transformation logic here
    }

    public void OnPointerReleased(DrawingCanvas canvas, PointerReleasedEventArgs e)
    {
        // Implement transformation logic here
    }
}
#endregion