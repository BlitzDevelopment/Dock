using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Svg.Skia;
using Blitz.ViewModels.Tools;
using Blitz.Events;
using System.IO;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using Avalonia.Controls.Models.TreeDataGrid;

namespace Blitz.Views.Tools;

public partial class LibraryView : UserControl
{
    private readonly EventAggregator _eventAggregator;
    private LibraryViewModel _libraryViewModel { set; get; }
    private CsXFL.Document? WorkingCsXFLDoc;
    private bool UseFlatSource = false;
    
    public LibraryView()
    {
        InitializeComponent();
        var _viewModelRegistry = ViewModelRegistry.Instance;
        _libraryViewModel = (LibraryViewModel)_viewModelRegistry.GetViewModel(nameof(LibraryViewModel));
        _eventAggregator = EventAggregator.Instance;
        _eventAggregator.Subscribe<ActiveDocumentChangedEvent>(OnActiveDocumentChanged);
        WorkingCsXFLDoc = null;

        LibrarySearch.TextChanged += OnLibrarySearchTextChanged!;
    }

    private void OnActiveDocumentChanged(ActiveDocumentChangedEvent e)
    {
        WorkingCsXFLDoc = e.NewDocument!;
    }

    /// <summary>
    /// Handles the text changed event for the library search TextBox.
    /// Updates the library view based on the search text entered by the user.
    /// </summary>
    /// <param name="sender">The source of the event, typically the TextBox where the text is being entered.</param>
    /// <param name="e">The event arguments containing information about the text change.</param>
    /// <remarks>
    /// - If the search text contains illegal characters ('/' or '\\'), a Flyout is displayed to notify the user,
    ///   and the illegal characters are removed from the TextBox.
    /// - If the search text is empty, the hierarchical tree view is displayed, and the flat tree view is hidden.
    /// - If the search text is not empty, the flat tree view is displayed with filtered items based on the search text.
    /// - The filtering is case-insensitive and matches the search text against the file name of each library item.
    /// </remarks>
    public void OnLibrarySearchTextChanged(object sender, TextChangedEventArgs e)
    {
        if (WorkingCsXFLDoc == null) { return; }

        string searchText = "";
        var textBox = sender as TextBox;
        if (textBox != null) { searchText = textBox.Text!; }

        // Handle illegal input
        if (searchText.Contains('/') || searchText.Contains('\\'))
        {
            if(textBox == null) { return; }
            
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

    /// <summary>
    /// Handles the paint event for the canvas and renders an SVG image onto it.
    /// </summary>
    /// <param name="sender">The source of the event, typically the canvas control.</param>
    /// <param name="e">The event arguments containing the surface to paint on.</param>
    /// <remarks>
    /// This method checks if the SVG data is available in the associated view model. 
    /// If available, it loads the SVG data into an SKSvg object, calculates the bounding 
    /// rectangle of the SVG image, and adjusts the canvas's translation and scaling to 
    /// fit the SVG image within the canvas bounds. Finally, it draws the SVG image onto 
    /// the canvas.
    /// </remarks>
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
