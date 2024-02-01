using Microsoft.Extensions.Logging;
using SnesConnectorLibrary.Connectors;

namespace SnesConnectorLibrary;

internal class SnesConnectorService : ISnesConnectorService
{
    private readonly ILogger<SnesConnectorService> _logger;
    private readonly Dictionary<SnesConnectorType, ISnesConnector> _connectors = new();
    private readonly List<SnesMemoryRequest> _queue = new();
    private readonly List<SnesRecurringMemoryRequest> _recurringRequests = new();
    private ISnesConnector? _currentConnector;
    private SnesConnectorType _currentConnectorType;

    public SnesConnectorService(ILogger<SnesConnectorService> logger, Usb2SnesConnector usb2SnesConnector, LuaConnectorDefault luaConnectorDefault, LuaConnectorEmoTracker luaConnectorEmoTracker, LuaConnectorCrowdControl luaConnectorCrowdControl, SniConnector sniConnector)
    {
        _logger = logger;
        _connectors[SnesConnectorType.Usb2Snes] = usb2SnesConnector;
        _connectors[SnesConnectorType.Lua] = luaConnectorDefault;
        _connectors[SnesConnectorType.LuaEmoTracker] = luaConnectorEmoTracker;
        _connectors[SnesConnectorType.LuaCrowdControl] = luaConnectorCrowdControl;
        _connectors[SnesConnectorType.Sni] = sniConnector;
    }
    
    public event EventHandler? OnConnected;

    public event EventHandler? OnDisconnected;

    public event SnesDataReceivedEventHandler? OnMessage;

    public bool IsConnected => _currentConnector?.IsConnected == true;

    public void Connect(SnesConnectorType type)
    {
        Connect(new SnesConnectorSettings() { ConnectorType = type });
    }

    public void Connect(SnesConnectorSettings settings)
    {
        Disconnect();
        _logger.LogInformation("Connecting to connector type {Type}", settings.ConnectorType.ToString());
        _currentConnectorType = settings.ConnectorType;
        _currentConnector = _connectors[settings.ConnectorType];
        _currentConnector.OnConnected += CurrentConnectorOnConnected;
        _currentConnector.OnDisconnected += CurrentConnectorOnDisconnected;
        _currentConnector.OnMessage += CurrentConnectorOnMessage;
        _currentConnector.Enable(settings);
    }
    
    public void Disconnect()
    {
        if (_currentConnector == null)
        {
            return;
        }
        
        _currentConnector.Disable();
        _currentConnector.OnConnected -= CurrentConnectorOnConnected;
        _currentConnector.OnDisconnected -= CurrentConnectorOnDisconnected;
        _currentConnector.OnMessage -= CurrentConnectorOnMessage;
        _currentConnector = null;
    }
    
    public void Dispose()
    {
        Disconnect();
        _currentConnector?.Dispose();
        GC.SuppressFinalize(this);
    }

    public void MakeRequest(SnesMemoryRequest request)
    {
        if (_currentConnector?.IsConnected != true)
        {
            _logger.LogWarning("No connected connector");
        }

        _queue.Add(request);
    }
    
    public void AddRecurringRequest(SnesRecurringMemoryRequest request)
    {
        _recurringRequests.Add(request);
    }

    private void CurrentConnectorOnMessage(object sender, SnesDataReceivedEventArgs e)
    {
        if (e.Request is SnesRecurringMemoryRequest recurringRequest)
        {
            recurringRequest.LastRunTime = DateTime.Now;
        }
        e.Request.OnResponse?.Invoke(e.Data);
        OnMessage?.Invoke(sender, e);
    }

    private void CurrentConnectorOnDisconnected(object? sender, EventArgs e)
    {
        _logger.LogInformation("Disconnected from {Type} connector", _currentConnectorType.ToString());
        OnDisconnected?.Invoke(sender, e);
    }

    private void CurrentConnectorOnConnected(object? sender, EventArgs e)
    {
        _logger.LogInformation("Successfully connected to {Type} connector", _currentConnectorType.ToString());
        _ = ProcessRequests();
        OnConnected?.Invoke(sender, e);
    }
    
    private async Task ProcessRequests()
    {
        while (IsConnected)
        {
            if (_currentConnector?.CanMakeRequest == true)
            {
                if (_queue.Any())
                {
                    var request = _queue.First();
                    await ProcessRequest(request);
                    _queue.Remove(request);
                }
                else if (_recurringRequests.Any())
                {
                    var request = _recurringRequests.Where(x => x.CanRun).MinBy(x => x.NextRunTime);
                    if (request != null)
                    {
                        await ProcessRequest(request);
                    }
                }
            }
            await Task.Delay(TimeSpan.FromMilliseconds(50));
        }
    }

    private async Task ProcessRequest(SnesMemoryRequest request)
    {
        if (!IsConnected)
        {
            _logger.LogWarning("No connected connector");
            return;
        }
        
        if (request.RequestType == SnesMemoryRequestType.Retrieve)
        {
            await _currentConnector!.GetAddress(request);
        }
        else if (request.RequestType == SnesMemoryRequestType.Update)
        {
            await _currentConnector!.PutAddress(request);
        }

    }
}