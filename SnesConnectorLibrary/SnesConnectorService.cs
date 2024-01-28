using Microsoft.Extensions.Logging;
using SnesConnectorLibrary.Connectors;

namespace SnesConnectorLibrary;

public class SnesConnectorService
{
    private readonly ILogger<SnesConnectorService> _logger;
    private readonly Dictionary<SnesConnectorType, ISnesConnector> _connectors = new();
    private ISnesConnector? _currentConnector;
    private List<SnesMemoryRequest> _queue = new();
    private List<SnesScheduledMemoryRequest> _scheduledRequests = new();

    public SnesConnectorService(ILogger<SnesConnectorService> logger, Usb2SnesConnector usb2SnesConnector, LuaConnectorDefault luaConnectorDefault, LuaConnectorEmoTracker luaConnectorEmoTracker, LuaConnectorCrowdControl luaConnectorCrowdControl, LuaConnectorSni luaConnectorSni, SniConnector sniConnector)
    {
        _logger = logger;
        _connectors[SnesConnectorType.Usb2Snes] = usb2SnesConnector;
        _connectors[SnesConnectorType.Lua] = luaConnectorDefault;
        _connectors[SnesConnectorType.LuaEmoTracker] = luaConnectorEmoTracker;
        _connectors[SnesConnectorType.LuaCrowdControl] = luaConnectorCrowdControl;
        _connectors[SnesConnectorType.LuaSni] = luaConnectorSni;
        _connectors[SnesConnectorType.Sni] = sniConnector;
    }
    
    public event EventHandler? OnConnected;

    public event EventHandler? OnDisconnected;

    public event SnesDataReceivedEventHandler? OnMessage;

    public bool IsConnected => _currentConnector?.IsConnected == true;

    public async Task ProcessRequests()
    {
        while (IsConnected)
        {
            if (_currentConnector?.CanMakeRequest == true)
            {
                if (_queue.Any())
                {
                    var request = _queue.First();
                    ProcessRequest(request);
                    _queue.Remove(request);
                }
                else if (_scheduledRequests.Any())
                {
                    var request = _scheduledRequests.Where(x => x.ShouldRun).MinBy(x => x.NextRunTime);
                    if (request != null)
                    {
                        ProcessRequest(request);
                    }
                }
            }
            await Task.Delay(TimeSpan.FromMilliseconds(50));
        }
    }
    
    public void Connect(SnesConnectorType type)
    {
        Connect(new SnesConnectorSettings() { ConnectorType = type });
    }

    public void Connect(SnesConnectorSettings settings)
    {
        Disconnect();
        _logger.LogInformation("Connecting to connector type {Type}", settings.ConnectorType.ToString());
        _currentConnector = _connectors[settings.ConnectorType];
        _currentConnector.OnConnected += CurrentConnectorOnConnected;
        _currentConnector.OnDisconnected += CurrentConnectorOnDisconnected;
        _currentConnector.OnMessage += CurrentConnectorOnMessage;
        _currentConnector.Connect(settings);
    }

    private void CurrentConnectorOnMessage(object sender, SnesDataReceivedEventArgs e)
    {
        if (e.Request is SnesScheduledMemoryRequest scheduledRequest)
        {
            scheduledRequest.LastRunTime = DateTime.Now;
        }
        e.Request.OnResponse?.Invoke(e.Data);
        OnMessage?.Invoke(sender, e);
    }

    private void CurrentConnectorOnDisconnected(object? sender, EventArgs e)
    {
        OnDisconnected?.Invoke(sender, e);
    }

    private void CurrentConnectorOnConnected(object? sender, EventArgs e)
    {
        _ = ProcessRequests();
        OnConnected?.Invoke(sender, e);
    }

    public void Disconnect()
    {
        if (_currentConnector == null)
        {
            return;
        }
        
        _currentConnector.Disconnect();
        _currentConnector.OnConnected -= CurrentConnectorOnConnected;
        _currentConnector.OnDisconnected -= CurrentConnectorOnDisconnected;
        _currentConnector.OnMessage -= CurrentConnectorOnMessage;
        _currentConnector = null;
    }

    public void MakeRequest(SnesMemoryRequest request)
    {
        if (_currentConnector?.IsConnected != true)
        {
            _logger.LogWarning("No connected connector");
        }

        _queue.Add(request);
    }
    
    private void ProcessRequest(SnesMemoryRequest request)
    {
        if (!IsConnected)
        {
            _logger.LogWarning("No connected connector");
            return;
        }
        
        if (request.RequestType == SnesMemoryRequestType.GetAddress)
        {
            _currentConnector!.GetAddress(request);
        }
        else if (request.RequestType == SnesMemoryRequestType.PutAddress)
        {
            _currentConnector!.PutAddress(request);
        }

    }

    public void AddScheduledRequest(SnesScheduledMemoryRequest request)
    {
        _scheduledRequests.Add(request);
    }
    
    
}