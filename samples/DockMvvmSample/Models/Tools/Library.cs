using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Blitz.Models.Tools;

public class Library
{
    public class LibraryItem : INotifyPropertyChanged
    {
        private string? _name;
        private bool _isDragOver;
        
        public string Name
        {
            get => _name!;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged(nameof(Name));
                }
            }
        }

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
        public bool IsFolder => Type == "Folder";

        public ObservableCollection<LibraryItem> Children { get; set; } = new();
        public CsXFL.Item? CsXFLItem { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}