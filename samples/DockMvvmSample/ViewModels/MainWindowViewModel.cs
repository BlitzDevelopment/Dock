using System.Diagnostics;
using System.Windows.Input;
using System.Threading.Tasks;
using DockMvvmSample.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Controls;
using Dock.Model.Core;
using System;
using System.Collections.Generic;
using System.IO;
using CsXFL;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using DockMvvmSample.Views;
using AutoMapper;
using DialogHostAvalonia;

namespace DockMvvmSample.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IFileService _fileService;

    // MARK: Top Level Stuff
    [ObservableProperty]
    public CsXFL.Document? _mainDocument;

    private ICommand _openDocumentCommand;
    public ICommand OpenDocumentCommand => _openDocumentCommand;
    public bool IsDocumentLoaded => MainDocument != null;
    private ICommand _saveDocumentCommand;
    public ICommand SaveDocumentCommand => _saveDocumentCommand;
    private ICommand _renderVideoDialogCommand;
    public ICommand RenderVideoDialogCommand => _renderVideoDialogCommand;

    partial void OnMainDocumentChanged(CsXFL.Document? value)
    {
        OnPropertyChanged(nameof(IsDocumentLoaded));
    }

    private static CsXFL.Document CloneDocument(CsXFL.Document document)
    {
        var config = new MapperConfiguration(cfg => cfg.CreateMap<CsXFL.Document, CsXFL.Document>());
        var mapper = config.CreateMapper();
        return mapper.Map<CsXFL.Document>(document);
    }

    public class CsXFLDocumentMemento : IMemento
    {
        private CsXFL.Document _document;
        private MainWindowViewModel _viewModel;

        public CsXFLDocumentMemento(MainWindowViewModel viewModel, CsXFL.Document document)
        {
            _viewModel = viewModel;
            _document = CloneDocument(document);
        }

        public string Description => $"Opened Document {_document.Filename}";

        public void Restore()
        {
            _viewModel.MainDocument = _document;
        }
    }

    private async void OpenDocument()
    {
        var mainWindow = ((IClassicDesktopStyleApplicationLifetime)App.Current!.ApplicationLifetime!).MainWindow!;
        var filePath = await _fileService.OpenFileAsync(mainWindow);
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            Debug.WriteLine($"Warning: File {filePath} does not exist or is null.");
            return;
        }
        MainDocument = await An.OpenDocumentAsync(filePath);
        var memento = new CsXFLDocumentMemento(this, MainDocument);
        ApplicationServices.MementoCaretaker.AddMemento(memento, $"You will never see this.");
    }

    private void SaveDocument()
    {
        MainDocument!.Save();
    }

    public async void RenderVideoDialog()
    {
        var dialog = new MainVideoRender();
        var dialogIdentifier = await DialogHost.Show(dialog) as string;
        dialog.DialogIdentifier = dialogIdentifier!;
    }

    // MARK: Dock Base
    private readonly IFactory? _factory;
    private IRootDock? _layout;

    public IRootDock? Layout
    {
        get => _layout;
        set => SetProperty(ref _layout, value);
    }

    public ICommand NewLayout { get; }

    public MainWindowViewModel()
    {
        _fileService = new FileService(((IClassicDesktopStyleApplicationLifetime)App.Current!.ApplicationLifetime!).MainWindow!);
        _factory = new DockFactory(this, new DemoData());

        // MARK: CsXFL Commands
        _openDocumentCommand = new RelayCommand(OpenDocument);
        _saveDocumentCommand = new RelayCommand(SaveDocument);
        _renderVideoDialogCommand = new RelayCommand(RenderVideoDialog);

        DebugFactoryEvents(_factory);

        Layout = _factory?.CreateLayout();
        if (Layout is { })
        {
            _factory?.InitLayout(Layout);
            if (Layout is { } root)
            {
                root.Navigate.Execute("Home");
            }
        }

        NewLayout = new RelayCommand(ResetLayout);
    }

    // MARK: Factory Debug
    private void DebugFactoryEvents(IFactory factory)
    {
        factory.ActiveDockableChanged += (_, args) =>
        {
            Debug.WriteLine($"[ActiveDockableChanged] Title='{args.Dockable?.Title}'");
        };

        factory.FocusedDockableChanged += (_, args) =>
        {
            Debug.WriteLine($"[FocusedDockableChanged] Title='{args.Dockable?.Title}'");
        };

        factory.DockableAdded += (_, args) =>
        {
            Debug.WriteLine($"[DockableAdded] Title='{args.Dockable?.Title}'");
        };

        factory.DockableRemoved += (_, args) =>
        {
            Debug.WriteLine($"[DockableRemoved] Title='{args.Dockable?.Title}'");
        };

        factory.DockableClosed += (_, args) =>
        {
            Debug.WriteLine($"[DockableClosed] Title='{args.Dockable?.Title}'");
        };

        factory.DockableMoved += (_, args) =>
        {
            Debug.WriteLine($"[DockableMoved] Title='{args.Dockable?.Title}'");
        };

        factory.DockableSwapped += (_, args) =>
        {
            Debug.WriteLine($"[DockableSwapped] Title='{args.Dockable?.Title}'");
        };

        factory.DockablePinned += (_, args) =>
        {
            Debug.WriteLine($"[DockablePinned] Title='{args.Dockable?.Title}'");
        };

        factory.DockableUnpinned += (_, args) =>
        {
            Debug.WriteLine($"[DockableUnpinned] Title='{args.Dockable?.Title}'");
        };

        factory.WindowOpened += (_, args) =>
        {
            Debug.WriteLine($"[WindowOpened] Title='{args.Window?.Title}'");
        };

        factory.WindowClosed += (_, args) =>
        {
            Debug.WriteLine($"[WindowClosed] Title='{args.Window?.Title}'");
        };

        factory.WindowClosing += (_, args) =>
        {
            // NOTE: Set to True to cancel window closing.
#if false
                args.Cancel = true;
#endif
            Debug.WriteLine($"[WindowClosing] Title='{args.Window?.Title}', Cancel={args.Cancel}");
        };

        factory.WindowAdded += (_, args) =>
        {
            Debug.WriteLine($"[WindowAdded] Title='{args.Window?.Title}'");
        };

        factory.WindowRemoved += (_, args) =>
        {
            Debug.WriteLine($"[WindowRemoved] Title='{args.Window?.Title}'");
        };

        factory.WindowMoveDragBegin += (_, args) =>
        {
            // NOTE: Set to True to cancel window dragging.
#if false
                args.Cancel = true;
#endif
            Debug.WriteLine($"[WindowMoveDragBegin] Title='{args.Window?.Title}', Cancel={args.Cancel}, X='{args.Window?.X}', Y='{args.Window?.Y}'");
        };

        factory.WindowMoveDrag += (_, args) =>
        {
            Debug.WriteLine($"[WindowMoveDrag] Title='{args.Window?.Title}', X='{args.Window?.X}', Y='{args.Window?.Y}");
        };

        factory.WindowMoveDragEnd += (_, args) =>
        {
            Debug.WriteLine($"[WindowMoveDragEnd] Title='{args.Window?.Title}', X='{args.Window?.X}', Y='{args.Window?.Y}");
        };
    }

    public void CloseLayout()
    {
        if (Layout is IDock dock)
        {
            if (dock.Close.CanExecute(null))
            {
                dock.Close.Execute(null);
            }
        }
    }

    public void ResetLayout()
    {
        if (Layout is not null)
        {
            if (Layout.Close.CanExecute(null))
            {
                Layout.Close.Execute(null);
            }
        }

        var layout = _factory?.CreateLayout();
        if (layout is not null)
        {
            Layout = layout;
            _factory?.InitLayout(layout);
        }
    }
}
