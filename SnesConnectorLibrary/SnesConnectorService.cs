﻿using System.Reflection;
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
    public event EventHandler? GameDetected;
    public event SnesMemoryResponseEventHandler? MemoryReceived;
    public event SnesResponseEventHandler<SnesMemoryRequest>? MemoryUpdated;
    public event SnesFileListResponseEventHandler? FileListReceived;
    public event SnesResponseEventHandler<SnesBootRomRequest>? RomBooted;
    public event SnesResponseEventHandler<SnesUploadFileRequest>? FileUploaded;
    public event SnesResponseEventHandler<SnesDeleteFileRequest>? FileDeleted;
    public event SnesResponseEventHandler<SnesCreateDirectoryRequest>? DirectoryCreated;
    public event SnesResponseEventHandler<SnesDeleteDirectoryRequest>? DirectoryDeleted;

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
        _currentConnector.DirectoryCreated += CurrentConnectorOnDirectoryCreated;
        _currentConnector.DirectoryDeleted += CurrentConnectorOnDirectoryDeleted;
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
        _currentConnector.DirectoryCreated -= CurrentConnectorOnDirectoryCreated;
        _currentConnector.DirectoryDeleted -= CurrentConnectorOnDirectoryDeleted;
        _currentConnector = null;
    }

    public bool GetFileList(SnesFileListRequest request) => MakeRequest(request);
    public Task<SnesFileListResponse> GetFileListAsync(SnesFileListRequest request)
    {
        var previousAction = request.OnResponse;

        var tcs = new TaskCompletionSource<SnesFileListResponse>();

        request.OnResponse = files =>
        {
            previousAction?.Invoke(files);
            try
            {
                tcs.TrySetResult(new SnesFileListResponse
                {
                    Successful = true,
                    Files = files
                });
            }
            catch (Exception)
            {
                // Do nothing
            }
        };

        if (!GetFileList(request))
        {
            try
            {
                tcs.TrySetResult(new SnesFileListResponse()
                {
                    Successful = false,
                    Files = new List<SnesFile>()
                });
            }
            catch (Exception)
            {
                // Do nothing
            }
        }

        return tcs.Task;
    }
    
    public bool BootRom(SnesBootRomRequest request) => MakeRequest(request);
    public Task<SnesBootRomResponse> BootRomAsync(SnesBootRomRequest request)
    {
        var previousAction = request.OnComplete;

        var tcs = new TaskCompletionSource<SnesBootRomResponse>();

        request.OnComplete = () =>
        {
            previousAction?.Invoke();
            try
            {
                tcs.TrySetResult(new SnesBootRomResponse()
                {
                    Successful = true
                });
            }
            catch (Exception)
            {
                // Do nothing
            }
        };

        if (!BootRom(request))
        {
            try
            {
                tcs.TrySetResult(new SnesBootRomResponse()
                {
                    Successful = false
                });
            }
            catch (Exception)
            {
                // Do nothing
            }
        }

        return tcs.Task;
    }
    
    public bool UploadFile(SnesUploadFileRequest request) => MakeRequest(request);
    public Task<SnesUploadFileResponse> UploadFileAsync(SnesUploadFileRequest request)
    {
        var previousAction = request.OnComplete;

        var tcs = new TaskCompletionSource<SnesUploadFileResponse>();

        request.OnComplete = () =>
        {
            previousAction?.Invoke();
            
            try
            {
                tcs.TrySetResult(new SnesUploadFileResponse()
                {
                    Successful = true
                });
            }
            catch (Exception)
            {
                // Do nothing
            }
        };

        if (!UploadFile(request))
        {
            try
            {
                tcs.TrySetResult(new SnesUploadFileResponse()
                {
                    Successful = false
                });
            }
            catch (Exception)
            {
                // Do nothing
            }
        }

        return tcs.Task;
    }
    
    public bool DeleteFile(SnesDeleteFileRequest request) => MakeRequest(request);
    public Task<SnesDeleteFileResponse> DeleteFileAsync(SnesDeleteFileRequest request)
    {
        var previousAction = request.OnComplete;

        var tcs = new TaskCompletionSource<SnesDeleteFileResponse>();

        request.OnComplete = () =>
        {
            previousAction?.Invoke();
            
            try
            {
                tcs.TrySetResult(new SnesDeleteFileResponse()
                {
                    Successful = true
                });
            }
            catch (Exception)
            {
                // Do nothing
            }
        };

        if (!DeleteFile(request))
        {
            try
            {
                tcs.TrySetResult(new SnesDeleteFileResponse()
                {
                    Successful = false
                });
            }
            catch (Exception)
            {
                // Do nothing
            }
        }

        return tcs.Task;
    }

    
    
    public bool CreateDirectory(SnesCreateDirectoryRequest request)
    {
        request.Path = FixDirectory(request.Path);
        return MakeRequest(request);
    }

    public Task<SnesCreateDirectoryResponse> CreateDirectoryAsync(SnesCreateDirectoryRequest request)
    {
        request.Path = FixDirectory(request.Path);
        
        var previousAction = request.OnComplete;

        var tcs = new TaskCompletionSource<SnesCreateDirectoryResponse>();

        request.OnComplete = () =>
        {
            previousAction?.Invoke();
            
            try
            {
                tcs.TrySetResult(new SnesCreateDirectoryResponse()
                {
                    Successful = true
                });
            }
            catch (Exception)
            {
                // Do nothing
            }
        };

        if (!CreateDirectory(request))
        {
            try
            {
                tcs.TrySetResult(new SnesCreateDirectoryResponse()
                {
                    Successful = false
                });
            }
            catch (Exception)
            {
                // Do nothing
            }
        }

        return tcs.Task;
    }

    public bool DeleteDirectory(SnesDeleteDirectoryRequest request)
    {
        request.Path = FixDirectory(request.Path);
        return MakeRequest(request);
    }

    public Task<SnesDeleteDirectoryResponse> DeleteDirectoryAsync(SnesDeleteDirectoryRequest request)
    {
        request.Path = FixDirectory(request.Path);
        
        var previousAction = request.OnComplete;

        var tcs = new TaskCompletionSource<SnesDeleteDirectoryResponse>();

        request.OnComplete = () =>
        {
            previousAction?.Invoke();
            
            try
            {
                tcs.TrySetResult(new SnesDeleteDirectoryResponse()
                {
                    Successful = true
                });
            }
            catch (Exception)
            {
                // Do nothing
            }
        };

        if (!DeleteDirectory(request))
        {
            try
            {
                tcs.TrySetResult(new SnesDeleteDirectoryResponse()
                {
                    Successful = false
                });
            }
            catch (Exception)
            {
                // Do nothing
            }
        }

        return tcs.Task;
    }

    public bool MakeMemoryRequest(SnesSingleMemoryRequest request) => MakeRequest(request);

    public Task<SnesSingleMemoryResponse> MakeMemoryRequestAsync(SnesSingleMemoryRequest request)
    {
        var previousAction = request.OnResponse;

        var tcs = new TaskCompletionSource<SnesSingleMemoryResponse>();

        request.OnResponse = (data, prevData) =>
        {
            previousAction?.Invoke(data, prevData);
            try
            {
                tcs.TrySetResult(new SnesSingleMemoryResponse
                {
                    Successful = true,
                    Data = data
                });
            }
            catch (Exception)
            {
                // Do nothing
            }
        };

        if (!MakeMemoryRequest(request))
        {
            try
            {
                tcs.TrySetResult(new SnesSingleMemoryResponse
                {
                    Successful = false,
                    Data = new SnesData(Array.Empty<byte>())
                });
            }
            catch (Exception)
            {
                // Do nothing
            }
        }

        return tcs.Task;
    }

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

    public void UpdateTimeoutSeconds(int seconds)
    {
        _currentConnector?.UpdateTimeoutSeconds(seconds);
    }

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

        if (e.Request is SnesSingleMemoryRequest && e.Request.OnResponse != null)
        {
            InvokeRequest(e.Request, new SnesData(Array.Empty<byte>()) , null);
        }
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
            _logger?.LogInformation("Getting files from path {Path}", fileListRequest.Path);
            await _currentConnector!.ListFiles(fileListRequest);
        }
        else if (request.RequestType == SnesRequestType.BootRom && request is SnesBootRomRequest bootRomRequest)
        {
            _logger?.LogInformation("Booting rom {Rom}", bootRomRequest.Path);
            await _currentConnector!.BootRom(bootRomRequest);
        }
        else if (request.RequestType == SnesRequestType.UploadFile && request is SnesUploadFileRequest uploadFileRequest)
        {
            _logger?.LogInformation("Uploading file {LocalPath} to {TargetPath}", uploadFileRequest.LocalFilePath, uploadFileRequest.TargetFilePath);
            await _currentConnector!.UploadFile(uploadFileRequest);
        }
        else if (request.RequestType == SnesRequestType.DeleteFile && request is SnesDeleteFileRequest deleteFileRequest)
        {
            _logger?.LogInformation("Deleting file {Path}", deleteFileRequest.Path);
            await _currentConnector!.DeleteFile(deleteFileRequest);
        }
        else if (request.RequestType == SnesRequestType.MakeDirectory &&
                 request is SnesCreateDirectoryRequest createDirectoryRequest)
        {
            _logger?.LogInformation("Creating directory {Path}", createDirectoryRequest.Path);
            await _currentConnector!.CreateDirectory(createDirectoryRequest);
        }
        else if (request.RequestType == SnesRequestType.DeleteDirectory &&
                 request is SnesDeleteDirectoryRequest deleteDirectoryRequest)
        {
            _logger?.LogInformation("Deleting directory {Path}", deleteDirectoryRequest.Path);
            await _currentConnector!.DeleteDirectory(deleteDirectoryRequest);
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
        GameDetected?.Invoke(sender, e);
    }
    
    private void CurrentConnectorOnDirectoryCreated(object sender, SnesResponseEventArgs<SnesCreateDirectoryRequest> e)
    {
        _logger?.LogInformation("{Path} created", e.Request.Path);
        DirectoryCreated?.Invoke(sender, e);
        e.Request.OnComplete?.Invoke();
    }
    
    private void CurrentConnectorOnDirectoryDeleted(object sender, SnesResponseEventArgs<SnesDeleteDirectoryRequest> e)
    {
        _logger?.LogInformation("{Path} deleted", e.Request.Path);
        DirectoryDeleted?.Invoke(sender, e);
        e.Request.OnComplete?.Invoke();
    }

    private string FixDirectory(string path)
    {
        path = path.Replace("\\", "/");
        
        if (!path.StartsWith('/'))
        {
            path = "/" + path;
        }

        if (path.EndsWith('/'))
        {
            path = path[..^1];
        }

        return path;
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