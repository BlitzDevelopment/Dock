using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using SkiaSharp;
using Svg.Skia;
using System.Xml.Linq;
using Blitz.ViewModels;
using Blitz.ViewModels.Tools;
using System.IO;

namespace Blitz.Views.Tools;

public partial class LibraryView : UserControl
{
    private LibraryViewModel _libraryViewModel;

    public LibraryView()
    {
        InitializeComponent();
        var _viewModelRegistry = ViewModelRegistry.Instance;
        _libraryViewModel = (LibraryViewModel)_viewModelRegistry.GetViewModel(nameof(LibraryViewModel));
    }

    private void OnCanvasPaint(object sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;

        if (_libraryViewModel.SvgData != null)
        {
            using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(_libraryViewModel.SvgData.ToString())))
            {
                var svg = new SKSvg();
                svg.Load(stream);

                var bounds = svg.Picture.CullRect;
                var scaleX = e.Info.Width / bounds.Width;
                var scaleY = e.Info.Height / bounds.Height;
                var scale = scaleX < scaleY ? scaleX : scaleY;

                canvas.Scale(scale);
                canvas.Translate((e.Info.Width - bounds.Width * scale) / 2, (e.Info.Height - bounds.Height * scale) / 2);

                canvas.DrawPicture(svg.Picture);
            }
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
