using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.Linq;
using DockMvvmSample.ViewModels.Tools;
using Avalonia.Markup.Xaml;
using DialogHostAvalonia;

namespace DockMvvmSample.Views
{
    public partial class LibrarySingleRename : UserControl
    {
        private Tool1ViewModel _viewModel;
        public string DialogIdentifier { get; set; }

        public LibrarySingleRename()
        {
            AvaloniaXamlLoader.Load(this);
            var _viewModelRegistry = ViewModelRegistry.Instance;
            _viewModel = (Tool1ViewModel)_viewModelRegistry.GetViewModel(nameof(Tool1ViewModel));
            SetTextBoxText();
        }

        private void SetTextBoxText()
        {
            string path = _viewModel.UserLibrarySelection.FirstOrDefault()?.Name;
            int lastIndex = path.LastIndexOf('/');
            string fileName = lastIndex != -1 ? path.Substring(lastIndex + 1) : path;
            InputRename.Text = fileName;
        }

        private void OkayButton_Click(object sender, RoutedEventArgs e)
        {
            DialogHost.Close(DialogIdentifier);
            Console.WriteLine();
        }
    }
}