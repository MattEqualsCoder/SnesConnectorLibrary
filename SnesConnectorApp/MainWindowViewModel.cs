using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using SnesConnectorLibrary;

namespace SnesConnectorApp;

public class MainWindowViewModel : INotifyPropertyChanged
{
    public List<string> Connectors { get; set; } = new List<string> { "Select a connector" }.Concat(
        Enum.GetValues(typeof(SnesConnectorType)).Cast<Enum>().Select(x => x.ToDescription().Description)).ToList();

    public Dictionary<string, SnesConnectorType> ConnectorMap { get; set; } = Enum.GetValues(typeof(SnesConnectorType))
        .Cast<Enum>().Select(x => x.ToDescription()).ToDictionary(x => x.Description, x => (SnesConnectorType)x.Value!);

    private bool _isDisconnected = true;

    public bool IsDisconnected
    {
        get => _isDisconnected;
        set => SetField(ref _isDisconnected, value);
    }
    
    private bool _isConnected;

    public bool IsConnected
    {
        get => _isConnected;
        set => SetField(ref _isConnected, value);
    }
    
    private bool _isConnectorSelected;

    public bool IsConnectorConnecting
    {
        get => _isConnectorSelected;
        set => SetField(ref _isConnectorSelected, value);
    }
    
    private string _currentGame = "N/A";

    public string CurrentGame
    {
        get => _currentGame;
        set => SetField(ref _currentGame, value);
    }
    
    private string _position = "N/A";

    public string Position
    {
        get => _position;
        set => SetField(ref _position, value);
    }
    
    private string _song = "N/A";

    public string Song
    {
        get => _song;
        set => SetField(ref _song, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}