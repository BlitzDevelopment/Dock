﻿using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Markup.Xaml;

namespace DockMvvmSample.Views.Tools;

public partial class LibraryView : UserControl
{
    public LibraryView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
