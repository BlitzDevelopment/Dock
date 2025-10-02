using Avalonia.Input;
using System;
using SkiaSharp;

namespace Avalonia.Controls;

public partial class DrawingCanvas
{
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (e.Handled)
        {
            Console.WriteLine("PointerPressed event was already handled.");
            return;
        }

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            base.OnPointerPressed(e);
            var mousePosition = e.GetPosition(this);
            SelectedElement = HitTest(mousePosition);

            if (SelectedElement != null)
            {
                AdorningLayer.Visible = true;
                UpdateAdorningLayer(SelectedElement, _adorningColor);
                InvalidateVisual();

                DragStartPosition = mousePosition;
                IsDragging = true;
                e.Handled = true;
            }
            else
            {
                SelectedElement = null;
                AdorningLayer.Elements.Clear();
                AdorningLayer.Visible = false;
                InvalidateVisual();
            }
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        if (IsDragging && SelectedElement != null)
        {
            var currentPosition = e.GetPosition(this);
            var deltaX = currentPosition.X - DragStartPosition.X;
            var deltaY = currentPosition.Y - DragStartPosition.Y;

            ApplySkiaTranslation(SelectedElement, deltaX, deltaY);

            SelectedElement.Matrix.Tx += deltaX;
            SelectedElement.Matrix.Ty += deltaY;

            DragStartPosition = currentPosition;

            UpdateAdorningLayer(SelectedElement, _adorningColor);
            CompositeLayersToRenderTarget();
            InvalidateVisual();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (IsDragging)
        {
            IsDragging = false;

            if (SelectedElement != null && SelectedElement.Model != null)
            {
                SelectedElement.Model.Matrix.Tx = SelectedElement.Matrix.Tx;
                SelectedElement.Model.Matrix.Ty = SelectedElement.Matrix.Ty;
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