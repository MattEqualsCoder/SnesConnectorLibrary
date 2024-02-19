using System.Reflection;
using Microsoft.Extensions.Logging;
using SnesConnectorLibrary.Connectors;

namespace SnesConnectorLibrary;

internal class SnesConnectorService : ISnesConnectorService
{
    private readonly ILogger<SnesConnectorService>? _logger;
    private readonly Dictionary<SnesConnectorType, ISnesConnector> _connectors = new();
    private readonly List<SnesMemoryRequest> _queue = new();
    private readonly Dictionary<string, RecurringRequestList> _recurringRequests = new();
    private ISnesConnector? _currentConnector;
    private SnesConnectorType _currentConnectorType;
    private Dictionary<string, SnesData> _previousRequestData = new();

    public SnesConnectorService()
    {
        _connectors[SnesConnectorType.Usb2Snes] = new Usb2SnesConnector();
        _connectors[SnesConnectorType.Lua] = new LuaConnectorDefault();
        _connectors[SnesConnectorType.LuaEmoTracker] = new LuaConnectorEmoTracker();
        _connectors[SnesConnectorType.LuaCrowdControl] = new LuaConnectorCrowdControl();
        _connectors[SnesConnectorType.Sni] = new SniConnector();
    }
    
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
        _logger?.LogInformation("Connecting to connector type {Type}", settings.ConnectorType.ToString());
        _currentConnectorType = settings.ConnectorType;
        _currentConnector = _connectors[settings.ConnectorType];
        _currentConnector.OnConnected += CurrentConnectorOnConnected;
        _currentConnector.OnDisconnected += CurrentConnectorOnDisconnected;
        _currentConnector.OnMessage += CurrentConnectorOnMessage;
        _currentConnector.Enable(settings);
        _previousRequestData.Clear();
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

    public void MakeRequest(SnesSingleMemoryRequest request)
    {
        if (_currentConnector?.IsConnected != true)
        {
            _logger?.LogWarning("No connected connector");
        }

        _queue.Add(request);
    }
    
    public SnesRecurringMemoryRequest AddRecurringRequest(SnesRecurringMemoryRequest request)
    {
        if (_recurringRequests.TryGetValue(request.Key, out var recurringRequest))
        {
            recurringRequest.AddRequest(request);
        }
        else
        {
            _recurringRequests.Add(request.Key, new RecurringRequestList(request));
        }
        return request;
    }

    public void RemoveRecurringRequest(SnesRecurringMemoryRequest request)
    {
        if (!_recurringRequests.TryGetValue(request.Key, out var recurringRequest)) return;
        recurringRequest.RemoveRequest(request, out var isNowEmpty);
        if (isNowEmpty)
        {
            _recurringRequests.Remove(request.Key);
        }
    }

    public bool CreateLuaScriptsFolder(string folder)
    {
        if (!Directory.Exists(folder))
        {
            try
            {
                Directory.CreateDirectory(folder);
                Directory.CreateDirectory(Path.Combine(folder, "x64"));
                Directory.CreateDirectory(Path.Combine(folder, "x86"));
            }
            catch (Exception e)
            {
                _logger?.LogInformation(e, "Could not create target Lua folder {Path}", folder);
                return false;
            }
        }

        try
        {
            CopyEmbeddedResource(folder, "json.lua");
            CopyEmbeddedResource(folder, "emulator.lua");
            CopyEmbeddedResource(folder, "connector.lua");
            CopyEmbeddedResource(folder, "x64/luasocket.LICENSE.txt");
            CopyEmbeddedResource(folder, "x64/socket-linux-5-1.so");
            CopyEmbeddedResource(folder, "x64/socket-linux-5-4.so");
            CopyEmbeddedResource(folder, "x64/socket-windows-5-1.dll");
            CopyEmbeddedResource(folder, "x64/socket-windows-5-4.dll");
            CopyEmbeddedResource(folder, "x86/luasocket.LICENSE.txt");
            CopyEmbeddedResource(folder, "x86/socket-windows-5-1.dll");
            _logger?.LogInformation("Created Lua files at {Path}", folder);
            return true;
        }
        catch (Exception e)
        {
            _logger?.LogInformation(e, "Could not create target Lua file(s)");
            return false;
        }
    }

    private void CopyEmbeddedResource(string targetFolder, string relativePath)
    {
        var inputPath = "SnesConnectorLibrary.Lua." + relativePath.Replace('/', '.');
        var outputPath = Path.Combine(targetFolder, relativePath.Replace('/', Path.DirectorySeparatorChar));
        using var input =  Assembly.GetExecutingAssembly().GetManifestResourceStream(inputPath);

        if (input == null)
        {
            throw new InvalidOperationException($"Could not copy file {inputPath} to {outputPath}");
        }
        using var output = File.Create(outputPath);
        var buffer = new byte[8192];
        int bytesRead;
        while ((bytesRead = input.Read(buffer, 0, buffer.Length)) > 0)
        {
            output.Write(buffer, 0, bytesRead);
        }
    }

    private void CurrentConnectorOnMessage(object sender, SnesDataReceivedEventArgs e)
    {
        if (e.Request is SnesRecurringMemoryRequest recurringRequest)
        {
            var key = recurringRequest.Key;
            recurringRequest.LastRunTime = DateTime.Now;
                
            if (_recurringRequests.TryGetValue(recurringRequest.Key, out var recurringRequestList))
            {
                var dataChanged = !_previousRequestData.TryGetValue(key, out var prevRequest) ||
                                  !prevRequest.Equals(e.Data);
                    
                foreach (var request in recurringRequestList.Requests)
                {
                    if (!request.CanRun) continue;
                    
                    request.LastRunTime = DateTime.Now;
                    
                    if (!request.RespondOnChangeOnly || dataChanged)
                    {
                        InvokeRequest(request, e.Data);
                    }
                }
                
                _previousRequestData[recurringRequestList.MainRequest.Key] = e.Data;
            }
        }
        else
        {
            InvokeRequest(e.Request, e.Data);
        }
        
        OnMessage?.Invoke(sender, e);
    }

    private void InvokeRequest(SnesMemoryRequest request, SnesData data)
    {
        try
        {
            request.OnResponse?.Invoke(data);
        }
        catch (Exception exception)
        {
            _logger?.LogError(exception, "Error while invoking a memory changed event for address {Address} in {Domain}", request.Address.ToString("X6"), request.SnesMemoryDomain.ToString());
        }
    }

    private void CurrentConnectorOnDisconnected(object? sender, EventArgs e)
    {
        _logger?.LogInformation("Disconnected from {Type} connector", _currentConnectorType.ToString());
        OnDisconnected?.Invoke(sender, e);
    }

    private void CurrentConnectorOnConnected(object? sender, EventArgs e)
    {
        _logger?.LogInformation("Successfully connected to {Type} connector", _currentConnectorType.ToString());
        _previousRequestData.Clear();
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
                    var request = _recurringRequests.Values.Where(x => x.MainRequest.CanRun).MinBy(x => x.MainRequest.NextRunTime);
                    if (request != null)
                    {
                        await ProcessRequest(request.MainRequest);
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
            _logger?.LogWarning("No connected connector");
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

    private class RecurringRequestList
    {
        public SnesRecurringMemoryRequest MainRequest { get; private set; } = null!;
        
        public List<SnesRecurringMemoryRequest> Requests { get; } = new();

        private void SetMainRequest()
        {
            MainRequest = new SnesRecurringMemoryRequest()
            {
                Address = Requests.First().Address,
                AddressFormat = Requests.First().AddressFormat,
                SnesMemoryDomain = Requests.First().SnesMemoryDomain,
                SniMemoryMapping = Requests.First().SniMemoryMapping,
                FrequencySeconds = Requests.Min(x => x.FrequencySeconds),
                Length = Requests.Max(x => x.Length)
            };
        }

        public RecurringRequestList(SnesRecurringMemoryRequest request)
        {
            Requests.Add(request);
            SetMainRequest();
        }
        
        public void AddRequest(SnesRecurringMemoryRequest request)
        {
            Requests.Add(request);
            SetMainRequest();
        }

        public void RemoveRequest(SnesRecurringMemoryRequest request, out bool isNowEmpty)
        {
            Requests.Remove(request);
            if (Requests.Count == 0)
            {
                isNowEmpty = true;
                return;
            }
            SetMainRequest();
            isNowEmpty = false;
        }
    }
}