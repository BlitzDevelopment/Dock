using Avalonia.Controls;
using Avalonia.Interactivity;
using DialogHostAvalonia;
using Avalonia.Markup.Xaml;
using System.Media;

namespace Blitz.Views
{
    public partial class MainGenericWarning : UserControl
    {
        public string? DialogIdentifier { get; set; }

        public MainGenericWarning(string warningText)
        {
            AvaloniaXamlLoader.Load(this);
            SystemSounds.Asterisk.Play();
            DynamicText.Text = warningText;
        }

        private void OkayButton_Click(object sender, RoutedEventArgs e)
        {
            DialogHost.Close(DialogIdentifier, true);
        }
    }
}