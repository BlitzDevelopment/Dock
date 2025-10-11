using System;
using SkiaSharp;
using Avalonia.Skia;
using Avalonia.Platform;
using Avalonia.Media.Imaging;
using System.Threading.Tasks;

namespace Avalonia.Controls;

public partial class DrawingCanvas : UserControl
{
    private IDrawingCanvasTool _currentTool;
    public IDrawingCanvasTool ActiveTool { get; set; }
    public DrawingCanvasToolType ActiveToolType { get; private set; }

    public void SetActiveTool(DrawingCanvasToolType toolType)
    {
        // Unsubscribe from previous tool's events if applicable
        if (_currentTool is IDrawingCanvasTool interactiveTool)
        {
            PointerPressed -= interactiveTool.OnPointerPressed;
            PointerMoved -= interactiveTool.OnPointerMoved;
            PointerReleased -= interactiveTool.OnPointerReleased;
        }

        ActiveToolType = toolType;

        _currentTool = toolType switch
        {
            DrawingCanvasToolType.Selection => new SelectionTool(),
            DrawingCanvasToolType.Transformation => new TransformationTool(),
            _ => throw new ArgumentOutOfRangeException(nameof(toolType), "Unsupported tool type")
        };

        // Subscribe to the new tool's events if it implements IInteractiveTool
        if (_currentTool is IDrawingCanvasTool newInteractiveTool)
        {
            PointerPressed += newInteractiveTool.OnPointerPressed;
            PointerMoved += newInteractiveTool.OnPointerMoved;
            PointerReleased += newInteractiveTool.OnPointerReleased;
        }
    }

    public BlitzCanvasController CanvasController = new BlitzCanvasController();
    internal BlitzLayer AdorningLayer = new BlitzLayer
    {
        Visible = false
    };

    #region Selection State
    internal BlitzElement SelectedElement { get; set; }
    internal Point DragStartPosition { get; set; }
    internal bool IsDragging { get; set; }
    #endregion

    //Our render target we compile everything to and present to the user
    private RenderTargetBitmap RenderTarget;
    private SKPicture _compositedPicture;

    private SKColor _stageColor = SKColors.White;
    public SKColor StageColor
    {
        get => _stageColor;
        set
        {
            if (_stageColor != value)
            {
                _stageColor = value;
                InvalidateVisual();
            }
        }
    }

    private double _scale = 1.0;
    public double Scale
    {
        get => _scale;
        set
        {
            if (_scale != value)
            {
                _scale = value;
            }
            InvalidateVisual();
        }
    }

    public void SetScale(double scale)
    {
        if (scale <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(scale), "Scale must be greater than zero.");
        }

        Scale = scale;

        if (SelectedElement != null)
        {
            UpdateAdorningLayer(SelectedElement);
        }

        InvalidateVisual();
    }

    // Todo: redo this, it sucks
    public static readonly StyledProperty<bool> ClipToBoundsProperty =
        AvaloniaProperty.Register<DrawingCanvas, bool>(nameof(ClipToBounds), true);

    public bool ClipToBounds
    {
        get => GetValue(ClipToBoundsProperty);
        set => SetValue(ClipToBoundsProperty, value);
    }

    public DrawingCanvas()
    {
        // Bind the Avalonia ClipToBounds property to this control
        this.Bind(UserControl.ClipToBoundsProperty, this.GetObservable(ClipToBoundsProperty));
        Focusable = true;
        IsHitTestVisible = true;
        Background = Avalonia.Media.Brushes.Transparent;

        SetActiveTool(DrawingCanvasToolType.Transformation);
    }

    public override void EndInit()
    {
        RenderTarget = new RenderTargetBitmap(new PixelSize((int)Width, (int)Height), new Vector(96, 96));

        var drawingContext = RenderTarget.CreateDrawingContext();
        if (drawingContext is IDrawingContextImpl contextImpl)
        {
            var skiaFeature = contextImpl.GetFeature<ISkiaSharpApiLeaseFeature>();
            if (skiaFeature != null)
            {
                using (var lease = skiaFeature.Lease())
                {
                    SKCanvas canvas = lease.SkCanvas;
                    if (canvas != null)
                    {
                        canvas.Clear(new SKColor(255, 255, 255));
                    }
                }
            }
        }

        base.EndInit();
    }

    public Task<bool> SaveAsync(string path)
    {
        return Task.Run(() =>
        {
            try
            {
                RenderTarget.Save(path);
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        });
    }
}