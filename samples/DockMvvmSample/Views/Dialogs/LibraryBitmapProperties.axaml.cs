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

namespace Blitz.Views
{
    public partial class LibraryBitmapProperties : UserControl
    {
        private EventAggregator _eventAggregator;
        private LibraryViewModel _libraryViewModel;
        private MainWindowViewModel _mainWindowViewModel;
        private CsXFL.Document _workingCsXFLDoc;
        public string? DialogIdentifier { get; set; }
        private Avalonia.Point _lastMousePosition;
        private ScaleTransform _scaleTransform;
        private TranslateTransform _translateTransform;
        private bool _isPanning;

        public LibraryBitmapProperties(CsXFL.BitmapItem item)
        {
            AvaloniaXamlLoader.Load(this);
            DataContext = this;
            var _libraryViewModelRegistry = ViewModelRegistry.Instance;
            _libraryViewModel = (LibraryViewModel)_libraryViewModelRegistry.GetViewModel(nameof(LibraryViewModel));
            _mainWindowViewModel = (MainWindowViewModel)_libraryViewModelRegistry.GetViewModel(nameof(MainWindowViewModel));
            _eventAggregator = EventAggregator.Instance;
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

            var delta = e.Delta.Y > 0 ? 1.1 : 0.9; // Smaller zoom increments for smoother zoom
            var newScaleX = _scaleTransform.ScaleX * delta;
            var newScaleY = _scaleTransform.ScaleY * delta;

            // Limit zoom to 4x zoom in and 0.5x zoom out
            if (newScaleX >= 0.5 && newScaleX <= 4.0 && newScaleY >= 0.5 && newScaleY <= 4.0)
            {
                // Calculate the offset of the pointer position relative to the image
                var relativeX = (pointerPosition.X - _translateTransform.X) / _scaleTransform.ScaleX;
                var relativeY = (pointerPosition.Y - _translateTransform.Y) / _scaleTransform.ScaleY;

                // Apply the new scale
                _scaleTransform.ScaleX = newScaleX;
                _scaleTransform.ScaleY = newScaleY;

                // Adjust the translation to keep the pointer position fixed
                _translateTransform.X = pointerPosition.X - relativeX * newScaleX;
                _translateTransform.Y = pointerPosition.Y - relativeY * newScaleY;
            }

            e.Handled = true;
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
            _eventAggregator = EventAggregator.Instance;
            _eventAggregator.Publish(new LibraryItemsChangedEvent());

            DialogHost.Close(DialogIdentifier);
        }
    }
}