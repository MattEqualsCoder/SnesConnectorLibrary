using System.Collections.Generic;
using AvaloniaControls.Models;
using ReactiveUI.Fody.Helpers;
using SnesConnectorLibrary;

namespace SnesConnectorApp.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    [Reactive] public SnesConnectorType ConnectorType { get; set; }
    [Reactive] public bool IsConnected { get; set; }
    [Reactive] public string CurrentGame { get; set; } = "N/A";
    [Reactive] public string Position { get; set; } = "N/A";
    [Reactive] public string Title { get; set; } = "N/A";
    [Reactive] public List<string> Roms { get; set; } = new();
    [Reactive] public string? Status { get; set; } = "Disconnected";
    
    [Reactive]
    [ReactiveLinkedProperties(nameof(CanBootDeleteFile))]
    public string? SelectedRom { get; set; }
    public bool CanBootDeleteFile => !string.IsNullOrEmpty(SelectedRom);
}
