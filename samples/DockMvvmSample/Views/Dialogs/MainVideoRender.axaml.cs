using DialogHostAvalonia;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Interactivity;
using Blitz.ViewModels;
using Avalonia.Media;
using Avalonia.Controls.ApplicationLifetimes;
using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Rendering;
using System.Threading.Tasks;
using CsXFL;

namespace Blitz.Views
{
    public partial class MainVideoRender : UserControl
    { 
        #region Events
        public event Action<int>? ProgressUpdated;
        #endregion

        #region Dependencies
        private FileService _fileService;
        private BlitzAppData _blitzAppData;
        private MainWindowViewModel _mainWindowViewModel;
        #endregion

        #region Document State
        private Document WorkingCsXFLDoc;
        #endregion

        #region Properties
        public string? DialogIdentifier { get; set; }
        private string? SelectedFormat { get; set; }
        private string? Encoding { get; set; }
        #endregion

        #region UI State
        private bool WHisLocked = false;
        private double originalWidth;
        private double originalHeight;
        #endregion

        #region Data
        private List<string> formats = new List<string> { "MP4", "MOV", "PNG-SEQ", "SVG-SEQ" };
        private List<string> spans = new List<string> { "Entire File", "Work Range", "Specified Frame Range" };
        #endregion

        public MainVideoRender()
        {
            _blitzAppData = new BlitzAppData();
            _fileService = new FileService(((IClassicDesktopStyleApplicationLifetime)App.Current!.ApplicationLifetime!).MainWindow!);

            AvaloniaXamlLoader.Load(this);
            FormatCombobox.ItemsSource = formats;
            FormatCombobox.SelectedIndex = 0;
            WorkingCsXFLDoc = CsXFL.An.GetActiveDocument();

            WidthEntry.ValueChanged += WidthEntry_TextChanged;
            HeightEntry.ValueChanged += HeightEntry_TextChanged;

            var _viewModelRegistry = ViewModelRegistry.Instance;
            _mainWindowViewModel = (MainWindowViewModel)_viewModelRegistry.GetViewModel(nameof(MainWindowViewModel));
            WidthEntry.Value = WorkingCsXFLDoc.Width;
            HeightEntry.Value = WorkingCsXFLDoc.Height;

            var scenes = WorkingCsXFLDoc!.Timelines;
            foreach (var scene in scenes) { spans.Add(scene.Name); }
            SpanCombobox.ItemsSource = spans;
            SpanCombobox.SelectedIndex = 0;

            Encoding = (EncodingSource.SelectedItem as ComboBoxItem)?.Content!.ToString();
            EncodingSource.SelectionChanged += EncodingSource_SelectionChanged!;

            // Initialize thread count
            int cpuThreadCount = Environment.ProcessorCount;
            ThreadCount.Maximum = 2 * cpuThreadCount;
            ThreadCount.Value = cpuThreadCount / 2;
            ThreadCount.ValueChanged += ThreadCount_ValueChanged;
            ThreadCount_ValueChanged(ThreadCount, null!);

            OutputPath.Text = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos) + "\\" + Path.GetFileNameWithoutExtension(WorkingCsXFLDoc.Filename) + ".mp4";
        }

        private void WidthEntry_TextChanged(object? sender, EventArgs e)
        {
            if (WHisLocked)
            {
                // Update HeightEntry based on WidthEntry
                int width;
                if (int.TryParse(WidthEntry.Text, out width))
                {
                    int height = (int)((double)width * (originalHeight / originalWidth));
                    HeightEntry.Value = height;
                }
            }
        }

        private void HeightEntry_TextChanged(object? sender, EventArgs e)
        {
            if (WHisLocked)
            {
                // Update WidthEntry based on HeightEntry
                int height;
                if (int.TryParse(HeightEntry.Text, out height))
                {
                    int width = (int)((double)height * (originalWidth / originalHeight));
                    WidthEntry.Value = width;
                }
            }
        }

        private void EncodingSource_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Encoding = (EncodingSource.SelectedItem as ComboBoxItem)?.Content!.ToString();        
        }

        private void ThreadCount_ValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
        {
            if (sender is NumericUpDown numericUpDown)
            {
                numericUpDown.BorderThickness = new Thickness(3);
                int cpuThreadCount = Environment.ProcessorCount;
                int halfCpuThreadCount = cpuThreadCount / 2;
                int value = (int)numericUpDown.Value!;

                if (value <= halfCpuThreadCount)
                {
                    numericUpDown.BorderBrush = Brushes.Green;
                }
                else if (value > halfCpuThreadCount && value < cpuThreadCount)
                {
                    numericUpDown.BorderBrush = Brushes.Yellow;
                }
                else if (value == cpuThreadCount)
                {
                    numericUpDown.BorderBrush = Brushes.Red;
                }
                else
                {
                    numericUpDown.BorderBrush = Brushes.Purple;
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
            var filePath = await _fileService.ExportFileAsync(mainWindow, SelectedFormat!);
            if (filePath == null) {return;}
            filePath = filePath.Replace("/", "\\");
            filePath = Regex.Replace(filePath, @"\.\w+$", $".{SelectedFormat}");
            OutputPath.Text = filePath;
        }

        private void FormatCombobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string selectedFormat = FormatCombobox.SelectedItem!.ToString()!;
            SelectedFormat = selectedFormat.Replace("-SEQ", "").ToLower();
            UpdateOutputFileFormat();
        }

        private void UpdateOutputFileFormat()
        {
            if (FormatCombobox.SelectedItem != null)
            {
                string outputFile = OutputPath.Text!;
                if (!string.IsNullOrEmpty(outputFile))
                {
                    // Use a regular expression to replace any file extension
                    outputFile = Regex.Replace(outputFile, @"\.\w+$", $".{SelectedFormat}");
                    OutputPath.Text = outputFile;
                }
            }
        }
        
        private void ShowFlyoutWarning(string message)
        {
            Flyout flyout = new Flyout();
            flyout.Content = new TextBlock { Text = message };
            flyout.ShowAt(OutputPath);

            // Dismiss the Flyout after 3 seconds
            Task.Delay(3000).ContinueWith(_ => 
            {
                flyout.Hide();
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void OkayButton_Click(object sender, RoutedEventArgs e)
        {
            string directoryPath = Path.GetDirectoryName(OutputPath.Text!)! + "\\";
            if (!Directory.Exists(directoryPath))
            {
                ShowFlyoutWarning("The output folder does not exist.");
                return;
            }

            try
            {
                string testFilePath = Path.Combine(directoryPath, Path.GetRandomFileName());
                using (FileStream fs = File.Create(testFilePath, 1, FileOptions.DeleteOnClose))
                {
                }
            }
            catch (UnauthorizedAccessException)
            {
                ShowFlyoutWarning("The output folder is not writable.");
                return;
            }

            DialogHost.Close(DialogIdentifier);
            var dialog = new MainRenderProgress();
            var dialogIdentifier = DialogHost.Show(dialog);
            HandleRendering();
        }

        private void HandleRendering()
        {
            string baseDirectory = AppContext.BaseDirectory;
            string threeDirectoriesUp = Path.GetFullPath(Path.Combine(baseDirectory, @"..\..\.."));
            string ffmpegPath = Path.Combine(threeDirectoriesUp, "dlls", "ffmpeg.exe");
            string directoryPath = Path.GetDirectoryName(OutputPath.Text!)! + "\\";
            string fileName = Path.GetFileName(OutputPath.Text!)!;
            string appDataFolder = _blitzAppData.GetTmpFolder();

            int totalFrames = 0;
            foreach (var timeline in WorkingCsXFLDoc.Timelines) { totalFrames += timeline.GetFrameCount(); }
            int maxDigits = totalFrames.ToString().Length;
            string ImageSequenceOutputFolder = Path.Combine(directoryPath, Path.GetFileNameWithoutExtension(fileName));

            // Canvas background is not available
            // PNG span boilerplate
            if (SelectedFormat != "svg" && SelectedFormat != "png") {
                // Render Videos
                var RenMan = new RenderingManager(WorkingCsXFLDoc, (int)ThreadCount.Value!, directoryPath, appDataFolder, ffmpegPath, true);
                // RenMan.ProgressUpdated += (progress) => { ProgressUpdated?.Invoke(progress); };
                bool isMov = SelectedFormat == "mov";
                string ffmpegEncoding = isMov ? "png" : (Encoding == "GPU" ? "h264_nvec" : "h264");
                string ffmpegPixelFormat = isMov ? "rgba" : "yuv420p";
                string ffmpegArgsBeforeinput = Encoding == "CPU" 
                    ? $"-y -framerate {WorkingCsXFLDoc.FrameRate}" 
                    : $"-y -hwaccel_device 0 -hwaccel_output_format cuda -hwaccel cuda -framerate {WorkingCsXFLDoc.FrameRate}";
                string ffmpegArgsAfterinput = $"-c:v {(ffmpegEncoding)} -preset fast -b:v 10M -pix_fmt {(ffmpegPixelFormat)} -f {SelectedFormat} -s {WidthEntry.Text}x{HeightEntry.Text}";
                if(InMemOnly.IsChecked == true) {
                    RenMan.RenderDocumentWithPipes(fileName, ffmpegArgsBeforeinput, ffmpegArgsAfterinput);
                } else {
                    RenMan.RenderDocumentWithTmpFiles(fileName, ffmpegArgsBeforeinput, ffmpegArgsAfterinput);
                }
            } else if (SelectedFormat == "png") {
                // Render PNG Sequence
                if (!Directory.Exists(ImageSequenceOutputFolder)) { Directory.CreateDirectory(ImageSequenceOutputFolder); }
                var RenMan = new RenderingManager(WorkingCsXFLDoc, (int)ThreadCount.Value!, directoryPath, appDataFolder, ffmpegPath, true);
                string outputPattern = Path.Combine(ImageSequenceOutputFolder, Path.GetFileNameWithoutExtension(fileName) + "_%0" + maxDigits + "d.png");

                string ffmpegEncoding = Encoding == "GPU" ? "h264_nvec" : "h264";
                string ffmpegArgsBeforeinput = Encoding == "CPU"
                    ? $"-y" 
                    : $"-y -hwaccel_device 0 -hwaccel_output_format cuda -hwaccel cuda";
                string ffmpegArgsAfterinput = $"-c:v png -preset fast -vf \"scale={WidthEntry.Text}:{HeightEntry.Text}\" -start_number 0 {outputPattern}";
                if(InMemOnly.IsChecked == true) {
                    RenMan.RenderDocumentWithPipes(outputPattern, ffmpegArgsBeforeinput, ffmpegArgsAfterinput);
                } else {
                    RenMan.RenderDocumentWithTmpFiles(outputPattern, ffmpegArgsBeforeinput, ffmpegArgsAfterinput);
                }
            } else {
                // Render SVG Sequence
                if (!Directory.Exists(ImageSequenceOutputFolder)) { Directory.CreateDirectory(ImageSequenceOutputFolder); }
                var SVGRen = new SVGRenderer(WorkingCsXFLDoc, appDataFolder, true);

                string selectedSpan = SpanCombobox.SelectedItem!.ToString()!;
                if (selectedSpan == "Entire File")
                {
                    for (int timelineIndex = 0; timelineIndex < WorkingCsXFLDoc.Timelines.Count; timelineIndex++)
                    {
                        for (int frameIndex = 0; frameIndex < WorkingCsXFLDoc!.Timelines[timelineIndex].GetFrameCount(); frameIndex++)
                        {
                            var OutputSVG = SVGRen.Render(0, frameIndex, int.Parse(WidthEntry.Text!), int.Parse(WidthEntry.Text!));
                            string filename = $"{Path.GetFileNameWithoutExtension(fileName)}_{frameIndex.ToString().PadLeft(maxDigits, '0')}.svg";
                            string svgFilePath = Path.Combine(ImageSequenceOutputFolder, filename);
                            OutputSVG.Save(svgFilePath);
                        }
                    }
                }
                else if (selectedSpan == "Specified Frame Range")
                {
                    throw new NotImplementedException("Specified Frame Range handling is not yet implemented.");
                    // TODO: Revisit the axaml panel and add some logic for when Specified Frame Range is selected, enable some NumberUpDowns for the user to specify the range
                    // This will apply to the currentTimeline, which is yet to be implemented.
                }
                else if (selectedSpan == "Work Range")
                {
                    throw new NotImplementedException("Work Range handling is not yet implemented.");
                    // TODO: This will require a work range to be defined in our timeline panel, which does not yet exist.
                }
                else
                {
                    for (int timelineIndex = 0; timelineIndex < WorkingCsXFLDoc.Timelines.Count; timelineIndex++)
                    {
                        var timeline = WorkingCsXFLDoc.Timelines[timelineIndex];
                        if (selectedSpan == timeline.Name)
                        {
                            for (int frameIndex = 0; frameIndex < timeline.GetFrameCount(); frameIndex++)
                            {
                                var OutputSVG = SVGRen.Render(0, frameIndex, int.Parse(WidthEntry.Text!), int.Parse(WidthEntry.Text!));
                                string filename = $"{Path.GetFileNameWithoutExtension(fileName)}_{frameIndex.ToString().PadLeft(maxDigits, '0')}.svg";
                                string svgFilePath = Path.Combine(ImageSequenceOutputFolder, filename);
                                OutputSVG.Save(svgFilePath);
                            }
                            break;
                        }
                    }
                }
            }
        }
    }
}