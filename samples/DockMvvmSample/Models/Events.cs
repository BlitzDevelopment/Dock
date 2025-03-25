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
}