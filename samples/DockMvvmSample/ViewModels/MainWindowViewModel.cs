using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Blitz.Events;
using Blitz.Models;
using Blitz.ViewModels.Documents;
using Blitz.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CsXFL;
using DialogHostAvalonia;
using Dock.Model.Controls;
using Dock.Model.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Input;
using System.Threading.Tasks;

namespace Blitz.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    #region Recent Files
    public MenuItem OpenRecentMenuItem { get; set; }
    private string recentFilesPath;
    public ObservableCollection<string> RecentFiles { get; set; } = new ObservableCollection<string>();
    public ICommand OpenRecentCommand { get; }
    #endregion

    #region Commands
    private ICommand _newDocumentCommand;
    public ICommand NewDocumentCommand => _newDocumentCommand;
    private ICommand _openDocumentCommand;
    public ICommand OpenDocumentCommand => _openDocumentCommand;
    private ICommand _saveDocumentCommand;
    public ICommand SaveDocumentCommand => _saveDocumentCommand;

    private ICommand _renderVideoDialogCommand;
    public ICommand RenderVideoDialogCommand => _renderVideoDialogCommand;
    private ICommand _preferencesCommand;
    public ICommand PreferencesCommand => _preferencesCommand;

    private ICommand _importToLibraryCommand;
    public ICommand ImportToLibraryCommand => _importToLibraryCommand;
    #endregion

    #region Events
    public event Action<int>? DocumentOpened;
    #endregion

    #region Document State
    private DocumentViewModel? _workingCsXFLDocViewModel;
    private CsXFL.Document? _workingCsXFLDoc;
    public CsXFL.Document? WorkingCsXFLDoc
    {
        get => _workingCsXFLDoc;
        private set
        {
            if (_workingCsXFLDoc != value)
            {
                _workingCsXFLDoc = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsDocumentLoaded));
            }
        }
    }

    public bool IsDocumentLoaded => WorkingCsXFLDoc != null;
    #endregion

    // MARK: Document Commands
    private void NewDocument()
    {
        WorkingCsXFLDoc = An.CreateDocument(Path.Combine(App.BlitzAppData.GetTmpFolder(), "Untitled.xfl"));
    }

    private async void OpenDocument()
    {
        var mainWindow = ((IClassicDesktopStyleApplicationLifetime)App.Current!.ApplicationLifetime!).MainWindow!;
        var filePath = await App.FileService.OpenFileAsync(mainWindow, FileService.BlitzCompatible, "Open Document");
        await OpenDocumentHelper(filePath);
    }

    private async Task OpenDocumentHelper(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            Debug.WriteLine($"Warning: File {filePath} does not exist or is null.");
            return;
        }
        WorkingCsXFLDoc = await An.OpenDocumentAsync(filePath);
        AddToRecentFiles(filePath);

        // Get the document list and find the index of the opened document
        var documentList = An.GetDocumentList();
        var documentIndex = documentList.IndexOf(WorkingCsXFLDoc);

        if (documentIndex == -1)
        {
            Debug.WriteLine($"Error: Opened document {filePath} not found in the document list.");
            return;
        }

        // Invoke the event with the index of the opened document
        DocumentOpened?.Invoke(documentIndex);
    }

    private void SaveDocument()
    {
        _workingCsXFLDocViewModel!.Dispose();
        WorkingCsXFLDoc!.Save();
        _workingCsXFLDocViewModel.InitializeZipArchive();
    }

    private async void OpenRecent(string filePath)
    {
        await OpenDocumentHelper(filePath!);
    }

    public void LoadRecentFiles()
    {
        RecentFiles.Clear();
        OpenRecentMenuItem.Items.Clear();

        if (File.Exists(recentFilesPath))
        {
            var recentFiles = File.ReadAllLines(recentFilesPath);
            foreach (var file in recentFiles)
            {
                RecentFiles.Add(file);
                var menuItem = new MenuItem
                {
                    Header = Path.GetFileName(file).Replace("_", "__"), // Escape underscores
                    Command = OpenRecentCommand,
                    CommandParameter = file // Pass the file path as the command parameter
                };
                OpenRecentMenuItem.Items.Add(menuItem);
            }
        }
    }

    private void AddToRecentFiles(string filePath)
    {
        List<string> recentFiles = new List<string>();

        if (File.Exists(recentFilesPath))
        {
            recentFiles = File.ReadAllLines(recentFilesPath).ToList();
        }

        if (recentFiles.Contains(filePath))
        {
            recentFiles.Remove(filePath);
        }

        recentFiles.Insert(0, filePath);

        if (recentFiles.Count > 6)
        {
            recentFiles = recentFiles.Take(6).ToList();
        }

        File.WriteAllLines(recentFilesPath, recentFiles);

        // Update RecentFiles collection and OpenRecentMenuItem
        LoadRecentFiles();
    }

    // MARK: Importing
    private async void ImportToLibrary() 
    {
        if (WorkingCsXFLDoc is null) { return; }
        var mainWindow = ((IClassicDesktopStyleApplicationLifetime)App.Current!.ApplicationLifetime!).MainWindow!;
        var filePath = await App.FileService.OpenFileAsync(mainWindow, FileService.BitmapCompatible, "Import to Library");
        WorkingCsXFLDoc.ImportFile(filePath);
        App.EventAggregator.Publish(new LibraryItemsChangedEvent());
    }

    // MARK: Dialogs
    public async void Preferences()
    {
        var dialog = new MainPreferences();
        var dialogIdentifier = await DialogHost.Show(dialog) as string;
        dialog.DialogIdentifier = dialogIdentifier!;
    }

    public async void RenderVideoDialog()
    {
        var dialog = new MainVideoRender();
        var dialogIdentifier = await DialogHost.Show(dialog) as string;
        dialog.DialogIdentifier = dialogIdentifier!;
    }

    // MARK: Events
    private void OnActiveDocumentChanged(ActiveDocumentChangedEvent activeDocumentChangedEvent)
    {
        WorkingCsXFLDoc = CsXFL.An.GetDocument(activeDocumentChangedEvent.Document.DocumentIndex);
        _workingCsXFLDocViewModel = activeDocumentChangedEvent.Document;
    }

    public MainWindowViewModel()
    {
        OpenRecentMenuItem = new MenuItem();
        _factory = new DockFactory(this, string.Empty);

        App.EventAggregator.Subscribe<ActiveDocumentChangedEvent>(OnActiveDocumentChanged);

        _newDocumentCommand = new RelayCommand(NewDocument);
        _openDocumentCommand = new RelayCommand(OpenDocument);
        _saveDocumentCommand = new RelayCommand(SaveDocument);
        _renderVideoDialogCommand = new RelayCommand(RenderVideoDialog);
        _importToLibraryCommand = new RelayCommand(ImportToLibrary);
        _preferencesCommand = new RelayCommand(Preferences);

        recentFilesPath = App.BlitzAppData.GetRecentFilesPath();
        OpenRecentCommand = new RelayCommand<string>(OpenRecent!);

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

    // MARK: Dock Base
    private readonly IFactory? _factory;
    private IRootDock? _layout;

    public IRootDock? Layout
    {
        get => _layout;
        set => SetProperty(ref _layout, value);
    }

    public ICommand NewLayout { get; }

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
