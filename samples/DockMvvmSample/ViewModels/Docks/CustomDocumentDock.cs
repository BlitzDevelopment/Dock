using Blitz.ViewModels.Documents;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;
using System.IO;
using System;
using Dock.Model.Core;
using Dock.Model.Core.Events;

namespace Blitz.ViewModels.Docks;

public class CustomDocumentDock : DocumentDock
{
    private MainWindowViewModel _mainWindowViewModel;

    public CustomDocumentDock(MainWindowViewModel mainWindowViewModel)
    {
        _mainWindowViewModel = mainWindowViewModel ?? throw new ArgumentNullException(nameof(mainWindowViewModel));
        CreateDocument = new RelayCommand(CreateNewDocument);
        _mainWindowViewModel.DocumentOpened += OnDocumentOpened;
    }

    private void CreateNewDocument()
    {
        if (!CanCreateDocument)
        {
            return;
        }

        var index = VisibleDockables?.Count + 1;
        var document = new DocumentViewModel {Id = $"Document{index}", Title = $"Document{index}"};

        Factory?.AddDockable(this, document);
        Factory?.SetActiveDockable(document);
        Factory?.SetFocusedDockable(this, document);
    }

    private void OnDocumentOpened(CsXFL.Document document)
    {
        // Create a new DocumentViewModel based on the opened document
        var documentViewModel = new DocumentViewModel
        {
            Id = document.Filename,
            Title = Path.GetFileName(document.Filename),
            AttachedDocument = document
        };

        // Add the new document to the DocumentDock
        Factory?.AddDockable(this, documentViewModel);
        Factory?.SetActiveDockable(documentViewModel);
        Factory?.SetFocusedDockable(this, documentViewModel);
    }
}