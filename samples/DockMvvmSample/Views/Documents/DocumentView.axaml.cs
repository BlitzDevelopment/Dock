using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System.Threading.Tasks;
using Avalonia.Animation;
using System;
using Avalonia.Styling;
using Blitz.ViewModels.Documents;
using Blitz.Events;
using CsXFL;
using System.IO;
using Avalonia.Threading;

namespace Blitz.Views.Documents;

public partial class DocumentView : UserControl
{
    private readonly DocumentViewModel _documentViewModel;
    private CsXFL.Document _workingCsXFLDocument;
    public DocumentView()
    {
        InitializeComponent();

        App.EventAggregator.Subscribe<DocumentFlyoutRequestedEvent>(OnDocumentFlyoutRequested);
        App.EventAggregator.Subscribe<DocumentProgressChangedEvent>(OnDocumentProgressChanged);

        SetProgressRingState(true);
        Task.Run(async () =>
            {
                await Task.Delay(5000);
                await Dispatcher.UIThread.InvokeAsync(() => SetProgressRingState(false));
            });
        ShowFlyoutAsync("Loaded " + Path.GetFileName(An.GetActiveDocument().Filename)).ConfigureAwait(false);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public async void SetProgressRingState(bool isActive)
    {
        if (ProgressRingControl != null)
        {
            // Create an animation for fading opacity
            var animation = new Animation
            {
                Duration = TimeSpan.FromSeconds(0.5),
                Children =
                {
                    new KeyFrame
                    {
                        Cue = new Cue(0),
                        Setters =
                        {
                            new Setter(Control.OpacityProperty, isActive ? 0 : 1) // Start opacity
                        }
                    },
                    new KeyFrame
                    {
                        Cue = new Cue(1),
                        Setters =
                        {
                            new Setter(Control.OpacityProperty, isActive ? 1 : 0) // End opacity
                        }
                    }
                }
            };

            // Run the animation asynchronously
            await animation.RunAsync(ProgressRingControl);

            // Toggle visibility after the animation completes
            ProgressRingControl.IsVisible = isActive;
        }
    }

    private void OnDocumentFlyoutRequested(DocumentFlyoutRequestedEvent e)
    {
        if (e.Document == _documentViewModel)
        {
            ShowFlyoutAsync(e.FlyoutMessage).ConfigureAwait(false);
        }
    }

    private void OnDocumentProgressChanged(DocumentProgressChangedEvent e)
    {
        if (e.Document == _documentViewModel)
        {
            SetProgressRingState(e.IsInProgress);
        }
    }

    public async Task ShowFlyoutAsync(string message, int durationInMilliseconds = 3000)
    {
        FlyoutText.Text = message;
        FlyoutContainer.IsVisible = true;
        await Task.Delay(durationInMilliseconds);
        FlyoutContainer.IsVisible = false;
    }
}