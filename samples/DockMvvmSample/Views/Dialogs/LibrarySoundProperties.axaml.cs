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

namespace Blitz.Views
{
    public partial class LibrarySoundProperties : UserControl
    {
        private EventAggregator _eventAggregator;
        private AudioService _audioService;
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
            _audioService = AudioService.Instance;
            _libraryViewModel = (LibraryViewModel)_libraryViewModelRegistry.GetViewModel(nameof(LibraryViewModel));
            _mainWindowViewModel = (MainWindowViewModel)_libraryViewModelRegistry.GetViewModel(nameof(MainWindowViewModel));
            _eventAggregator = EventAggregator.Instance;
            _workingCsXFLDoc = CsXFL.An.GetActiveDocument();
            _audioData = audioData;
            _soundItem = item as CsXFL.SoundItem;
            SoundInfoDisplay.Text = _soundItem.Format + " " + Math.Round(_soundItem.Duration, 2) + "s ";
        }

        private void OnCanvasPaint(object sender, SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            SKPicture? _cachedWaveformPicture = null;
            if (_soundItem != null)
            {
                canvas.ResetMatrix();

                var fileExtension = Path.GetExtension(_libraryViewModel.Sound.Href)?.TrimStart('.').ToLower() ?? string.Empty;

                if (fileExtension == "wav" || fileExtension == "flac")
                {
                    var amplitudes = _audioService.GetAudioAmplitudes(_audioData, 16, 1);
                    (_cachedWaveformPicture, _, _) = _audioService.GenerateWaveform(amplitudes, 800, 200, _libraryViewModel.CanvasColor!);
                }
                else if (fileExtension == "mp3")
                {
                    var pcmData = _audioService.DecodeMp3ToWav(_audioData);
                    var amplitudes = _audioService.GetAudioAmplitudes(pcmData, 16, 1);
                    (_cachedWaveformPicture, _, _) = _audioService.GenerateWaveform(amplitudes, 800, 200, _libraryViewModel.CanvasColor!);
                }

                if (_cachedWaveformPicture != null) // Ensure the picture is not null
                {
                    var canvasWidth = e.Info.Width;
                    var canvasHeight = e.Info.Height;
                    var offsetX = (canvasWidth - 800) / 2f;
                    var offsetY = (canvasHeight - 200) / 2f;

                    canvas.Save();
                    canvas.Translate(offsetX, offsetY);
                    canvas.DrawPicture(_cachedWaveformPicture);
                    canvas.Restore();
                }
                else
                {
                    Console.WriteLine("Failed to generate waveform picture.");
                }
            }
        }

        private void OkayButton_Click(object sender, RoutedEventArgs e)
        {
            DialogHost.Close(DialogIdentifier);
        }
    }
}