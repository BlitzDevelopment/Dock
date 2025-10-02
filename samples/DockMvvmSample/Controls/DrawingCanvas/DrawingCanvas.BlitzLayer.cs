using System;
using System.Collections.Generic;

namespace Avalonia.Controls;

public class BlitzLayer
{
    public string Color { get; set; }
    public string LayerType { get; set; }
    public string Name { get; set; }
    public bool Locked { get; set; }
    public bool Current { get; set; }
    public bool Selected { get; set; }
    public bool Visible { get; set; }

    public List<BlitzElement> Elements { get; set; } = new List<BlitzElement>();
}

public static class LayerConverter
{
    public static BlitzLayer ConvertToBlitzLayer(CsXFL.Layer csxflLayer)
    {
        if (csxflLayer == null)
            throw new ArgumentNullException(nameof(csxflLayer));

        var blitzLayer = new BlitzLayer
        {
            Color = csxflLayer.Color,
            LayerType = csxflLayer.LayerType,
            Name = csxflLayer.Name,
            Locked = csxflLayer.Locked,
            Current = csxflLayer.Current,
            Selected = csxflLayer.Selected,
            Visible = csxflLayer.Visible
        };

        return blitzLayer;
    }
}