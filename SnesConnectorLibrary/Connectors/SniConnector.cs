using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using SnesConnectorLibrary.Requests;
using SnesConnectorLibrary.Responses;
using SNI;

namespace SnesConnectorLibrary.Connectors;

internal class SniConnector : ISnesConnector
{
    private readonly ILogger<SniConnector>? _logger;
    private string? _address;
    private Devices.DevicesClient? _devices;
    private DeviceMemory.DeviceMemoryClient? _memory;
    private DeviceFilesystem.DeviceFilesystemClient? _filesystem;
    private string? _deviceAddress;
    private SnesRequest? _pendingRequest;
    private DateTime? _lastMessageTime;
    private bool _isEnabled;
    private GrpcChannel? _channel;
    private ConnectorFunctionality _connectorFunctionality;
    private int _timeoutSeconds = 60;
    
    public SniConnector()
    {
    }
    
    public SniConnector(ILogger<SniConnector> logger)
    {
        _logger = logger;
    }
    
    public void Dispose()
    {
        Disable();
        GC.SuppressFinalize(this);
    }
    
    #region Events and properties
    public event EventHandler? Connected;
    public event EventHandler? GameDetected;
    public event EventHandler? Disconnected;
    public event SnesMemoryResponseEventHandler? MemoryReceived;
    public event SnesResponseEventHandler<SnesMemoryRequest>? MemoryUpdated;
    public event SnesFileListResponseEventHandler? FileListReceived;
    public event SnesResponseEventHandler<SnesBootRomRequest>? RomBooted;
    public event SnesResponseEventHandler<SnesUploadFileRequest>? FileUploaded;
    public event SnesResponseEventHandler<SnesDeleteFileRequest>? FileDeleted;
    
    public bool IsConnected { get; private set; }
    public bool IsGameDetected { get; private set; }
    public bool CanProcessRequests => IsConnected && !string.IsNullOrEmpty(_deviceAddress) && _pendingRequest == null;
    public bool CanMakeRequest(SnesRequest request) => IsConnected && !string.IsNullOrEmpty(_deviceAddress) && request.CanPerformRequest(SupportedFunctionality);
    public int TranslateAddress(SnesMemoryRequest message) => message.GetTranslatedAddress(AddressFormat.FxPakPro);
    public ConnectorFunctionality SupportedFunctionality => _connectorFunctionality;
    #endregion

    #region Public methods
    public void Enable(SnesConnectorSettings settings)
    {
        if (IsConnected)
        {
            Disable();    
        }
        
        _isEnabled = true;
        var address = string.IsNullOrEmpty(settings.SniAddress) ? "http://localhost:8191" : settings.SniAddress;
        if (!address.Contains(':'))
        {
            address += ":8191";
        }

        if (!address.StartsWith("http://"))
        {
            address = "http://" + address;
        }

        _address = address;
        _logger?.LogInformation("Attempting to connect to SNI server at {Address}", _address);
        _timeoutSeconds = settings.TimeoutSeconds;
        
        _channel = GrpcChannel.ForAddress(new Uri(_address));
        _devices = new Devices.DevicesClient(_channel);
        _memory = new DeviceMemory.DeviceMemoryClient(_channel);
        _filesystem = new DeviceFilesystem.DeviceFilesystemClient(_channel);
        _ = GetDevices();
    }
    
    public void Disable()
    {
        _channel?.Dispose();
        if (_isEnabled)
        {
            _isEnabled = false;
            _ = MarkAsDisconnected();
        }
    }
    
    public async Task GetAddress(SnesMemoryRequest request)
    {
        if (_memory == null)
        {
            _logger?.LogWarning("Attempted to make GetAddress call when not connected");
            return;
        }
        
        _pendingRequest = request;
        try
        {
            var response = await _memory.SingleReadAsync(new SingleReadMemoryRequest()
            {
                Uri = _deviceAddress,
                Request = new ReadMemoryRequest()
                {
                    RequestAddress = (uint)TranslateAddress(request),
                    RequestAddressSpace = AddressSpace.FxPakPro,
                    RequestMemoryMapping = request.SniMemoryMapping,
                    Size = (uint)request.Length
                }
            }, new CallOptions(deadline: DateTime.UtcNow.AddSeconds(3)));
            
            var bytes = response.Response.Data.ToArray();
            _pendingRequest = null;
            _lastMessageTime = DateTime.Now;
            
            MemoryReceived?.Invoke(this, new SnesMemoryResponseEventArgs()
            {
                Request = request,
                Data = new SnesData(bytes)
            });
        }
        catch (Exception e)
        {
            _logger?.LogError(e, "Unable to send message to SNI");
            _ = MarkAsDisconnected();
        }
    }

    public async Task PutAddress(SnesMemoryRequest request)
    {
        if (_memory == null || request.Data == null)
        {
            _logger?.LogWarning("Invalid PutAddress request");
            return;
        }
        
        _pendingRequest = request;

        try
        {
            await _memory.SingleWriteAsync(new SingleWriteMemoryRequest()
            {
                Uri = _deviceAddress,
                Request = new WriteMemoryRequest()
                {
                    RequestAddress = (uint)TranslateAddress(request),
                    RequestAddressSpace = AddressSpace.FxPakPro,
                    RequestMemoryMapping = request.SniMemoryMapping,
                    Data = ByteString.CopyFrom(request.Data.ToArray())
                }
            }, new CallOptions(deadline: DateTime.UtcNow.AddSeconds(3)));
            
            MemoryUpdated?.Invoke(this, new SnesResponseEventArgs<SnesMemoryRequest>() { Request = request });

            _pendingRequest = null;
        }
        catch (Exception e)
        {
            _logger?.LogError(e, "Unable to send message to SNI");
            _ = MarkAsDisconnected();
        }
        
    }
    
    public async Task ListFiles(SnesFileListRequest request)
    {
        if (_filesystem == null)
        {
            throw new InvalidOperationException("SNI Connector not initialized");
        }

        _pendingRequest = request;
        
        var parentFolderName = request.Path;
        if (parentFolderName.Contains('/'))
        {
            parentFolderName = parentFolderName.Split("/").Last();
        }
        else if (parentFolderName.Contains('\\'))
        {
            parentFolderName = parentFolderName.Split("\\").Last();
        }

        try
        {
            var output = new List<SnesFile>();
            output.AddRange(await ReadDirectory(request, parentFolderName, request.Path));
            _pendingRequest = null;
            FileListReceived?.Invoke(this, new SnesFileListResponseEventArgs()
            {
                Request = request,
                Files = output
            });
        }
        catch (Exception e)
        {
            _logger?.LogError(e, "Unable to get files from device");
            _pendingRequest = null;
        }
    }

    public async Task BootRom(SnesBootRomRequest request)
    {
        if (_filesystem == null)
        {
            throw new InvalidOperationException("SNI Connector not initialized");
        }

        _pendingRequest = request;
        
        await _filesystem.BootFileAsync(new BootFileRequest()
        {
            Uri = _deviceAddress,
            Path = request.Path
        });
        
        RomBooted?.Invoke(this, new SnesResponseEventArgs<SnesBootRomRequest>() { Request = request });

        _pendingRequest = null;
    }

    public async Task UploadFile(SnesUploadFileRequest request)
    {
        if (_filesystem == null)
        {
            throw new InvalidOperationException("SNI Connector not initialized");
        }
        
        _pendingRequest = request;
        
        var data = await ByteString.FromStreamAsync(File.OpenRead(request.LocalFilePath));
        
        await _filesystem.PutFileAsync(new PutFileRequest()
        {
            Uri = _deviceAddress,
            Path = request.TargetFilePath,
            Data = data
        });
        
        FileUploaded?.Invoke(this, new SnesResponseEventArgs<SnesUploadFileRequest>() { Request = request });

        _pendingRequest = null;
    }

    public async Task DeleteFile(SnesDeleteFileRequest request)
    {
        if (_filesystem == null)
        {
            throw new InvalidOperationException("SNI Connector not initialized");
        }
        
        _pendingRequest = request;
        
        await _filesystem.RemoveFileAsync(new RemoveFileRequest()
        {
            Uri = _deviceAddress,
            Path = request.Path,
        });
        
        FileDeleted?.Invoke(this, new SnesResponseEventArgs<SnesDeleteFileRequest>() { Request = request });

        _pendingRequest = null;
    }
    #endregion

    #region Private methods
    private async Task GetDevices()
    {
        if (_devices == null)
        {
            _logger?.LogWarning("SNI Devices is not set");
            return;
        }

        while (_isEnabled)
        {
            try
            {
                var response = await _devices.ListDevicesAsync(new DevicesRequest(),
                    new CallOptions(deadline: DateTime.UtcNow.AddSeconds(3)));

                if (response.Devices.Any())
                {
                    var device = response.Devices.First();
                    _logger?.LogInformation("Connecting to device: {Name}", device.DisplayName);
                    _deviceAddress = device.Uri;
                    
                    _connectorFunctionality.CanReadMemory = device.Capabilities.Contains(DeviceCapability.ReadMemory);
                    _connectorFunctionality.CanReadRom = false; // device.Capabilities.Contains(DeviceCapability.ReadMemory);
                    _connectorFunctionality.CanWriteRom = false; // device.Capabilities.Contains(DeviceCapability.WriteMemory);
                    _connectorFunctionality.CanPerformCommands = device.Capabilities.Contains(DeviceCapability.BootFile);
                    _connectorFunctionality.CanAccessFiles = device.Capabilities.Contains(DeviceCapability.GetFile) && device.Capabilities.Contains(DeviceCapability.PutFile);
                        
                    IsConnected = true;
                    IsGameDetected = true;
                    _lastMessageTime = DateTime.Now;
                    _ = MonitorConnection();
                    Connected?.Invoke(this, EventArgs.Empty);
                    GameDetected?.Invoke(this, EventArgs.Empty);
                    break;
                }
            }
            catch (Exception)
            {
                // Do nothing as SNI is probably not running
            }

            await Task.Delay(TimeSpan.FromSeconds(3));
        }
    }
    
    private async Task<ICollection<SnesFile>> ReadDirectory(SnesFileListRequest request, string currentDirectoryName, string path)
    {
        if (_filesystem == null)
        {
            return Array.Empty<SnesFile>();
        }

        var output = new List<SnesFile>();

        var files = await _filesystem.ReadDirectoryAsync(new ReadDirectoryRequest()
        {
            Uri = _deviceAddress,
            Path = path
        });
        
        foreach (var file in files.Entries)
        {
            if (file.Name.StartsWith('.'))
            {
                continue;
            }
            
            var newFile = new SnesFile()
            {
                FullPath = $"{path}/{file.Name}",
                ParentName = currentDirectoryName,
                Name = file.Name,
                IsFolder = file.Type == DirEntryType.Directory
            };
            
            if (request.Recursive && newFile.IsFolder)
            {
                output.AddRange(await ReadDirectory(request, newFile.Name, newFile.FullPath));
            }

            if (request.SnesFileMatches(newFile))
            {
                output.Add(newFile);
            }
        }

        return output;
    }
    
    private async Task MonitorConnection()
    {
        while (IsConnected)
        {
            if (_lastMessageTime != null && (DateTime.Now - _lastMessageTime.Value).TotalSeconds > _timeoutSeconds)
            {
                _ = MarkAsDisconnected();
                _logger?.LogInformation("Disconnected due to no responses");
                break;
            }

            await Task.Delay(TimeSpan.FromSeconds(1));
        }
    }
    
    private async Task MarkAsDisconnected()
    {
        if (IsConnected)
        {
            IsConnected = false;
            IsGameDetected = false;
            _pendingRequest = null;
            _lastMessageTime = null;
            _deviceAddress = null;
            Disconnected?.Invoke(this, EventArgs.Empty);
            
            if (_isEnabled)
            {
                await Task.Delay(TimeSpan.FromSeconds(3));
                _ = GetDevices();
            }
        }
    }
    #endregion
}