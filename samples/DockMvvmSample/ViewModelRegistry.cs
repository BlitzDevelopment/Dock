using System.Collections.Generic;
using DockMvvmSample.ViewModels.Tools;

namespace DockMvvmSample
{
    public class ViewModelRegistry
    {
        public static string LibraryViewModel { get; } = "LibraryViewModel";
        private static ViewModelRegistry _instance = new ViewModelRegistry();

        private ViewModelRegistry() { }

        public static ViewModelRegistry Instance
        {
            get { return _instance ?? (_instance = new ViewModelRegistry()); }
        }
        private readonly Dictionary<string, object> _viewModels = new Dictionary<string, object>();

        public void RegisterViewModel(string key, object viewModel)
        {
            _viewModels[key] = viewModel;
        }

        public object GetViewModel(string key)
        {
            if (_viewModels.TryGetValue(key, out var viewModel))
            {
                return viewModel;
            }
            else
            {
                throw new KeyNotFoundException($"VMR failure. ViewModel with key '{key}' not found.");
            }
        }

        public LibraryViewModel GetLibraryViewModel()
        {
            return (LibraryViewModel)GetViewModel(nameof(LibraryViewModel));
        }
    }
}