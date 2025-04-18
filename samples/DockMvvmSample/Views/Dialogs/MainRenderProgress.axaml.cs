using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using DialogHostAvalonia;

namespace Blitz.Views
{
    public partial class MainRenderProgress : UserControl
    {
        public string? DialogIdentifier { get; set; }

        public MainRenderProgress()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public void UpdateProgress(int progress)
        {
            var progressBar = this.FindControl<ProgressBar>("ProgressBar");
            progressBar!.Value = progress;
        }

        private void OkayButton_Click(object sender, RoutedEventArgs e)
        {
            DialogHost.Close(DialogIdentifier);
        }
    }
}