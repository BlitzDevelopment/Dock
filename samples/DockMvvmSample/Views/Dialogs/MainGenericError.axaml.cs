using Avalonia.Controls;
using Avalonia.Interactivity;
using DialogHostAvalonia;
using Avalonia.Markup.Xaml;
using System.Media;
using CsXFL;

namespace Blitz.Views
{
    public partial class MainGenericError : UserControl
    {
        public string? DialogIdentifier { get; set; }

        public MainGenericError(string errorText)
        {
            AvaloniaXamlLoader.Load(this);
            SystemSounds.Exclamation.Play();
            DynamicText.Text = errorText;
        }

        private void OkayButton_Click(object sender, RoutedEventArgs e)
        {
            DialogHost.Close(DialogIdentifier, true);
        }
    }
}