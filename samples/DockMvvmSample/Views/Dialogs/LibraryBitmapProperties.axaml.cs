using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Controls;
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
        public string? DialogIdentifier { get; set; }
        private Avalonia.Point _lastMousePosition;
        private ScaleTransform _scaleTransform;
        private TranslateTransform _translateTransform;
        private bool _isPanning;
        private const double MinZoom = 0.5;
        private const double MaxZoom = 4.0;
        private const double ZoomInFactor = 1.1;
        private const double ZoomOutFactor = 0.9;

        public LibraryBitmapProperties(CsXFL.BitmapItem item)
        {
            AvaloniaXamlLoader.Load(this);
            DataContext = this;
            var _libraryViewModelRegistry = ViewModelRegistry.Instance;
            _libraryViewModel = (LibraryViewModel)_libraryViewModelRegistry.GetViewModel(nameof(LibraryViewModel));
            _mainWindowViewModel = (MainWindowViewModel)_libraryViewModelRegistry.GetViewModel(nameof(MainWindowViewModel));
            _workingCsXFLDoc = CsXFL.An.GetActiveDocument();

            // Todo-- bitdepth?
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
                LibraryBitmapPreview.Source = new Avalonia.Media.Imaging.Bitmap(memoryStream);
            }

            SetTextBoxText();

            //Canvas logic
            var transformGroup = (TransformGroup)LibraryBitmapPreview.RenderTransform;
            _scaleTransform = (ScaleTransform)transformGroup.Children[0];
            _translateTransform = (TranslateTransform)transformGroup.Children[1];

            LibraryBitmapPreview.PointerWheelChanged += OnMouseWheelZoom;
            LibraryBitmapPreview.PointerPressed += OnMousePressed;
            LibraryBitmapPreview.PointerReleased += OnMouseReleased;
            LibraryBitmapPreview.PointerMoved += OnMouseMoved;
        }

        private void OnMouseWheelZoom(object? sender, PointerWheelEventArgs e)
        {
            var pointerPosition = e.GetPosition(LibraryBitmapPreview);
            double zoomFactor = e.Delta.Y > 0 ? ZoomInFactor : ZoomOutFactor;

            ApplyZoom(zoomFactor);
            e.Handled = true;
        }

        private void ApplyZoom(double zoomFactor)
        {
            // Calculate the new scale values
            var newScaleX = _scaleTransform.ScaleX * zoomFactor;
            var newScaleY = _scaleTransform.ScaleY * zoomFactor;

            // Clamp the zoom to min/max values
            newScaleX = Math.Max(MinZoom, Math.Min(MaxZoom, newScaleX));
            newScaleY = Math.Max(MinZoom, Math.Min(MaxZoom, newScaleY));

            // Calculate the center of the image in control coordinates
            var centerX = LibraryBitmapPreview.Bounds.Width / 2;
            var centerY = LibraryBitmapPreview.Bounds.Height / 2;

            // Calculate the current center position in image coordinates (before zoom)
            var imagePointX = (centerX - _translateTransform.X) / _scaleTransform.ScaleX;
            var imagePointY = (centerY - _translateTransform.Y) / _scaleTransform.ScaleY;

            // Apply the new scale
            _scaleTransform.ScaleX = newScaleX;
            _scaleTransform.ScaleY = newScaleY;

            // Adjust the translation to keep the center position fixed in image space
            _translateTransform.X = centerX - imagePointX * newScaleX;
            _translateTransform.Y = centerY - imagePointY * newScaleY;
        }

        private void OnMousePressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                _isPanning = true;
                _lastMousePosition = e.GetPosition(this);
                e.Handled = true;
            }
        }

        private void OnMouseReleased(object? sender, PointerReleasedEventArgs e)
        {
            _isPanning = false;
            e.Handled = true;
        }

        private void OnMouseMoved(object? sender, PointerEventArgs e)
        {
            if (_isPanning)
            {
                var currentPosition = e.GetPosition(this);
                var delta = currentPosition - _lastMousePosition;
                _lastMousePosition = currentPosition;

                _translateTransform.X += delta.X;
                _translateTransform.Y += delta.Y;

                e.Handled = true;
            }
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