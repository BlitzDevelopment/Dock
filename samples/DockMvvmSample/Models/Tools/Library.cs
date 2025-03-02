using System.Collections.ObjectModel;
using System.ComponentModel;

namespace DockMvvmSample.Models.Tools;

public class Library
{
    public class LibraryItem : INotifyPropertyChanged
    {
        private string? _name;
        public string Name
        {
            get { return _name!; }
            set
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }
        public string? Type { get; set; }
        public string? UseCount { get; set; }
        public ObservableCollection<LibraryItem> Children { get; set; } = new();
        public CsXFL.Item? CsXFLItem { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

    }
}
