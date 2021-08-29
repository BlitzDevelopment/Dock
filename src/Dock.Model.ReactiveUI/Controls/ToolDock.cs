﻿using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.ReactiveUI.Core;
using ReactiveUI;

namespace Dock.Model.ReactiveUI.Controls
{
    /// <summary>
    /// Tool dock.
    /// </summary>
    [DataContract(IsReference = true)]
    public class ToolDock : DockBase, IToolDock
    {
        private Alignment _alignment = Alignment.Unset;
        private bool _isExpanded;
        private bool _autoHide = true;
        private GripMode _gripMode = Model.Core.GripMode.Visible;

        /// <inheritdoc/>
        [DataMember(IsRequired = false, EmitDefaultValue = true)]
        [JsonInclude]
        public Alignment Alignment
        {
            get => _alignment;
            set => this.RaiseAndSetIfChanged(ref _alignment, value);
        }

        /// <inheritdoc/>
        [DataMember(IsRequired = false, EmitDefaultValue = true)]
        [JsonInclude]
        public bool IsExpanded
        {
            get => _isExpanded;
            set => this.RaiseAndSetIfChanged(ref _isExpanded, value);
        }

        /// <inheritdoc/>
        [DataMember(IsRequired = false, EmitDefaultValue = true)]
        [JsonInclude]
        public bool AutoHide
        {
            get => _autoHide;
            set => this.RaiseAndSetIfChanged(ref _autoHide, value);
        }

        /// <inheritdoc/>
        [DataMember(IsRequired = false, EmitDefaultValue = true)]
        [JsonInclude]
        public GripMode GripMode
        {
            get => _gripMode;
            set => this.RaiseAndSetIfChanged(ref _gripMode, value);
        }
    }
}
