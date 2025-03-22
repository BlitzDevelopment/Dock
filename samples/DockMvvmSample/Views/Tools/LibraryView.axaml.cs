using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Svg.Skia;
using Blitz.ViewModels;
using Blitz.ViewModels.Tools;
using System.IO;
using System;
using System.Linq;
using Avalonia.Styling;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using Avalonia.Controls.Models.TreeDataGrid;
using Blitz.Models;

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

        // Handle illegal input
        if (searchText.Contains('/') || searchText.Contains('\\'))
        {
            Flyout flyout = new Flyout();
            flyout.Content = new TextBlock { Text = "Illegal characters '/' or '\\' are not allowed." };
            flyout.ShowAt(textBox);

            // Dismiss the Flyout after 3 seconds
            Task.Delay(3000).ContinueWith(_ => 
            {
                flyout.Hide();
            }, TaskScheduler.FromCurrentSynchronizationContext());
            textBox.Text = new string(searchText.Where(c => c != '/' && c != '\\').ToArray());
        }

        if (string.IsNullOrEmpty(searchText))
        {
            UseFlatSource = false;
            HierarchalTreeView.IsVisible = true;
            HierarchalTreeView.RowSelection!.Clear();
            _libraryViewModel.UserLibrarySelection = null;
            FlatTreeView.IsVisible = false;
            FlatTreeView.RowSelection!.Clear();
        }
        else
        {
            if (!UseFlatSource) {
                UseFlatSource = true;
                HierarchalTreeView.IsVisible = false;
                HierarchalTreeView.RowSelection!.Clear();
                _libraryViewModel.UserLibrarySelection = null;
                FlatTreeView.IsVisible = true;
                FlatTreeView.RowSelection!.Clear();
                FlatTreeView.Source = _libraryViewModel.FlatSource;
            }

            // Use LINQ to filter items without modifying the original collection
            var filteredItems = _libraryViewModel.Items
                .Where(item => Path.GetFileName(item.Name).Contains(searchText, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Update FlatSource directly without clearing and repopulating FlatItems
            _libraryViewModel.FlatSource = new FlatTreeDataGridSource<Blitz.Models.Tools.Library.LibraryItem>(new ObservableCollection<Blitz.Models.Tools.Library.LibraryItem>(filteredItems))
            {
                Columns =
                {
                    new TextColumn<Blitz.Models.Tools.Library.LibraryItem, string>("Name", x => Path.GetFileName(x.Name)),
                    new TextColumn<Blitz.Models.Tools.Library.LibraryItem, string>("Type", x => x.Type),
                    new TextColumn<Blitz.Models.Tools.Library.LibraryItem, string>("Use Count", x => x.UseCount),
                },
            };
            FlatTreeView.Source = _libraryViewModel.FlatSource;
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
