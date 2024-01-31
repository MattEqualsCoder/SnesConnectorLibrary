using Avalonia.Controls;
using Serilog;
using SnesConnectorLibrary;

namespace SnesConnectorApp;

public partial class MainWindow : Window
{
    private ISnesConnectorService? _snesConnectorService;
    private MainWindowViewModel? _model;
    
    public MainWindow() : this(null)
    {
    }
    
    public MainWindow(ISnesConnectorService? snesConnectorService)
    {
        _snesConnectorService = snesConnectorService;
        DataContext = _model = new MainWindowViewModel();
        InitializeComponent();

        if (_snesConnectorService == null)
        {
            return;
        }
        
        _snesConnectorService.AddRecurringRequest(new SnesRecurringMemoryRequest()
        {
            RequestType = SnesMemoryRequestType.Retrieve, 
            SnesMemoryDomain = SnesMemoryDomain.Memory,
            Address = 0x7e09C2,
            Length = 0x400,
            FrequencySeconds = 1,
            OnResponse = data =>
            {
                Log.Information("Response: {Data}", data.ReadUInt16(0x7e09C2));
            },
        });

        _snesConnectorService.OnConnected += (sender, args) =>
        {
            _model.IsConnected = true;
            _model.IsConnectorConnecting = false;
            _model.IsDisconnected = false;
        };
        
        _snesConnectorService.OnDisconnected += (sender, args) =>
        {
            _model.IsConnected = false;
            _model.IsConnectorConnecting = false;
            _model.IsDisconnected = true;
        };
    }


    private void SelectingItemsControl_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_model == null)
        {
            return;
        }
        var selectedItem = (sender as ComboBox)!.SelectedItem as string;
        if (string.IsNullOrEmpty(selectedItem) || !_model.ConnectorMap.TryGetValue(selectedItem, out var selectedConnectorType))
        {
            _snesConnectorService?.Disconnect();
            _model.IsConnected = false;
            _model.IsConnectorConnecting = false;
            _model.IsDisconnected = true;
        }
        else
        {
            _snesConnectorService?.Connect(selectedConnectorType);
            _model.IsConnected = false;
            _model.IsConnectorConnecting = true;
            _model.IsDisconnected = false;
        }
        
        
    }
}