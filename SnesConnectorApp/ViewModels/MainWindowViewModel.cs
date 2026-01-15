using System.Collections.Generic;
using AvaloniaControls.Models;
using ReactiveUI.SourceGenerators;
using SnesConnectorLibrary;

namespace SnesConnectorApp.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [Reactive] public partial SnesConnectorType ConnectorType { get; set; }
    [Reactive] public partial bool IsConnected { get; set; }
    [Reactive] public partial string CurrentGame { get; set; } = "N/A";
    [Reactive] public partial string Position { get; set; } = "N/A";
    [Reactive] public partial string Title { get; set; } = "N/A";
    [Reactive] public partial List<string> Roms { get; set; } = new();
    [Reactive] public partial string? Status { get; set; } = "Disconnected";
    
    [Reactive]
    [ReactiveLinkedProperties(nameof(CanBootDeleteFile))]
    public partial string? SelectedRom { get; set; }
    
    public bool CanBootDeleteFile => !string.IsNullOrEmpty(SelectedRom);
}
