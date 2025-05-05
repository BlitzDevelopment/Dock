using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Linq;
using Blitz.ViewModels.Tools;
using Avalonia.Markup.Xaml;
using DialogHostAvalonia;
using Blitz.ViewModels;
using Blitz.Events;
using CommunityToolkit.Mvvm.ComponentModel;
using CsXFL;
using System;
using SkiaSharp;
using System.IO;
using Serilog;

namespace Blitz.Views
{
    public partial class LibrarySoundProperties : UserControl
    {
        private LibraryViewModel _libraryViewModel;
        private MainWindowViewModel _mainWindowViewModel;
        private CsXFL.Document _workingCsXFLDoc;
        private CsXFL.SoundItem _soundItem;
        private byte[] _audioData;
        public string? DialogIdentifier { get; set; }

        public LibrarySoundProperties(CsXFL.Item item, byte[] audioData)
        {
            AvaloniaXamlLoader.Load(this);
            DataContext = this;
            var _libraryViewModelRegistry = ViewModelRegistry.Instance;

            _libraryViewModel = (LibraryViewModel)_libraryViewModelRegistry.GetViewModel(nameof(LibraryViewModel));
            _mainWindowViewModel = (MainWindowViewModel)_libraryViewModelRegistry.GetViewModel(nameof(MainWindowViewModel));
            _workingCsXFLDoc = CsXFL.An.GetActiveDocument();
            _audioData = audioData;
            _soundItem = item as CsXFL.SoundItem ?? throw new ArgumentNullException(nameof(item), "Item must be a SoundItem.");

            var size = audioData.Length >= 1024 * 1024
                ? $"{audioData.Length / (1024.0 * 1024.0):F2} MB"
                : audioData.Length >= 1024
                    ? $"{audioData.Length / 1024.0:F2} KB"
                    : $"{audioData.Length} bytes";

            SoundInfoDisplay.Text = _soundItem.Format + " " + Math.Round(_soundItem.Duration, 2) + "s " + size;
            SetTextBoxText();
        }

        private void OnCanvasPaint(object sender, SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            SKPicture? _cachedWaveformPicture = null;
            if (_soundItem != null)
            {
                canvas.ResetMatrix();

                var fileExtension = _libraryViewModel.Sound?.Href != null
                    ? Path.GetExtension(_libraryViewModel.Sound.Href)?.TrimStart('.').ToLower()
                    : string.Empty;

                if (fileExtension == "wav" || fileExtension == "flac")
                {
                    var amplitudes = App.AudioService.GetAudioAmplitudes(_audioData, 16, 1);
                    (_cachedWaveformPicture, _, _) = App.AudioService.GenerateWaveform(amplitudes, 800, 200, _libraryViewModel.CanvasColor!);
                }
                else if (fileExtension == "mp3")
                {
                    var pcmData = App.AudioService.DecodeMp3ToWav(_audioData);
                    var amplitudes = App.AudioService.GetAudioAmplitudes(pcmData, 16, 1);
                    (_cachedWaveformPicture, _, _) = App.AudioService.GenerateWaveform(amplitudes, 800, 200, _libraryViewModel.CanvasColor!);
                }
                if (_cachedWaveformPicture != null) // Ensure the picture is not null
                {
                    var canvasWidth = e.Info.Width;
                    var canvasHeight = e.Info.Height;

                    // Calculate the horizontal scaling factor
                    var scaleX = canvasWidth / 800f;

                    // Center vertically
                    var offsetY = (canvasHeight - (200 * scaleX)) / 2f;

                    canvas.Save();
                    canvas.Scale(scaleX, scaleX); // Apply horizontal scaling
                    canvas.Translate(0, offsetY / scaleX); // Adjust vertical offset after scaling
                    canvas.DrawPicture(_cachedWaveformPicture);
                    canvas.Restore();
                }
                else
                {
                    Log.Error("Failed to generate waveform picture.");
                }
            }
        }

        private void SetTextBoxText()
        {
            string path = _libraryViewModel.UserLibrarySelection!.FirstOrDefault()?.Name!;
            int lastIndex = path.LastIndexOf('/');
            string fileName = lastIndex != -1 ? path.Substring(lastIndex + 1) : path;
            InputRename.Text = fileName;
        }

        private void OkayButton_Click(object sender, RoutedEventArgs e)
        {
            var result = new
            {
                Name = InputRename.Text
            };
            DialogHost.Close(DialogIdentifier, result);
        }
    }
}