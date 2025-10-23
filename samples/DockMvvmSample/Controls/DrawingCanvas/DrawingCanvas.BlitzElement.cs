using System;
using System.IO;
using System.Xml.Linq;
using SkiaSharp;
using Svg.Skia;

namespace Avalonia.Controls;

public class BlitzElement
{
    public SKImage Image { get; set; }
    public SKSurface Surface { get; set; }
    private SKPicture _identityPicture;
    public SKPicture Picture { get; set; }

    public CsXFL.Element Model { get; set; }

    public string ElementType { get; set; }
    public string Name { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public bool Selected { get; set; }
    public CsXFL.Matrix Matrix { get; set; }
    public double ScaleX { get; set; }
    public double ScaleY { get; set; }
    public CsXFL.Point TransformationPoint { get; set; }

    public CsXFL.Rectangle BBox { get; private set; }

    public void LoadSvg(XDocument svgDocument, CsXFL.Rectangle bbox)
    {
        var svg = new SKSvg();
        using (var stream = new MemoryStream())
        {
            svgDocument.Save(stream);
            stream.Seek(0, SeekOrigin.Begin);
            svg.Load(stream);
        }

        _identityPicture = svg.Picture;
        BBox = bbox;
        UpdateTransform();

        // Calculate width and height from bounding box
        int width = (int)Math.Ceiling(bbox.Right - bbox.Left);
        int height = (int)Math.Ceiling(bbox.Bottom - bbox.Top);

        var info = new SKImageInfo(width, height);
        Surface = SKSurface.Create(info);
        var canvas = Surface.Canvas;

        canvas.Clear(SKColors.Transparent);
        canvas.DrawPicture(Picture);
        canvas.Flush();

        Image = Surface.Snapshot();
    }

    public void UpdateTransform()
    {
        if (_identityPicture == null || Matrix == null)
            return;
        
        var transformedCullRect = CalculateTransformedCullRect();

        // Create new picture
        using var recorder = new SKPictureRecorder();
        var recordingCanvas = recorder.BeginRecording(transformedCullRect);
        
        // Apply transformation matrix
        var skMatrix = ConvertToSKMatrix(Matrix);
        recordingCanvas.SetMatrix(skMatrix);
        recordingCanvas.DrawPicture(_identityPicture);
        Picture?.Dispose();
        Picture = recorder.EndRecording();

        Console.WriteLine("BBox after UpdateTransform: " + BBox.Left + ", " + BBox.Top + ", " + BBox.Right + ", " + BBox.Bottom);
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