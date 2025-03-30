using Avalonia.Controls;
using Avalonia.Interactivity;
using DialogHostAvalonia;
using Avalonia.Markup.Xaml;
using Blitz.ViewModels.Tools;
using Blitz.ViewModels;
using System.Linq;
using System.Globalization;
using System;
using System.Reactive.Subjects;

namespace Blitz.Views
{
    public partial class LibrarySymbolProperties : UserControl
    {
        private LibraryViewModel _viewModel;
        private MainWindowViewModel _mainWindowViewModel;
        public string? DialogIdentifier { get; set; }
        public string? SymbolName { get; set; }
        public string? SymbolType { get; set; }
        public ComboBox? TypeComboBox { get; set; }

        public LibrarySymbolProperties(CsXFL.Item item)
        {
            AvaloniaXamlLoader.Load(this);
            TypeComboBox = this.FindControl<ComboBox>("Type");
            var _viewModelRegistry = ViewModelRegistry.Instance;
            _viewModel = (LibraryViewModel)_viewModelRegistry.GetViewModel(nameof(LibraryViewModel));
            _mainWindowViewModel = (MainWindowViewModel)_viewModelRegistry.GetViewModel(nameof(MainWindowViewModel));
            SetTextBoxText();
            SetComboBox();
        }

        private void SetComboBox()
        {
            var itemType = _viewModel.UserLibrarySelection!.FirstOrDefault()?.ItemType;
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
            string path = _viewModel.UserLibrarySelection!.FirstOrDefault()?.Name!;
            int lastIndex = path.LastIndexOf('/');
            string fileName = lastIndex != -1 ? path.Substring(lastIndex + 1) : path;
            Name.Text = fileName;
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