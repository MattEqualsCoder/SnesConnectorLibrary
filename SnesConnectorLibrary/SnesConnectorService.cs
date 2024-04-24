using System.Reflection;
using Microsoft.Extensions.Logging;
using SnesConnectorLibrary.Connectors;
using SnesConnectorLibrary.Requests;
using SnesConnectorLibrary.Responses;

namespace SnesConnectorLibrary;

internal class SnesConnectorService : ISnesConnectorService
{
    private readonly ILogger<SnesConnectorService>? _logger;
    private readonly Dictionary<SnesConnectorType, ISnesConnector> _connectors = new();
    private readonly List<SnesRequest> _queue = new();
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
    
    public void Dispose()
    {
        Disconnect();
        _currentConnector?.Dispose();
        GC.SuppressFinalize(this);
    }
    
    #region Events and properties
    public event EventHandler? Connected;
    public event EventHandler? Disconnected;
    public event SnesMemoryResponseEventHandler? MemoryReceived;
    public event SnesResponseEventHandler<SnesMemoryRequest>? MemoryUpdated;
    public event SnesFileListResponseEventHandler? FileListReceived;
    public event SnesResponseEventHandler<SnesBootRomRequest>? RomBooted;
    public event SnesResponseEventHandler<SnesUploadFileRequest>? FileUploaded;
    public event SnesResponseEventHandler<SnesDeleteFileRequest>? FileDeleted;

    public bool IsConnected => _currentConnector?.IsConnected == true;
    
    #endregion

    #region Public methods
    public void Connect(SnesConnectorType type)
    {
        Connect(new SnesConnectorSettings() { ConnectorType = type });
    }

    public void Connect(SnesConnectorSettings settings)
    {
        if (IsConnected && _currentConnectorType == settings.ConnectorType)
        {
            return;
        }
        
        Disconnect();

        if (settings.ConnectorType == SnesConnectorType.None)
        {
            throw new InvalidOperationException("Invalid SNES Connector Type");
        }
        
        _logger?.LogInformation("Connecting to connector type {Type}", settings.ConnectorType.ToString());
        _currentConnectorType = settings.ConnectorType;
        _currentConnector = _connectors[settings.ConnectorType];
        _currentConnector.Connected += CurrentConnectorConnected;
        _currentConnector.Disconnected += CurrentConnectorDisconnected;
        _currentConnector.MemoryReceived += CurrentConnectorMemoryReceived;
        _currentConnector.MemoryUpdated += CurrentConnectorOnMemoryUpdated;
        _currentConnector.GameDetected += CurrentConnectorOnGameDetected;
        _currentConnector.FileListReceived += CurrentConnectorOnFileListReceived;
        _currentConnector.RomBooted += CurrentConnectorOnRomBooted;
        _currentConnector.FileUploaded += CurrentConnectorOnFileUploaded;
        _currentConnector.FileDeleted += CurrentConnectorOnFileDeleted;
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
        _currentConnector.Connected -= CurrentConnectorConnected;
        _currentConnector.Disconnected -= CurrentConnectorDisconnected;
        _currentConnector.MemoryReceived -= CurrentConnectorMemoryReceived;
        _currentConnector.MemoryUpdated -= CurrentConnectorOnMemoryUpdated;
        _currentConnector.GameDetected -= CurrentConnectorOnGameDetected;
        _currentConnector.FileListReceived -= CurrentConnectorOnFileListReceived;
        _currentConnector.RomBooted -= CurrentConnectorOnRomBooted;
        _currentConnector.FileUploaded -= CurrentConnectorOnFileUploaded;
        _currentConnector.FileDeleted -= CurrentConnectorOnFileDeleted;
        _currentConnector = null;
    }

    public bool GetFileList(SnesFileListRequest request) => MakeRequest(request);
    public bool BootRom(SnesBootRomRequest request) => MakeRequest(request);
    public bool UploadFile(SnesUploadFileRequest request) => MakeRequest(request);
    public bool DeleteFile(SnesDeleteFileRequest request) => MakeRequest(request);
    public bool MakeMemoryRequest(SnesSingleMemoryRequest request) => MakeRequest(request);

    public bool MakeRequest(SnesRequest request)
    {
        if (_currentConnector?.IsConnected != true)
        {
            _logger?.LogWarning("No connected connector");
            return false;
        }
        else if (!_currentConnector.CanMakeRequest(request))
        {
            _logger?.LogWarning("The current connector does not support that request type");
            return false;
        }

        _queue.Add(request);
        return true;
    }
    
    public SnesRecurringMemoryRequest AddRecurringMemoryRequest(SnesRecurringMemoryRequest request)
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

    public void ClearRequests()
    {
        _recurringRequests.Clear();
        _previousRequestData.Clear();
        _queue.Clear();
    }

    public bool CreateLuaScriptsFolder(string folder)
    {
        if (!Directory.Exists(folder))
        {
            try
            {
                Directory.CreateDirectory(folder);
            }
            catch (Exception e)
            {
                _logger?.LogInformation(e, "Could not create target Lua folder {Path}", folder);
                return false;
            }
        }
        
        if (!Directory.Exists(Path.Combine(folder, "x64")))
        {
            try
            {
                Directory.CreateDirectory(Path.Combine(folder, "x64"));
            }
            catch (Exception e)
            {
                _logger?.LogInformation(e, "Could not create target Lua folder {Path}", folder);
                return false;
            }
        }
        
        if (!Directory.Exists(Path.Combine(folder, "x86")))
        {
            try
            {
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

    public ConnectorFunctionality CurrentConnectorFunctionality =>
        _currentConnector?.SupportedFunctionality ?? new ConnectorFunctionality();

    #endregion

    #region Private methods
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

    private void CurrentConnectorMemoryReceived(object sender, SnesMemoryResponseEventArgs e)
    {
        if (e.Data.Raw.Length > 10)
        {
            _logger?.LogTrace("{ByteCount} bytes received from {Domain} address 0x{Address}", e.Data.Raw.Length, e.Request.SnesMemoryDomain.ToString(), e.Request.Address.ToString("X"));    
        }
        else
        {
            _logger?.LogTrace("[{Data}] received from {Domain} address 0x{Address}", string.Join(", ", e.Data.Raw), e.Request.SnesMemoryDomain.ToString(), e.Request.Address.ToString("X"));
        }
        
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
                        InvokeRequest(request, e.Data, prevRequest);
                    }
                }
                
                _previousRequestData[recurringRequestList.MainRequest.Key] = e.Data;
            }
        }
        else
        {
            InvokeRequest(e.Request, e.Data, null);
        }
        
        MemoryReceived?.Invoke(sender, e);
    }
    
    private void CurrentConnectorOnMemoryUpdated(object sender, SnesResponseEventArgs<SnesMemoryRequest> e)
    {
        MemoryUpdated?.Invoke(sender, e);
    }

    private void InvokeRequest(SnesMemoryRequest request, SnesData data, SnesData? prevData)
    {
        try
        {
            request.OnResponse?.Invoke(data, prevData);
        }
        catch (Exception exception)
        {
            _logger?.LogError(exception, "Error while invoking a memory changed event for address {Address} in {Domain}", request.Address.ToString("X6"), request.SnesMemoryDomain.ToString());
        }
    }

    private void CurrentConnectorDisconnected(object? sender, EventArgs e)
    {
        _logger?.LogInformation("Disconnected from {Type} connector", _currentConnectorType.ToString());
        Disconnected?.Invoke(sender, e);
    }

    private void CurrentConnectorConnected(object? sender, EventArgs e)
    {
        _logger?.LogInformation("Successfully connected to {Type} connector", _currentConnectorType.ToString());
        _previousRequestData.Clear();
        _ = ProcessRequests();
        Connected?.Invoke(sender, e);
    }
    
    private async Task ProcessRequests()
    {
        while (IsConnected)
        {
            if (_currentConnector?.CanProcessRequests == true)
            {
                if (_queue.Any())
                {
                    var request = _queue.First();
                    await ProcessRequest(request);
                    _queue.Remove(request);
                }
                else if (_recurringRequests.Any())
                {
                    var request = _recurringRequests.Values.Where(x => x.MainRequest.CanRun && _currentConnector.CanMakeRequest(x.MainRequest)).MinBy(x => x.MainRequest.NextRunTime);
                    if (request != null)
                    {
                        await ProcessRequest(request.MainRequest);
                    }
                }
            }
            
            await Task.Delay(TimeSpan.FromMilliseconds(50));
        }
    }

    private async Task ProcessRequest(SnesRequest request)
    {
        if (!IsConnected)
        {
            _logger?.LogWarning("No connected connector");
            return;
        }
        
        if (request.RequestType == SnesRequestType.Memory && request is SnesMemoryRequest memoryRequest)
        {
            if (memoryRequest.MemoryRequestType == SnesMemoryRequestType.RetrieveMemory)
            {
                _logger?.LogTrace("{Count} bytes requested from {Domain} address 0x{Address}", memoryRequest.Length, memoryRequest.SnesMemoryDomain.ToString(), memoryRequest.Address.ToString("X") );
                await _currentConnector!.RetrieveMemory(memoryRequest);    
            }
            else
            {
                _logger?.LogTrace("{Count} bytes being updated on {Domain} address 0x{Address}", memoryRequest.Data?.Count, memoryRequest.SnesMemoryDomain.ToString(), memoryRequest.Address.ToString("X") );
                await _currentConnector!.UpdateMemory(memoryRequest);
            }
        }
        else if (request.RequestType == SnesRequestType.GetFileList && request is SnesFileListRequest fileListRequest)
        {
            _logger?.LogDebug("Getting files from path {Path}", fileListRequest.Path);
            await _currentConnector!.ListFiles(fileListRequest);
        }
        else if (request.RequestType == SnesRequestType.BootRom && request is SnesBootRomRequest bootRomRequest)
        {
            _logger?.LogDebug("Booting rom {Rom}", bootRomRequest.Path);
            await _currentConnector!.BootRom(bootRomRequest);
        }
        else if (request.RequestType == SnesRequestType.UploadFile && request is SnesUploadFileRequest uploadFileRequest)
        {
            _logger?.LogDebug("Uploading file {LocalPath} to {TargetPath}", uploadFileRequest.LocalFilePath, uploadFileRequest.TargetFilePath);
            await _currentConnector!.UploadFile(uploadFileRequest);
        }
        else if (request.RequestType == SnesRequestType.DeleteFile && request is SnesDeleteFileRequest deleteFileRequest)
        {
            _logger?.LogDebug("Deleting file {Path}", deleteFileRequest.Path);
            await _currentConnector!.DeleteFile(deleteFileRequest);
        }
    }
    
    private void CurrentConnectorOnFileDeleted(object sender, SnesResponseEventArgs<SnesDeleteFileRequest> e)
    {
        _logger?.LogInformation("{FileName} deleted", e.Request.Path);
        FileDeleted?.Invoke(sender, e);
        e.Request.OnComplete?.Invoke();
    }

    private void CurrentConnectorOnFileUploaded(object sender, SnesResponseEventArgs<SnesUploadFileRequest> e)
    {
        _logger?.LogInformation("{Source} uploaded to {Destination}", e.Request.LocalFilePath, e.Request.TargetFilePath);
        FileUploaded?.Invoke(sender, e);
        e.Request.OnComplete?.Invoke();
    }

    private void CurrentConnectorOnRomBooted(object sender, SnesResponseEventArgs<SnesBootRomRequest> e)
    {
        _logger?.LogInformation("{FileName} booted", e.Request.Path);
        RomBooted?.Invoke(sender, e);
        e.Request.OnComplete?.Invoke();
    }

    private void CurrentConnectorOnFileListReceived(object sender, SnesFileListResponseEventArgs e)
    {
        _logger?.LogInformation("{Count} files found within {FileName}", e.Files.Count, e.Request.Path == "" ? "the root directory" : e.Request.Path);
        FileListReceived?.Invoke(sender, e);
        e.Request.OnResponse?.Invoke(e.Files);
    }

    private void CurrentConnectorOnGameDetected(object? sender, EventArgs e)
    {
        _logger?.LogInformation("Game detected!");
    }
    #endregion

    #region Classes
    private class RecurringRequestList
    {
        public SnesRecurringMemoryRequest MainRequest { get; private set; } = null!;
        
        public List<SnesRecurringMemoryRequest> Requests { get; } = new();

        private void SetMainRequest()
        {
            MainRequest = new SnesRecurringMemoryRequest()
            {
                MemoryRequestType = Requests.First().MemoryRequestType,
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
    #endregion
}