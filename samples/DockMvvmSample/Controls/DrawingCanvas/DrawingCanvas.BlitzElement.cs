using System;
using System.IO;
using System.Xml.Linq;
using SkiaSharp;
using Svg.Skia;
using DockMvvmSample.Services;

namespace Avalonia.Controls;

public class BlitzElement
{
    public SKImage Image { get; set; }
    public SKSurface Surface { get; set; }
    private SKPicture _identityPicture;
    public SKPicture Picture { get; set; }

    public CsXFL.Element Model { get; set; }

    public CsXFL.Matrix Matrix { get; set; }

    public string ElementType { get; set; }
    public string Name { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public bool Selected { get; set; }
    public double ScaleX { get; set; }
    public double ScaleY { get; set; }
    public CsXFL.Point TransformationPoint { get; set; }
    public CsXFL.Rectangle BBox { get; private set; }
    
    public void LoadSvg(XDocument svgDocument)
    {
        var svg = new SKSvg();
        using (var stream = new MemoryStream())
        {
            svgDocument.Save(stream);
            stream.Seek(0, SeekOrigin.Begin);
            svg.Load(stream);
        }

        _identityPicture = svg.Picture;

        // Get initial BBox from service
        UpdateBBoxFromService();
        UpdateTransform();

        // Calculate width and height from bounding box
        int width = (int)Math.Ceiling(BBox.Right - BBox.Left);
        int height = (int)Math.Ceiling(BBox.Bottom - BBox.Top);

        var info = new SKImageInfo(width, height);
        Surface = SKSurface.Create(info);
        var canvas = Surface.Canvas;

        canvas.Clear(SKColors.Transparent);
        canvas.DrawPicture(Picture);
        canvas.Flush();

        Image = Surface.Snapshot();
    }

    private void UpdateBBoxFromService()
    {
        if (Model != null)
        {
            try
            {
                // Get the dynamic BBox from the rendering service
                // Use transformBBoxByMatrix: false to get the identity BBox, then we'll transform it ourselves
                BBox = RenderingService.Instance.GetElementBBox(Model, frameIndex: 0, transformBBoxByMatrix: false);
            }
            catch (Exception ex)
            {
                // Fallback to a default bbox if service is not available
                Console.WriteLine($"Failed to get BBox from RenderingService: {ex.Message}");
                BBox = new CsXFL.Rectangle(0, 0, 100, 100); // Default fallback
            }
        }
    }

    public void UpdateTransform()
    {
        if (_identityPicture == null || Matrix == null)
            return;

        // Get the transformed BBox from the rendering service
        CsXFL.Rectangle transformedBBox;
        try
        {
            // Use transformBBoxByMatrix: true to get the already transformed bbox
            transformedBBox = RenderingService.Instance.GetElementBBox(Model, frameIndex: 0, transformBBoxByMatrix: true);
        }
        catch (Exception ex)
        {
            // Fallback to calculating it ourselves if service fails
            Console.WriteLine($"Failed to get transformed BBox from RenderingService: {ex.Message}");
            var calculatedRect = CalculateTransformedCullRect();
            transformedBBox = new CsXFL.Rectangle(calculatedRect.Left, calculatedRect.Top, 
                                                calculatedRect.Right, calculatedRect.Bottom);
        }

        var transformedCullRect = new SKRect((float)transformedBBox.Left, (float)transformedBBox.Top, 
                                            (float)transformedBBox.Right, (float)transformedBBox.Bottom);

        // Create new picture
        using var recorder = new SKPictureRecorder();
        var recordingCanvas = recorder.BeginRecording(transformedCullRect);

        // Apply transformation matrix
        var skMatrix = ConvertToSKMatrix(Matrix);
        recordingCanvas.SetMatrix(skMatrix);
        recordingCanvas.DrawPicture(_identityPicture);
        Picture?.Dispose();
        Picture = recorder.EndRecording();
    }

    private SKRect CalculateTransformedCullRect()
    {
        if (_identityPicture == null)
            return SKRect.Empty;

        var originalCull = _identityPicture.CullRect;
        var skMatrix = ConvertToSKMatrix(Matrix);

        // Transform all four corners of the original cull rect
        var corners = new SKPoint[]
        {
            new SKPoint(originalCull.Left, originalCull.Top),
            new SKPoint(originalCull.Right, originalCull.Top),
            new SKPoint(originalCull.Right, originalCull.Bottom),
            new SKPoint(originalCull.Left, originalCull.Bottom)
        };

        // Apply transformation to each corner
        var transformedCorners = new SKPoint[corners.Length];
        skMatrix.MapPoints(transformedCorners, corners);

        // Find the bounding rectangle of transformed corners
        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;

        foreach (var corner in transformedCorners)
        {
            minX = Math.Min(minX, corner.X);
            minY = Math.Min(minY, corner.Y);
            maxX = Math.Max(maxX, corner.X);
            maxY = Math.Max(maxY, corner.Y);
        }

        return new SKRect(minX, minY, maxX, maxY);
    }
    
    public SKRect GetTransformedBBox()
    {
        var matrix = Matrix;
        var bbox = BBox;
        
        if (matrix == null)
            return new SKRect((float)bbox.Left, (float)bbox.Top, (float)bbox.Right, (float)bbox.Bottom);

        // Get all four corners of the bounding box
        var topLeft = new SKPoint((float)bbox.Left, (float)bbox.Top);
        var topRight = new SKPoint((float)bbox.Right, (float)bbox.Top);
        var bottomLeft = new SKPoint((float)bbox.Left, (float)bbox.Bottom);
        var bottomRight = new SKPoint((float)bbox.Right, (float)bbox.Bottom);

        // Transform all corners
        var skMatrix = ConvertToSKMatrix(matrix);
        var transformedTopLeft = skMatrix.MapPoint(topLeft);
        var transformedTopRight = skMatrix.MapPoint(topRight);
        var transformedBottomLeft = skMatrix.MapPoint(bottomLeft);
        var transformedBottomRight = skMatrix.MapPoint(bottomRight);

        // Find the axis-aligned bounding box of the transformed corners
        float minX = Math.Min(Math.Min(transformedTopLeft.X, transformedTopRight.X), 
                            Math.Min(transformedBottomLeft.X, transformedBottomRight.X));
        float minY = Math.Min(Math.Min(transformedTopLeft.Y, transformedTopRight.Y), 
                            Math.Min(transformedBottomLeft.Y, transformedBottomRight.Y));
        float maxX = Math.Max(Math.Max(transformedTopLeft.X, transformedTopRight.X), 
                            Math.Max(transformedBottomLeft.X, transformedBottomRight.X));
        float maxY = Math.Max(Math.Max(transformedTopLeft.Y, transformedTopRight.Y), 
                            Math.Max(transformedBottomLeft.Y, transformedBottomRight.Y));

        return new SKRect(minX, minY, maxX, maxY);
    }

    private SKMatrix ConvertToSKMatrix(CsXFL.Matrix matrix)
    {
        // CsXFL Matrix format: A B C D Tx Ty
        // SKMatrix format: ScaleX, SkewY, SkewX, ScaleY, TransX, TransY, Persp0, Persp1, Persp2
        return new SKMatrix
        {
            ScaleX = (float)matrix.A,
            SkewY = (float)matrix.B,
            SkewX = (float)matrix.C,
            ScaleY = (float)matrix.D,
            TransX = (float)matrix.Tx,
            TransY = (float)matrix.Ty,
            Persp0 = 0,
            Persp1 = 0,
            Persp2 = 1
        };
    }

    public void SyncToModel()
    {
        if (Model == null)
            return;

        // Can't and shouldn't change ElementType
        Model.Name = Name;
        Model.Width = Width;
        Model.Height = Height;
        Model.Selected = Selected;
        Model.Matrix = Matrix;
        Model.ScaleX = ScaleX;
        Model.ScaleY = ScaleY;
        Model.TransformationPoint.X = TransformationPoint.X;
        Model.TransformationPoint.Y = TransformationPoint.Y;
    }

}

public static class ElementConverter
{
    public static BlitzElement ConvertToBlitzElement(CsXFL.Element csxflelement)
    {
        if (csxflelement == null)
            throw new ArgumentNullException(nameof(csxflelement));

        // TODO: Being patient for width and height helpers...
        var blitzelement = new BlitzElement
        {
            Model = csxflelement,
            ElementType = csxflelement.ElementType,
            Name = csxflelement.Name,
            Width = 0,
            Height = 0,
            Selected = csxflelement.Selected,
            Matrix = csxflelement.Matrix,
            ScaleX = csxflelement.ScaleX,
            ScaleY = csxflelement.ScaleY,
            TransformationPoint = csxflelement.TransformationPoint
        };

        return blitzelement;
    }
}