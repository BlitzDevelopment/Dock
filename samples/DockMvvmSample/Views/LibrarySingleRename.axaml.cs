using Avalonia.Controls;
using DialogHostAvalonia;
using Avalonia.Interactivity;
using Avalonia;
using DockMvvmSample.ViewModels;
using CsXFL;
using System;

namespace DockMvvmSample.Views
{
    public partial class LibrarySingleRename : UserControl
    {
        async void DialogHost_Loaded(object sender, RoutedEventArgs e)
        {
            var dialog = new DialogHost();
            dialog.OpenDialogCommand.Execute(new LibrarySingleRename());
        }
    }
}