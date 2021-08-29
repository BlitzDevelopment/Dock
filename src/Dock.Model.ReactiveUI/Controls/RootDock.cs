﻿using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using System.Windows.Input;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.ReactiveUI.Core;
using ReactiveUI;

namespace Dock.Model.ReactiveUI.Controls
{
    /// <summary>
    /// Root dock.
    /// </summary>
    [DataContract(IsReference = true)]
    public class RootDock : DockBase, IRootDock
    {
        private bool _isFocusableRoot = true;
        private IDockWindow? _window;
        private IList<IDockWindow>? _windows;

        /// <inheritdoc/>
        [DataMember(IsRequired = false, EmitDefaultValue = true)]
        [JsonInclude]
        public bool IsFocusableRoot
        {
            get => _isFocusableRoot;
            set => this.RaiseAndSetIfChanged(ref _isFocusableRoot, value);
        }

        /// <inheritdoc/>
        [DataMember(IsRequired = false, EmitDefaultValue = true)]
        [JsonInclude]
        public IDockWindow? Window
        {
            get => _window;
            set => this.RaiseAndSetIfChanged(ref _window, value);
        }

        /// <inheritdoc/>
        [DataMember(IsRequired = false, EmitDefaultValue = true)]
        [JsonInclude]
        public IList<IDockWindow>? Windows
        {
            get => _windows;
            set => this.RaiseAndSetIfChanged(ref _windows, value);
        }

        /// <summary>
        /// Initializes new instance of the <see cref="RootDock"/> class.
        /// </summary>
        [JsonConstructor]
        public RootDock()
        {
            ShowWindows = ReactiveCommand.Create(() => _navigateAdapter.ShowWindows());
            ExitWindows = ReactiveCommand.Create(() => _navigateAdapter.ExitWindows());
        }

        /// <inheritdoc/>
        [IgnoreDataMember]
        [JsonIgnore]
        public ICommand ShowWindows { get; }

        /// <inheritdoc/>
        [IgnoreDataMember]
        [JsonIgnore]
        public ICommand ExitWindows { get; }
    }
}
