using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Linq;
using Blitz.ViewModels.Tools;
using Avalonia.Markup.Xaml;
using DialogHostAvalonia;
using Blitz.ViewModels;

namespace Blitz.Views
{
    public partial class LibrarySingleRename : UserControl
    {
        private LibraryViewModel _viewModel;
        private MainWindowViewModel _mainWindowViewModel;
        private CsXFL.Document WorkingCsXFLDoc;
        public string? DialogIdentifier { get; set; }

        public LibrarySingleRename()
        {
            AvaloniaXamlLoader.Load(this);
            var _viewModelRegistry = ViewModelRegistry.Instance;
            _viewModel = (LibraryViewModel)_viewModelRegistry.GetViewModel(nameof(LibraryViewModel));
            _mainWindowViewModel = (MainWindowViewModel)_viewModelRegistry.GetViewModel(nameof(MainWindowViewModel));
            SetTextBoxText();
            WorkingCsXFLDoc = CsXFL.An.GetActiveDocument();
        }

        private void SetTextBoxText()
        {
            string path = _viewModel.UserLibrarySelection!.FirstOrDefault()?.Name!;
            int lastIndex = path.LastIndexOf('/');
            string fileName = lastIndex != -1 ? path.Substring(lastIndex + 1) : path;
            InputRename.Text = fileName;
        }

        private void OkayButton_Click(object sender, RoutedEventArgs e)
        {
            string ReName = InputRename.Text!;

            CsXFL.Item ItemToRename = _viewModel.UserLibrarySelection![0];

            string originalPath = ItemToRename.Name.Contains("/") ? ItemToRename.Name.Substring(0, ItemToRename.Name.LastIndexOf('/') + 1) : "";
            string newPath = originalPath + ReName;

            WorkingCsXFLDoc.Library.RenameItem(ItemToRename.Name, newPath);
            
            // TODO: Find appropriate logic for updating both HierarchicalSource and FlatSource
            
            // var libraryItem = _viewModel.HierarchicalSource.RowSelection!.SelectedItems.OfType<LibraryItem>().FirstOrDefault();
            // if (libraryItem != null)
            // {
            //     int lastIndex = newPath.LastIndexOf('/');
            //     string newFileName = lastIndex != -1 ? newPath.Substring(lastIndex + 1) : newPath;
            //     libraryItem.Name = newFileName;
            // }

            DialogHost.Close(DialogIdentifier);
        }
    }
}