using System;
using SkiaSharp;
using Avalonia.Skia;
using Avalonia.Platform;
using Avalonia.Media.Imaging;
using System.Threading.Tasks;

namespace Avalonia.Controls;

public partial class DrawingCanvas : UserControl
{
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

    private SKColor _adorningColor = SKColor.Parse("#388ff9");
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
            UpdateAdorningLayer(SelectedElement, _adorningColor);
        }

        InvalidateVisual();
    }

    public void RefreshCanvas()
    {
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
    }

    public override void EndInit()
    {
        SKPaint SKBrush = new SKPaint();
        SKBrush.IsAntialias = true;
        SKBrush.Color = new SKColor(0, 0, 0);
        SKBrush.Shader = SKShader.CreateColor(SKBrush.Color);
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