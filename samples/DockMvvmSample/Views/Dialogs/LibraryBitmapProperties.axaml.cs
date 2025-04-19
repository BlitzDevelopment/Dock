using Avalonia.Controls;
using Avalonia.Interactivity;
using Blitz.ViewModels.Tools;
using Avalonia.Markup.Xaml;
using DialogHostAvalonia;
using Blitz.ViewModels;

namespace Blitz.Views
{
    public partial class LibraryBitmapProperties : UserControl
    {
        private EventAggregator _eventAggregator;
        private LibraryViewModel _libraryViewModel;
        private MainWindowViewModel _mainWindowViewModel;
        private CsXFL.Document _workingCsXFLDoc;
        public string? DialogIdentifier { get; set; }

        public LibraryBitmapProperties(CsXFL.Item item)
        {
            AvaloniaXamlLoader.Load(this);
            DataContext = this;
            var _libraryViewModelRegistry = ViewModelRegistry.Instance;
            _libraryViewModel = (LibraryViewModel)_libraryViewModelRegistry.GetViewModel(nameof(LibraryViewModel));
            _mainWindowViewModel = (MainWindowViewModel)_libraryViewModelRegistry.GetViewModel(nameof(MainWindowViewModel));
            _eventAggregator = EventAggregator.Instance;
            _workingCsXFLDoc = CsXFL.An.GetActiveDocument();
        }

        private void OkayButton_Click(object sender, RoutedEventArgs e)
        {
            DialogHost.Close(DialogIdentifier);
        }
    }
}