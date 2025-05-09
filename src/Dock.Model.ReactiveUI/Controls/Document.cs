﻿using System.Runtime.Serialization;
using Dock.Model.Controls;
using Dock.Model.ReactiveUI.Core;

namespace Dock.Model.ReactiveUI.Controls;

/// <summary>
/// Document.
/// </summary>
[DataContract(IsReference = true)]
public partial class Document : DockableBase, IDocument
{
}
