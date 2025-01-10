
using System.Collections.ObjectModel;

namespace DockMvvmSample.Models.Tools;

public class Tool1
{
    public class LibraryItem
    {
        public string? Name { get; set; }
        public string? Type { get; set; }
        public string? UseCount { get; set; }
        public ObservableCollection<LibraryItem> Children { get; set; } = new();
    }
}
