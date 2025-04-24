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

namespace Blitz.Views.Documents;

public partial class DocumentView : UserControl
{
    private readonly EventAggregator _eventAggregator;
    private readonly DocumentViewModel _documentViewModel;
    private CsXFL.Document _workingCsXFLDocument;
    public DocumentView()
    {
        InitializeComponent();
        _eventAggregator = EventAggregator.Instance;

        _eventAggregator.Subscribe<DocumentFlyoutRequestedEvent>(OnDocumentFlyoutRequested);
        _eventAggregator.Subscribe<DocumentProgressChangedEvent>(OnDocumentProgressChanged);

        SetProgressRingState(true);
        ShowFlyoutAsync("Loaded " + Path.GetFileName(An.GetActiveDocument().Filename)).ConfigureAwait(false);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public void SetProgressRingState(bool isActive)
    {
        if (ProgressRingControl != null)
        {
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