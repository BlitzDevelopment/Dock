using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using SkiaSharp;
using Svg.Skia;
using System.Xml.Linq;
using Blitz.ViewModels;
using Blitz.ViewModels.Tools;
using System.IO;
using System;

namespace Blitz.Views.Tools;

public partial class LibraryView : UserControl
{
    private LibraryViewModel _libraryViewModel { set; get; }
    private MainWindowViewModel _mainWindowViewModel;
    private bool UseFlatSource = false;
    
    public LibraryView()
    {
        InitializeComponent();
        var _viewModelRegistry = ViewModelRegistry.Instance;
        _libraryViewModel = (LibraryViewModel)_viewModelRegistry.GetViewModel(nameof(LibraryViewModel));
        _mainWindowViewModel = (MainWindowViewModel)_viewModelRegistry.GetViewModel(nameof(MainWindowViewModel));
        LibrarySearch.TextChanged += OnLibrarySearchTextChanged!;
    }

    public void OnLibrarySearchTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_mainWindowViewModel.MainDocument == null) { return; }

        string searchText = "";
        var textBox = sender as TextBox;
        if (textBox != null) { searchText = textBox.Text!; }
        if (string.IsNullOrEmpty(searchText))
        {
            UseFlatSource = false;
            HierarchalTreeView.IsVisible = true;
            HierarchalTreeView.RowSelection!.Clear();
            _libraryViewModel.UserLibrarySelection = null;
            FlatTreeView.IsVisible = false;
            FlatTreeView.RowSelection!.Clear();
        }
        else if (UseFlatSource == false)
        {
            UseFlatSource = true;
            HierarchalTreeView.IsVisible = false;
            HierarchalTreeView.RowSelection!.Clear();
            _libraryViewModel.UserLibrarySelection = null;
            FlatTreeView.IsVisible = true;
            FlatTreeView.RowSelection!.Clear();
        }
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
                
                // Get bounding rectangle for SVG image
                var boundingBox = svg.Picture!.CullRect;

                // Translate and scale drawing canvas to fit SVG image
                canvas.Translate(canvas.LocalClipBounds.MidX, canvas.LocalClipBounds.MidY);
                canvas.Scale(0.9f * Math.Min(canvas.LocalClipBounds.Width / boundingBox.Width, canvas.LocalClipBounds.Height / boundingBox.Height));
                canvas.Translate(-boundingBox.MidX, -boundingBox.MidY);

                // Now finally draw the SVG image
                canvas.DrawPicture(svg.Picture);
            }
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
