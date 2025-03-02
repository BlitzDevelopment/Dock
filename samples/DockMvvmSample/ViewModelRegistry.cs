using System.Collections.Generic;
using DockMvvmSample.ViewModels.Tools;

namespace DockMvvmSample
{
    public class ViewModelRegistry
    {
        public static string Tool1ViewModel { get; } = "Tool1ViewModel";
        private static ViewModelRegistry _instance;

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
            return _viewModels.TryGetValue(key, out var viewModel) ? viewModel : null;
        }

        public Tool1ViewModel GetTool1ViewModel()
        {
            return (Tool1ViewModel)GetViewModel(nameof(Tool1ViewModel));
        }
    }
}