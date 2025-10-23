using Avalonia.Input;
using SkiaSharp;

namespace Avalonia.Controls;

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

            // Update the model's matrix translation
            canvas.SelectedElement.Matrix.Tx += deltaX;
            canvas.SelectedElement.Matrix.Ty += deltaY;
            canvas.SelectedElement.UpdateTransform();
            canvas.SelectedElement.SyncToModel();

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
                canvas.SelectedElement.SyncToModel();
            }

            e.Handled = true;
        }
    }
}