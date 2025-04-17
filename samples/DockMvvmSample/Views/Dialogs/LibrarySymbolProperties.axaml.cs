using Avalonia.Controls;
using Avalonia.Interactivity;
using DialogHostAvalonia;
using Avalonia.Markup.Xaml;
using Blitz.ViewModels.Tools;
using Blitz.ViewModels;
using System.Linq;
using System.Globalization;
using System;
using Serilog;
using System.Xml.Linq;
using Rendering;
using CsXFL;
using System.IO;
using Svg.Skia;

namespace Blitz.Views
{
    public partial class LibrarySymbolProperties : UserControl
    {
        private readonly BlitzAppData _blitzAppData;
        private LibraryViewModel _libraryViewModel;
        private MainWindowViewModel _mainWindowViewModel;
        public string? DialogIdentifier { get; set; }
        public string? SymbolName { get; set; }
        public string? SymbolType { get; set; }
        public ComboBox? TypeComboBox { get; set; }

        public LibrarySymbolProperties(CsXFL.Item item)
        {
            AvaloniaXamlLoader.Load(this);
            _blitzAppData = new BlitzAppData();
            TypeComboBox = this.FindControl<ComboBox>("Type");
            var _libraryViewModelRegistry = ViewModelRegistry.Instance;
            _libraryViewModel = (LibraryViewModel)_libraryViewModelRegistry.GetViewModel(nameof(LibraryViewModel));
            _mainWindowViewModel = (MainWindowViewModel)_libraryViewModelRegistry.GetViewModel(nameof(MainWindowViewModel));
            SetTextBoxText();
            SetComboBox();
        }

        private void SetComboBox()
        {
            var itemType = _libraryViewModel.UserLibrarySelection!.FirstOrDefault()?.ItemType;
            var titleCaseItemType = itemType != null 
                ? CultureInfo.CurrentCulture.TextInfo.ToTitleCase(itemType.ToLower()) 
                : null;

            // Find the ComboBoxItem with matching Content
            foreach (var item in Type!.Items)
            {
                if (item is ComboBoxItem comboBoxItem && comboBoxItem.Content?.ToString() == titleCaseItemType)
                {
                    Type.SelectedItem = comboBoxItem;
                    break;
                }
            }
        }

        private void SetTextBoxText()
        {
            string path = _libraryViewModel.UserLibrarySelection!.FirstOrDefault()?.Name!;
            int lastIndex = path.LastIndexOf('/');
            string fileName = lastIndex != -1 ? path.Substring(lastIndex + 1) : path;
            Name.Text = fileName;
        }

        private void OnCanvasPaint(object sender, SKPaintSurfaceEventArgs e)
        {
            string appDataFolder = _blitzAppData.GetTmpFolder();
            SVGRenderer renderer = new SVGRenderer(An.GetActiveDocument(), appDataFolder, true);
            var canvas = e.Surface.Canvas;

            try
            {
                var symbolToRender = _libraryViewModel.UserLibrarySelection[0] as CsXFL.SymbolItem;
                (XDocument renderedSVG, CsXFL.Rectangle bbox) = renderer.RenderSymbol(symbolToRender!.Timeline, 0);
                SkiaSharp.SKPicture renderedSymbol = null!;

                using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(renderedSVG.ToString())))
                {
                    var svg = new SKSvg();
                    svg.Load(stream);
                    renderedSymbol = svg.Picture;
                }

                // Use the cached picture
                var boundingBox = bbox!;
                var boundingBoxWidth = boundingBox.Right - boundingBox.Left;
                var boundingBoxHeight = boundingBox.Top - boundingBox.Bottom;
                var boundingBoxCenterX = boundingBox.Left + boundingBoxWidth / 2;
                var boundingBoxCenterY = boundingBox.Bottom + boundingBoxHeight / 2;

                canvas.Translate(canvas.LocalClipBounds.MidX, canvas.LocalClipBounds.MidY);
                canvas.Scale(0.9f * (float)Math.Min(canvas.LocalClipBounds.Width / boundingBoxWidth, canvas.LocalClipBounds.Height / boundingBoxHeight));
                canvas.Translate((float)-boundingBoxCenterX, (float)-boundingBoxCenterY);

                canvas.DrawPicture(renderedSymbol);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to render symbol: {ErrorMessage}", ex.Message);
            }
        }

        private void OkayButton_Click(object sender, RoutedEventArgs e)
        {
            var nameTextBox = this.FindControl<TextBox>("Name");
            SymbolName = nameTextBox?.Text; 
            SymbolType = TypeComboBox?.SelectedItem is ComboBoxItem comboBoxItem 
                ? comboBoxItem.Content?.ToString() 
                : null;
            var result = new
            {
                Name = SymbolName,
                Type = SymbolType,
                IsOkay = true
            };
            DialogHost.Close(DialogIdentifier, result);
        }
    }
}