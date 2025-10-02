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

    public void LoadSvg(XDocument svgDocument, int width, int height, CsXFL.Rectangle bbox)
    {
        var svg = new SKSvg();
        using (var stream = new MemoryStream())
        {
            svgDocument.Save(stream);
            stream.Seek(0, SeekOrigin.Begin);
            svg.Load(stream);
        }

        Picture = svg.Picture;

        var info = new SKImageInfo(width, height);
        Surface = SKSurface.Create(info);
        var canvas = Surface.Canvas;

        canvas.Clear(SKColors.Transparent);
        canvas.DrawPicture(Picture);
        canvas.Flush();

        Image = Surface.Snapshot();

        BBox = bbox;
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