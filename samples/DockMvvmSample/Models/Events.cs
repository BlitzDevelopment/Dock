namespace Blitz.Events
{
    public class ActiveDocumentChangedEvent
    {
        public int Index { get; set; }

        public ActiveDocumentChangedEvent(int index)
        {
            Index = index;
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