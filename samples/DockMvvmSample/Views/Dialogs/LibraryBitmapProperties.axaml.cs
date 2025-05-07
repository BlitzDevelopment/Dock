using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Controls;
using Avalonia.Controls.PanAndZoom;
using Avalonia.Interactivity;
using Blitz.ViewModels.Tools;
using Avalonia.Markup.Xaml;
using DialogHostAvalonia;
using Blitz.ViewModels;
using System.IO;
using CsXFL;
using Blitz.Events;
using System;

namespace Blitz.Views
{
    public partial class LibraryBitmapProperties : UserControl
    {
        private LibraryViewModel _libraryViewModel;
        private MainWindowViewModel _mainWindowViewModel;
        private CsXFL.Document _workingCsXFLDoc;
        private readonly ZoomBorder? _zoomBorder;
        public string? DialogIdentifier { get; set; }

        public LibraryBitmapProperties(CsXFL.BitmapItem item)
        {
            AvaloniaXamlLoader.Load(this);
            DataContext = this;

            _zoomBorder = this.Find<ZoomBorder>("ZoomBorder");
            _zoomBorder.Background = new SolidColorBrush(Colors.Transparent);

            var _libraryViewModelRegistry = ViewModelRegistry.Instance;
            _libraryViewModel = (LibraryViewModel)_libraryViewModelRegistry.GetViewModel(nameof(LibraryViewModel));
            _mainWindowViewModel = (MainWindowViewModel)_libraryViewModelRegistry.GetViewModel(nameof(MainWindowViewModel));
            _workingCsXFLDoc = CsXFL.An.GetActiveDocument();

            // Todo-- bitdepth?
            if (_libraryViewModel.DocumentViewModel != null && item != null)
            {
                var bitmapData = _libraryViewModel.DocumentViewModel.GetBitmapData(item);
                var size = bitmapData.Length >= 1024 * 1024
                    ? $"{bitmapData.Length / (1024.0 * 1024.0):F2} MB"
                    : bitmapData.Length >= 1024
                        ? $"{bitmapData.Length / 1024.0:F2} KB"
                        : $"{bitmapData.Length} bytes";

                BitmapInfoDisplay.Text = item.HPixels + " x " + item.VPixels + " px " + size;

                // Directly set the cached bitmap data to the Image control
                using (var memoryStream = new MemoryStream(bitmapData))
                {
                    memoryStream.Seek(0, SeekOrigin.Begin); // Ensure the stream is at the beginning
                    var bitmap = new Avalonia.Media.Imaging.Bitmap(memoryStream);
                    LibraryBitmapPreview.Source = bitmap;

                    // Center the image inside its available space
                    LibraryBitmapPreview.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
                    LibraryBitmapPreview.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
                    
                    // Ensure the image scales proportionally while fitting within bounds
                    LibraryBitmapPreview.Stretch = Avalonia.Media.Stretch.Uniform;
                }
            }
            else
            {
                // Handle the null case appropriately (e.g., log an error, show a message, etc.)
                BitmapInfoDisplay.Text = "Unable to load bitmap data.";
            }

            SetTextBoxText();

        }

        private void SetTextBoxText()
        {
            string path = _libraryViewModel.UserLibrarySelection![0].Name;
            int lastIndex = path.LastIndexOf('/');
            string fileName = lastIndex != -1 ? path.Substring(lastIndex + 1) : path;
            InputRename.Text = fileName;
        }

        private void OkayButton_Click(object sender, RoutedEventArgs e)
        {
            CsXFL.Item ItemToRename = _libraryViewModel.UserLibrarySelection![0];

            string originalPath = ItemToRename.Name.Contains("/") ? ItemToRename.Name.Substring(0, ItemToRename.Name.LastIndexOf('/') + 1) : "";
            string newPath = originalPath + InputRename.Text!;

            _workingCsXFLDoc.Library.RenameItem(ItemToRename.Name, newPath);
            App.EventAggregator.Publish(new LibraryItemsChangedEvent());

            DialogHost.Close(DialogIdentifier);
        }
    }
}