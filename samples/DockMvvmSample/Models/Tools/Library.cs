using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Blitz.Models.Tools;

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
        private bool _isDragOver;
        public bool IsDragOver
        {
            get => _isDragOver;
            set
            {
                if (_isDragOver != value)
                {
                    _isDragOver = value;
                    OnPropertyChanged(nameof(IsDragOver));
                }
            }
        }
        public bool IsExpanded { get; set; }
        public string? Type { get; set; }
        public string? UseCount { get; set; }
        public ObservableCollection<LibraryItem> Children { get; set; } = new();
        public CsXFL.Item? CsXFLItem { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        // Override Equals
        public override bool Equals(object obj)
        {
            if (obj is LibraryItem other)
            {
                return Name == other.Name; // Compare based on Name or other unique properties
            }
            return false;
        }

        // Override GetHashCode
        public override int GetHashCode()
        {
            return Name?.GetHashCode() ?? 0; // Use Name's hash code or a default value
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

    }
}
