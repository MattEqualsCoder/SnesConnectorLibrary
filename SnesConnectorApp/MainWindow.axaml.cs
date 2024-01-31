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
            SnesMemoryDomain = SnesMemoryDomain.SaveRam,
            Address = 0xA173FE,
            Length = 2,
            FrequencySeconds = 0.5,
            OnResponse = data =>
            {
                _model.CurrentGame = data.ReadUInt8(0xA173FE) == 0xFF ? "Super Metroid" : "A Link to the Past";
            },
        });
        
        _snesConnectorService.AddRecurringRequest(new SnesRecurringMemoryRequest()
        {
            RequestType = SnesMemoryRequestType.Retrieve, 
            SnesMemoryDomain = SnesMemoryDomain.Memory,
            Address = 0x7E0020,
            Length = 4,
            FrequencySeconds = 0.5,
            OnResponse = data =>
            {
                _model.Position = $"({data.ReadUInt16(0x7E0022)}, {data.ReadUInt16(0x7E0020)})";
            },
            Filter = () => _model.CurrentGame == "A Link to the Past"
        });
        
        _snesConnectorService.AddRecurringRequest(new SnesRecurringMemoryRequest()
        {
            RequestType = SnesMemoryRequestType.Retrieve, 
            SnesMemoryDomain = SnesMemoryDomain.Memory,
            Address = 0x7E0AF6,
            Length = 8,
            FrequencySeconds = 0.5,
            OnResponse = data =>
            {
                _model.Position = $"({data.ReadUInt16(0x7E0AF6)}, {data.ReadUInt16(0x7E0AFA)})";
            },
            Filter = () => _model.CurrentGame == "Super Metroid"
        });
        
        _snesConnectorService.AddRecurringRequest(new SnesRecurringMemoryRequest()
        {
            RequestType = SnesMemoryRequestType.Retrieve, 
            SnesMemoryDomain = SnesMemoryDomain.Memory,
            Address = 0x7E010B,
            Length = 1,
            FrequencySeconds = 0.5,
            OnResponse = data =>
            {
                _model.Song = $"{data.Raw[0]}";
            },
            Filter = () => _model.CurrentGame == "A Link to the Past"
        });
        
        _snesConnectorService.AddRecurringRequest(new SnesRecurringMemoryRequest()
        {
            RequestType = SnesMemoryRequestType.Retrieve, 
            SnesMemoryDomain = SnesMemoryDomain.Memory,
            Address = 0x7E0332,
            Length = 1,
            FrequencySeconds = 0.5,
            OnResponse = data =>
            {
                _model.Song = $"{data.Raw[0]}";
            },
            Filter = () => _model.CurrentGame == "Super Metroid"
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