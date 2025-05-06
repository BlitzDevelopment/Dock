using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System.Threading.Tasks;
using Avalonia.Animation;
using System;
using Avalonia.Styling;
using Blitz.ViewModels.Documents;
using Blitz.Events;
using CsXFL;
using System.IO;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rendering;
using System.Xml.Linq;
using Svg.Skia;
using SkiaSharp;
using System.Collections.Generic;
using System.Numerics;

namespace Blitz.Views.Documents;

public partial class DocumentView : UserControl
{
    private readonly DocumentViewModel _documentViewModel;
    private CsXFL.Document _workingCsXFLDoc;
    public DocumentView()
    {
        InitializeComponent();

        App.EventAggregator.Subscribe<DocumentFlyoutRequestedEvent>(OnDocumentFlyoutRequested);
        App.EventAggregator.Subscribe<DocumentProgressChangedEvent>(OnDocumentProgressChanged);
        App.EventAggregator.Subscribe<ActiveDocumentChangedEvent>(OnActiveDocumentChanged);

        SetProgressRingState(true);
        Task.Run(async () =>
            {
                await Task.Delay(5000);
                await Dispatcher.UIThread.InvokeAsync(() => SetProgressRingState(false));
            });
        ShowFlyoutAsync("Loaded " + Path.GetFileName(An.GetActiveDocument().Filename)).ConfigureAwait(false);

    }

    private void OnActiveDocumentChanged(ActiveDocumentChangedEvent e)
    {
       _workingCsXFLDoc = An.GetDocument(e.Document.DocumentIndex);
       ViewFrame();
    }

    private readonly Dictionary<string, (SKPicture RenderedPicture, CsXFL.Rectangle Bbox)> _svgCache = new();

    public static Matrix4x4 CreateAffine(double a, double b, double c, double d, double tx, double ty)
    {
        return new Matrix4x4
        {
            M11 = (float)a,
            M12 = (float)b,
            M21 = (float)c,
            M22 = (float)d,
            M14 = (float)tx,
            M24 = (float)ty,
            M33 = 1,
            M44 = 1
        };
    }
    private static Matrix4x4 DeserializeMatrix(Matrix? matrix)
    {
        if (matrix is null)
        {
            return Matrix4x4.Identity;
        }

        return CreateAffine(matrix.A, matrix.B, matrix.C, matrix.D, matrix.Tx, matrix.Ty);
    }

    private static Matrix SerializeMatrix(Matrix4x4 matrix)
    {
        return new Matrix
        {
            A = matrix.M11,
            B = matrix.M12,
            C = matrix.M21,
            D = matrix.M22,
            Tx = matrix.M14,
            Ty = matrix.M24
        };
    }

    private List<Action<SKCanvas>> _drawingInstructions = new();

    public void ViewFrame()
    {
        var operatingTimeline = _workingCsXFLDoc.Timelines[0];
        var operatingFrame = 0;

        var layers = operatingTimeline.Layers;

        // Precompute drawing instructions
        _drawingInstructions.Clear();
        for (int i = layers.Count - 1; i >= 0; i--)
        {
            var layer = layers[i];
            if (layer.GetFrameCount() <= operatingFrame)
            {
                continue; // Skip this layer and move to the next one
            }

            var frame = layer.GetFrame(operatingFrame);
            foreach (var element in frame.Elements)
            {
                if (element is CsXFL.SymbolInstance)
                {
                    try
                    {
                        var elementAsSymbolInstance = (CsXFL.SymbolInstance)element;
                        var elementAsSymbolItem = (CsXFL.SymbolItem)elementAsSymbolInstance.CorrespondingItem;

                        // Should also include AS3 & anything else that makes the visuals unique
                        string cacheKey = $"{elementAsSymbolItem.Name}_{elementAsSymbolInstance.FirstFrame}";

                        // Check if the element is already cached
                        if (!_svgCache.TryGetValue(cacheKey, out var cachedData))
                        {
                            string appDataFolder = App.BlitzAppData.GetTmpFolder();

                            SVGRenderer renderer = new SVGRenderer(_workingCsXFLDoc!, appDataFolder, true);
                            Console.WriteLine("Rendering symbol: " + elementAsSymbolItem.Name + " as " + elementAsSymbolInstance.FirstFrame);
                            (XDocument renderedSvgDoc, CsXFL.Rectangle renderedBbox) = renderer.RenderSymbol(elementAsSymbolItem.Timeline, elementAsSymbolInstance.FirstFrame);

                            // Convert the rendered SVG to SKPicture and cache it
                            using (var stream = new MemoryStream())
                            {
                                renderedSvgDoc.Save(stream); // Directly save the XDocument to the MemoryStream
                                stream.Seek(0, SeekOrigin.Begin); // Reset the stream position to the beginning

                                var svg = new SKSvg();
                                svg.Load(stream);

                                if (svg.Picture != null)
                                {
                                    cachedData = (svg.Picture, renderedBbox);
                                    _svgCache[cacheKey] = cachedData;
                                }
                            }
                        }

                        var (cachedRenderedPicture, bbox) = cachedData;

                        // Precompute the drawing action
                        _drawingInstructions.Add(canvas =>
                        {
                            canvas.Save();
                            ApplyTransformAndDraw(canvas, elementAsSymbolInstance, cachedRenderedPicture);
                            canvas.Restore();
                        });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error rendering symbol: {ex.Message}" + ex.StackTrace);
                    }
                }
            }
        }

        MainSkXamlCanvas.Width = MainCanvas.Width;
        MainSkXamlCanvas.Height = MainCanvas.Height;

        MainSkXamlCanvas.PaintSurface += (sender, args) =>
        {
            var canvas = args.Surface.Canvas;

            // Clear the canvas
            canvas.Clear(SkiaSharp.SKColors.Transparent);

            // Execute precomputed drawing instructions
            foreach (var drawAction in _drawingInstructions)
            {
                drawAction(canvas);
            }
        };
    }

    private void ApplyTransformAndDraw(SKCanvas canvas, CsXFL.SymbolInstance elementAsSymbolInstance, SKPicture cachedRenderedPicture)
    {
        try
        {
            // Fix this later
            System.Numerics.Matrix4x4 transformOrigin = System.Numerics.Matrix4x4.Identity;

            // Translate to the transformation point
            transformOrigin.M14 = (float)-elementAsSymbolInstance.TransformationPoint.X;
            transformOrigin.M24 = (float)-elementAsSymbolInstance.TransformationPoint.Y;

            var affineMatrix = CreateAffine(
                elementAsSymbolInstance.Matrix.A,
                elementAsSymbolInstance.Matrix.B,
                elementAsSymbolInstance.Matrix.C,
                elementAsSymbolInstance.Matrix.D,
                elementAsSymbolInstance.Matrix.Tx,
                elementAsSymbolInstance.Matrix.Ty
            );

            transformOrigin *= affineMatrix;

            transformOrigin.M14 += (float)elementAsSymbolInstance.TransformationPoint.X;
            transformOrigin.M24 += (float)elementAsSymbolInstance.TransformationPoint.Y;

            SKMatrix ImplicitConversationOperator = new SKMatrix(
                transformOrigin.M11,
                -transformOrigin.M12,
                transformOrigin.M14,
                -transformOrigin.M21,
                transformOrigin.M22,
                transformOrigin.M24,
                0, 0, 1
            );

            canvas.SetMatrix(ImplicitConversationOperator);

            // Draw the cached picture
            if (cachedRenderedPicture != null)
            {
                canvas.DrawPicture(cachedRenderedPicture);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error applying transform and drawing: {ex.Message}" + ex.StackTrace);
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public async void SetProgressRingState(bool isActive)
    {
        if (ProgressRingControl != null)
        {
            // Create an animation for fading opacity
            var animation = new Animation
            {
                Duration = TimeSpan.FromSeconds(0.5),
                Children =
                {
                    new KeyFrame
                    {
                        Cue = new Cue(0),
                        Setters =
                        {
                            new Setter(Control.OpacityProperty, isActive ? 0 : 1) // Start opacity
                        }
                    },
                    new KeyFrame
                    {
                        Cue = new Cue(1),
                        Setters =
                        {
                            new Setter(Control.OpacityProperty, isActive ? 1 : 0) // End opacity
                        }
                    }
                }
            };

            // Run the animation asynchronously
            await animation.RunAsync(ProgressRingControl);

            // Toggle visibility after the animation completes
            ProgressRingControl.IsVisible = isActive;
        }
    }

    private void OnDocumentFlyoutRequested(DocumentFlyoutRequestedEvent e)
    {
        if (e.Document == _documentViewModel)
        {
            ShowFlyoutAsync(e.FlyoutMessage).ConfigureAwait(false);
        }
    }

    private void OnDocumentProgressChanged(DocumentProgressChangedEvent e)
    {
        if (e.Document == _documentViewModel)
        {
            SetProgressRingState(e.IsInProgress);
        }
    }

    public async Task ShowFlyoutAsync(string message, int durationInMilliseconds = 3000)
    {
        FlyoutText.Text = message;
        FlyoutContainer.IsVisible = true;
        await Task.Delay(durationInMilliseconds);
        FlyoutContainer.IsVisible = false;
    }
}