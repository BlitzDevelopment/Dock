using DialogHostAvalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Interactivity;
using DockMvvmSample.ViewModels;
using Avalonia.Media;
using Avalonia.Controls.ApplicationLifetimes;
using System;
using System.IO;
using System.Collections.Generic;

namespace DockMvvmSample.Views
{
    public partial class MainVideoRender : UserControl
    { 
        private FileService _fileService;
        private MainWindowViewModel _mainWindowViewModel;
        public string? DialogIdentifier { get; set; }
        private bool WHisLocked = false;
        private double originalWidth;
        private double originalHeight;
        private TextBox? currentlyEditedTextBox;
        private void WidthEntry_GotFocus(object sender, RoutedEventArgs e)
        {
            currentlyEditedTextBox = WidthEntry;
        }

        private void WidthEntry_LostFocus(object sender, RoutedEventArgs e)
        {
            currentlyEditedTextBox = null;
        }

        private void HeightEntry_GotFocus(object sender, RoutedEventArgs e)
        {
            currentlyEditedTextBox = HeightEntry;
        }

        private void HeightEntry_LostFocus(object sender, RoutedEventArgs e)
        {
            currentlyEditedTextBox = null;
        }

        List<string> formats = new List<string> { "MP4", "MOV", "PNG-SEQ", "SVG-SEQ" };
        List<string> spans = new List<string> { "Entire File", "Work Range", "Specified Frame Range"};

        public MainVideoRender()
        {
            _fileService = new FileService(((IClassicDesktopStyleApplicationLifetime)App.Current!.ApplicationLifetime!).MainWindow!);
            AvaloniaXamlLoader.Load(this);
            FormatCombobox.ItemsSource = formats;
            FormatCombobox.SelectedIndex = 0;

            WidthEntry.TextChanged += WidthEntry_TextChanged;
            HeightEntry.TextChanged += HeightEntry_TextChanged;

            var _viewModelRegistry = ViewModelRegistry.Instance;
            _mainWindowViewModel = (MainWindowViewModel)_viewModelRegistry.GetViewModel(nameof(MainWindowViewModel));
            WidthEntry.Text = _mainWindowViewModel.MainDocument!.Width.ToString();
            HeightEntry.Text = _mainWindowViewModel.MainDocument!.Height.ToString();;

            var scenes = _mainWindowViewModel.MainDocument.Timelines;
            foreach (var scene in scenes) { spans.Add(scene.Name); }
            SpanCombobox.ItemsSource = spans;
            SpanCombobox.SelectedIndex = 0;

            ThreadCount.Maximum = Environment.ProcessorCount - 1;
            OutputPath.Text = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos) + "\\" + Path.GetFileNameWithoutExtension(_mainWindowViewModel.MainDocument!.Filename) + ".mp4";
        }

        private void WidthEntry_TextChanged(object? sender, EventArgs e)
        {
            if (WHisLocked && currentlyEditedTextBox != WidthEntry)
            {
                // Update HeightEntry based on WidthEntry
                int width;
                if (int.TryParse(WidthEntry.Text, out width))
                {
                    int height = (int)((double)width * (originalHeight / originalWidth));
                    HeightEntry.Text = height.ToString();
                }
            }
        }

        private void HeightEntry_TextChanged(object? sender, EventArgs e)
        {
            if (WHisLocked && currentlyEditedTextBox != HeightEntry)
            {
                // Update WidthEntry based on HeightEntry
                int height;
                if (int.TryParse(HeightEntry.Text, out height))
                {
                    int width = (int)((double)height * (originalWidth / originalHeight));
                    WidthEntry.Text = width.ToString();
                }
            }
        }

        private void WHlock_Click(object sender, RoutedEventArgs e)
        {
            WHisLocked = !WHisLocked;
            if (WHisLocked)
            {
                int width;
                int height;
                if (int.TryParse(WidthEntry.Text, out width) && int.TryParse(HeightEntry.Text, out height))
                {
                    originalWidth = width;
                    originalHeight = height;
                }
            }
            LockIcon.Data = WHisLocked ? (Geometry)App.Current!.Resources["ico_men_unlock"]! : (Geometry)App.Current!.Resources["ico_men_lock"]!;
        }

        private async void OpenSavePicker_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = ((IClassicDesktopStyleApplicationLifetime)App.Current!.ApplicationLifetime!).MainWindow!;
            var filePath = await _fileService.ExportFileAsync(mainWindow, ".mp4");
        }
        
        private void OkayButton_Click(object sender, RoutedEventArgs e)
        {
            DialogHost.Close(DialogIdentifier);
        }
    }
}