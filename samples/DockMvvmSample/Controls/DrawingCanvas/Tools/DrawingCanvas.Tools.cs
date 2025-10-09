using Avalonia.Input;

namespace Avalonia.Controls;

public enum DrawingCanvasToolType
{
    Selection,
    Transformation
}

public interface IDrawingCanvasTool
{
    void OnPointerPressed(object? sender, PointerPressedEventArgs e);
    void OnPointerMoved(object? sender, PointerEventArgs e);
    void OnPointerReleased(object? sender, PointerReleasedEventArgs e);

}