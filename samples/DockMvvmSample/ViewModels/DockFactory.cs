using System;
using System.Collections.Generic;
using System.IO;
using Blitz.Models.Tools;
using Blitz.ViewModels.Docks;
using Blitz.ViewModels.Documents;
using Blitz.ViewModels.Tools;
using Blitz.ViewModels.Views;
using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm;
using Dock.Model.Mvvm.Controls;
using Dock.Model.Core.Events;
using Blitz.Events;
using Serilog;

namespace Blitz.ViewModels;

public class DockFactory : Factory
{
    private readonly object _context;
    private readonly List<DocumentViewModel> OpenDocuments = new();
    private MainWindowViewModel _mainWindowViewModel;
    private IRootDock? _rootDock;
    private IDocumentDock? _documentDock;

    public DockFactory(MainWindowViewModel mainWindowViewModel, object context)
    {
        _context = context;
        _mainWindowViewModel = mainWindowViewModel;
        ViewModelRegistry.Instance.RegisterViewModel(nameof(MainWindowViewModel), _mainWindowViewModel);
        ActiveDockableChanged += OnActiveDockableChanged;
        DockableClosed += OnDockableClosed;
    }

    private void OnActiveDockableChanged(object? sender, ActiveDockableChangedEventArgs e)
    {

        // Switching active documents
        if (e.Dockable is DocumentViewModel document)
        {
            if (!OpenDocuments.Contains(document))
            {
                Log.Information($"Adding document {document.Title} to OpenDocuments list.");
                OpenDocuments.Add(document);
            }

            CsXFL.An.SetActiveDocument(CsXFL.An.GetDocument(document.DocumentIndex));
            App.EventAggregator.Publish(new ActiveDocumentChangedEvent(document));
        }
    }

    private void OnDockableClosed(object? sender, DockableClosedEventArgs e)
    {
        // Closed any document, can be non-focused
        if (e.Dockable is DocumentViewModel document)
        {
            // Remove the closed document from the list
            OpenDocuments.Remove(document);

            // Recalculate indices for remaining documents
            for (int i = 0; i < OpenDocuments.Count; i++)
            {
                OpenDocuments[i].DocumentIndex = i;
            }

            // Call CloseDocument with the updated index
            CsXFL.An.CloseDocument(document.DocumentIndex);
        }
    }

    public override IDocumentDock CreateDocumentDock() => new CustomDocumentDock(_mainWindowViewModel);

    public override IRootDock CreateLayout()
    {
        var Library = new LibraryViewModel() {Id = "Library", Title = "Library"};
        ViewModelRegistry.Instance.RegisterViewModel(nameof(LibraryViewModel), Library);
        var tool2 = new Tool2ViewModel {Id = "Tool2", Title = "Tool2"};
        var tool3 = new Tool3ViewModel {Id = "Tool3", Title = "Tool3"};
        var tool4 = new Tool4ViewModel {Id = "Tool4", Title = "Tool4"};
        var tool5 = new Tool5ViewModel {Id = "Tool5", Title = "Tool5"};
        var tool6 = new Tool6ViewModel {Id = "Tool6", Title = "Tool6", CanClose = true, CanPin = true};
        var tool7 = new Tool7ViewModel {Id = "Tool7", Title = "Tool7", CanClose = false, CanPin = false};
        var tool8 = new Tool8ViewModel {Id = "Tool8", Title = "Tool8", CanClose = false, CanPin = true};

        var leftDock = new ProportionalDock
        {
            Proportion = 0.25,
            Orientation = Orientation.Vertical,
            ActiveDockable = null,
            VisibleDockables = CreateList<IDockable>
            (
                new ToolDock
                {
                    ActiveDockable = Library,
                    VisibleDockables = CreateList<IDockable>(Library, tool2),
                    Alignment = Alignment.Left
                },
                new ProportionalDockSplitter(),
                new ToolDock
                {
                    ActiveDockable = tool3,
                    VisibleDockables = CreateList<IDockable>(tool3, tool4),
                    Alignment = Alignment.Bottom
                }
            )
        };

        var rightDock = new ProportionalDock
        {
            Proportion = 0.25,
            Orientation = Orientation.Vertical,
            ActiveDockable = null,
            VisibleDockables = CreateList<IDockable>
            (
                new ToolDock
                {
                    ActiveDockable = tool5,
                    VisibleDockables = CreateList<IDockable>(tool5, tool6),
                    Alignment = Alignment.Top,
                    GripMode = GripMode.Hidden
                },
                new ProportionalDockSplitter(),
                new ToolDock
                {
                    ActiveDockable = tool7,
                    VisibleDockables = CreateList<IDockable>(tool7, tool8),
                    Alignment = Alignment.Right,
                    GripMode = GripMode.AutoHide
                }
            )
        };

        var documentDock = new CustomDocumentDock(_mainWindowViewModel)
        {
            IsCollapsable = false,
            CanCreateDocument = false
        };

        var mainLayout = new ProportionalDock
        {
            Orientation = Orientation.Horizontal,
            VisibleDockables = CreateList<IDockable>
            (
                leftDock,
                new ProportionalDockSplitter(),
                documentDock,
                new ProportionalDockSplitter(),
                rightDock
            )
        };

        var dashboardView = new DashboardViewModel
        {
            Id = "Dashboard",
            Title = "Dashboard"
        };

        var homeView = new HomeViewModel
        {
            Id = "Home",
            Title = "Home",
            ActiveDockable = mainLayout,
            VisibleDockables = CreateList<IDockable>(mainLayout)
        };

        var rootDock = CreateRootDock();

        rootDock.IsCollapsable = false;
        rootDock.ActiveDockable = dashboardView;
        rootDock.DefaultDockable = homeView;
        rootDock.VisibleDockables = CreateList<IDockable>(dashboardView, homeView);

        _documentDock = documentDock;
        _rootDock = rootDock;
            
        return rootDock;
    }

    public override IDockWindow? CreateWindowFrom(IDockable dockable)
    {
        var window = base.CreateWindowFrom(dockable);

        if (window != null)
        {
            window.Title = "Blitz";
        }
        return window;
    }

    public override void InitLayout(IDockable layout)
    {
        ContextLocator = new Dictionary<string, Func<object?>>
        {
            ["Library"] = () => new Library(),
            ["Tool2"] = () => new Tool2(),
            ["Tool3"] = () => new Tool3(),
            ["Tool4"] = () => new Tool4(),
            ["Tool5"] = () => new Tool5(),
            ["Tool6"] = () => new Tool6(),
            ["Tool7"] = () => new Tool7(),
            ["Tool8"] = () => new Tool8(),
            ["Dashboard"] = () => layout,
            ["Home"] = () => _context
        };

        DockableLocator = new Dictionary<string, Func<IDockable?>>()
        {
            ["Root"] = () => _rootDock,
            ["Documents"] = () => _documentDock
        };

        HostWindowLocator = new Dictionary<string, Func<IHostWindow?>>
        {
            [nameof(IDockWindow)] = () => new HostWindow()
        };

        base.InitLayout(layout);
    }
}
