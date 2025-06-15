using Blitz.ViewModels.Documents;
using System.Collections.Generic;

namespace Blitz.Events
{
    #region Application
    public class ApplicationPreferencesChangedEvent
    {
        public Dictionary<string, object> Preferences { get; set; }

        public ApplicationPreferencesChangedEvent(Dictionary<string, object> preferences)
        {
            Preferences = preferences;
        }
    }
    #endregion

    #region Document
    public class OnDocumentSavedEvent
    {
        public DocumentViewModel Document { get; set; }

        public OnDocumentSavedEvent(DocumentViewModel document)
        {
            Document = document;
        }
    }

    public class ActiveDocumentChangedEvent
    {
        public DocumentViewModel Document { get; set; }

        public ActiveDocumentChangedEvent(DocumentViewModel document)
        {
            Document = document;
        }
    }

    public class DocumentProgressChangedEvent
    {
        public DocumentViewModel Document { get; set; }
        public bool IsInProgress { get; set; }

        public DocumentProgressChangedEvent(DocumentViewModel document, bool isInProgress)
        {
            Document = document;
            IsInProgress = isInProgress;
        }
    }

    public class DocumentFlyoutRequestedEvent
    {
        public DocumentViewModel Document { get; set; }
        public string FlyoutMessage { get; set; }

        public DocumentFlyoutRequestedEvent(DocumentViewModel document, string flyoutMessage)
        {
            Document = document;
            FlyoutMessage = flyoutMessage;
        }
    }
    #endregion

    #region Library
    public class UserLibrarySelectionChangedEvent
    {
        public CsXFL.Item[] UserLibrarySelection { get; set; }

        public UserLibrarySelectionChangedEvent(CsXFL.Item[] userLibrarySelection)
        {
            UserLibrarySelection = userLibrarySelection;
        }
    }
    public class LibraryItemsChangedEvent
    {
        public LibraryItemsChangedEvent() { }
    }
    #endregion

    #region Canvas
    public class CanvasActionCenterEvent
    {
        public int DocumentIndex { get; set; }

        public CanvasActionCenterEvent(int documentIndex)
        {
            DocumentIndex = documentIndex;
        }
    }

    public class CanvasActionToggleClipEvent
    {
        public int DocumentIndex { get; set; }

        public CanvasActionToggleClipEvent(int documentIndex)
        {
            DocumentIndex = documentIndex;
        }
    }
    #endregion
}