using Blitz.ViewModels.Documents;

namespace Blitz.Events
{
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
        public LibraryItemsChangedEvent() {}
    }
}