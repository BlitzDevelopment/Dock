namespace Blitz.Events
{
    public class ActiveDocumentChangedEvent
    {
        public CsXFL.Document NewDocument { get; set; }

        public ActiveDocumentChangedEvent(CsXFL.Document newDocument)
        {
            NewDocument = newDocument;
        }
    }
}