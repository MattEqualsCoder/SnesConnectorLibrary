using System.Diagnostics.CodeAnalysis;
using System.Net.WebSockets;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SnesConnectorLibrary.Requests;
using SnesConnectorLibrary.Responses;
using SNI;
using Websocket.Client;

namespace SnesConnectorLibrary.Connectors;

internal class Usb2SnesConnector : ISnesConnector
{
    private const string SendClientNameOpCode = "Name";
    private const string DeviceInfoOpCode = "Info";
    private const string DeviceListOpCode = "DeviceList";
    private const string AttachDeviceOpCode = "Attach";
    private const string FileListOpCode = "List";
    private const string GetAddressOpCode = "GetAddress";
    private const string PutAddressOpCode = "PutAddress";
    private const string BootOpCode = "Boot";
    private const string RemoveOpCode = "Remove";
    private const string PutFileOpCode = "PutFile";
    
    private readonly ILogger<Usb2SnesConnector>? _logger;
    private WebsocketClient? _client;
    private bool _hasReceivedMessage;
    private string _clientName = "SnesConnectorLibrary";
    private SnesRequest? _pendingRequest;
    private readonly List<byte> _bytes = new();
    private ConnectorFunctionality _supportedFunctionality;
    private string? _currentOpCode;
    private SnesFile? _currentFileListFolder;
    private List<SnesFile> _pendingListFolders = new();
    private List<SnesFile> _foundListFiles = new();
    private string _prevInfo = "";

    public Usb2SnesConnector()
    {
    }
    
    public Usb2SnesConnector(ILogger<Usb2SnesConnector> logger)
    {
        _logger = logger;
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
    public bool CanProcessRequests => IsConnected && _pendingRequest == null;
    public int TranslateAddress(SnesMemoryRequest message) => message.GetTranslatedAddress(AddressFormat.FxPakPro);
    public ConnectorFunctionality SupportedFunctionality => _supportedFunctionality;
    #endregion

    #region Public methods
    public void Enable(SnesConnectorSettings settings)
    {
        _clientName = settings.ClientName;
        
        if (IsConnected)
        {
            Disable();    
        }

        var address = string.IsNullOrEmpty(settings.Usb2SnesAddress) ? "localhost:8080" : settings.Usb2SnesAddress;
        if (!address.Contains(':'))
        {
            address += ":8080";
        }

        if (!address.StartsWith("ws://"))
        {
            address = "ws://" + address;
        }
        _logger?.LogInformation("Attempting to connect to usb2snes server at {Address}", address);
        
        var factory = new Func<ClientWebSocket>(() => new ClientWebSocket
        {
            Options =
            {
                KeepAliveInterval = TimeSpan.Zero,
                
            }
        });
        
        _client = new WebsocketClient(new Uri(address), factory);
        _client.ErrorReconnectTimeout = TimeSpan.FromSeconds(5);
        _client.ReconnectTimeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
        
        _client.ReconnectionHappened.Subscribe(OnClientConnected);
        _client.DisconnectionHappened.Subscribe(OnClientDisconnected);
        _client.MessageReceived.Subscribe(OnClientMessage);

        _client.Start();
    }
    
    public void Disable()
    {
        _client?.Dispose();
        _client = null;
        IsConnected = false;
    }

    public void Dispose()
    {
        Disable();
        GC.SuppressFinalize(this);
    }
    
    public async Task GetAddress(SnesMemoryRequest request)
    {
        if (_client == null)
        {
            return;
        }

        _pendingRequest = request;
        var address = TranslateAddress(request).ToString("X");
        var length = request.Length.ToString("X");
        _bytes.Clear();

        await Send(new Usb2SnesRequest()
        {
            Opcode = GetAddressOpCode,
            Space = "SNES",
            Operands = new List<string>() { address, length }
        });
    }

    public async Task PutAddress(SnesMemoryRequest request)
    {
        if (_client == null)
        {
            return;
        }

        if (request.Data == null)
        {
            throw new NullReferenceException("SnesMemoryRequest.Data is null");
        }
        
        _pendingRequest = request;
        
        var address = TranslateAddress(request).ToString("X");
        var length = request.Data.Count.ToString("X");

        if (!await Send(new Usb2SnesRequest()
            {
                Opcode = PutAddressOpCode,
                Space = "SNES",
                Operands = new List<string>() { address, length }
            }))
        {
            return;
        }

        await Task.Delay(TimeSpan.FromMilliseconds(100));

        if (!await Send(request.Data.ToArray()))
        {
            return;
        }
            
        MemoryUpdated?.Invoke(this, new SnesResponseEventArgs<SnesMemoryRequest>() { Request = request });

        _pendingRequest = null;
    }

    public bool CanMakeRequest(SnesRequest request)
    {
        if (!IsConnected || !request.CanPerformRequest(SupportedFunctionality))
        {
            return false;
        }
        
        switch (request.RequestType)
        {
            case SnesRequestType.Memory:
                return IsGameDetected;
            case SnesRequestType.GetFileList:
            case SnesRequestType.UploadFile:
            case SnesRequestType.BootRom:
            case SnesRequestType.DeleteFile:
                return IsConnected;
        }

        return false;
    }

    public async Task ListFiles(SnesFileListRequest request)
    {
        _pendingRequest = request;
        _logger?.LogInformation("Send SNES file list");

        var parentFolderName = request.Path;
        if (parentFolderName.Contains('/'))
        {
            parentFolderName = parentFolderName.Split("/").Last();
        }
        else if (parentFolderName.Contains('\\'))
        {
            parentFolderName = parentFolderName.Split("\\").Last();
        }

        _currentFileListFolder = new SnesFile()
        {
            ParentName = "",
            Name = parentFolderName,
            FullPath = request.Path,
            IsFolder = true,
        };
        
        _foundListFiles.Clear();
        _pendingListFolders.Clear();
        await Send(new Usb2SnesRequest()
        {
            Opcode = FileListOpCode,
            Space = "SNES",
            Operands = new List<string>() { request.Path }
        });
    }

    public async Task BootRom(SnesBootRomRequest request)
    {
        _pendingRequest = request;

        if (!await Send(new Usb2SnesRequest()
            {
                Opcode = BootOpCode,
                Space = "SNES",
                Operands = new List<string>() { request.Path }
            }))
        {
            return;
        }

        _pendingRequest = null;
        RomBooted?.Invoke(this, new SnesResponseEventArgs<SnesBootRomRequest>() { Request = request });
        
    }

    public async Task UploadFile(SnesUploadFileRequest request)
    {
        _pendingRequest = request;

        var file = new FileInfo(request.LocalFilePath);
        var length = file.Length.ToString("X");
        var numChunks = file.Length / 4096.0;

        if (!await Send(new Usb2SnesRequest()
            {
                Opcode = PutFileOpCode,
                Space = "SNES",
                Operands = new List<string>() { request.TargetFilePath, length }
            }))
        {
            return;
        }
        
        var stream = file.OpenRead();
        var bytes = new byte[4096];
        var chunk = 1;
        while(true)
        {
            _logger?.LogInformation("Chunk {Num} of {Total}", chunk, numChunks);
            chunk++;
            var numBytes = await stream.ReadAsync(bytes);
            if (numBytes == 0)
                break;
            if (!await Send(bytes.Take(numBytes).ToArray()))
            {
                break;
            }
        }

        await Task.Delay(TimeSpan.FromSeconds(5));
        
        FileUploaded?.Invoke(this, new SnesResponseEventArgs<SnesUploadFileRequest>() { Request = request });
        _pendingRequest = null;
    }

    public async Task DeleteFile(SnesDeleteFileRequest request)
    {
        _pendingRequest = request;

        if (!await Send(new Usb2SnesRequest()
            {
                Opcode = RemoveOpCode,
                Space = "SNES",
                Operands = new List<string>() { request.Path }
            }))
        {
            return;
        }
        
        FileDeleted?.Invoke(this, new SnesResponseEventArgs<SnesDeleteFileRequest>() { Request = request });
        _pendingRequest = null;
    }
    #endregion
    
    #region Private methods
    private void OnClientConnected(ReconnectionInfo info)
    {
        _logger?.LogInformation("Client connected: {Type}", info.Type);
        _pendingRequest = null;
        _hasReceivedMessage = false;
        IsGameDetected = false;
        
        _ = Send(new Usb2SnesRequest()
        {
            Opcode = DeviceListOpCode,
            Space = "SNES"
        });
    }
    
    private void OnClientDisconnected(DisconnectionInfo info)
    {
        if (IsConnected)
        {
            _logger?.LogInformation("Client disconnected: {Type}", info.Type);
            IsConnected = false;
            Disconnected?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnClientMessage(ResponseMessage msg)
    {
        if (msg.MessageType == WebSocketMessageType.Text)
        {
            if (!string.IsNullOrEmpty(msg.Text))
            {
                if (_currentOpCode == DeviceListOpCode)
                {
                    _ = AttachToDevice(msg.Text.Trim());    
                }
                else if (_currentOpCode == DeviceInfoOpCode)
                {
                    _ = ParseDeviceInfo(msg.Text.Trim());
                }
                else if (_currentOpCode == FileListOpCode)
                {
                    _ = ParseFileList(msg.Text.Trim());
                }
            }
        }
        else if (msg is { MessageType: WebSocketMessageType.Binary, Binary: not null } && _pendingRequest is SnesMemoryRequest memoryRequest)
        {
            _bytes.AddRange(msg.Binary);

            if (_bytes.Count >= memoryRequest?.Length)
            {
                var request = _pendingRequest!;
                _pendingRequest = null;

                if (!_hasReceivedMessage)
                {
                    IsGameDetected = true;
                    GameDetected?.Invoke(this, EventArgs.Empty);
                    _hasReceivedMessage = true;
                }
                else
                {
                    MemoryReceived?.Invoke(this, new SnesMemoryResponseEventArgs()
                    {
                        Request = memoryRequest,
                        Data = new SnesData(_bytes.ToArray())
                    });
                }
            }
        }
        else
        {
            _logger?.LogInformation("Other");
        }
    }
    
    private async Task AttachToDevice(string message)
    {
        var response = JsonSerializer.Deserialize<Usb2SnesDeviceListResponse>(message);
        if (response?.Results == null)
        {
            _logger?.LogError("Invalid json response {Text}", message);
            return;
        }

        if (response.Results.Count == 0)
        {
            _logger?.LogInformation("No devices found");
            await Task.Delay(TimeSpan.FromSeconds(3));
            _ = Send(new Usb2SnesRequest()
            {
                Opcode = DeviceListOpCode,
                Space = "SNES"
            });
            return;
        }
        
        _logger?.LogInformation("Possible devices: {Devices}", string.Join(", ", response.Results));
            
        _logger?.LogInformation("Connecting to USB2SNES device {Device} as {ClientName}", response.Results.First(), _clientName);

        if (!await Send(new Usb2SnesRequest()
            {
                Opcode = AttachDeviceOpCode,
                Space = "SNES",
                Operands = new List<string> { response.Results.First() }
            }))
        {
            return;
        }
        
        await Task.Delay(TimeSpan.FromMilliseconds(100));

        if (!await Send(new Usb2SnesRequest()
            {
                Opcode = SendClientNameOpCode,
                Space = "SNES",
                Operands = new List<string> { _clientName }
            }))
        {
            return;
        }

        await Task.Delay(TimeSpan.FromMilliseconds(100));

        if (!await Send(new Usb2SnesRequest()
            {
                Opcode = DeviceInfoOpCode,
                Space = "SNES"
            }))
        {
            return;
        }
    }

    private async Task<bool> Send(Usb2SnesRequest request)
    {
        if (_client?.IsRunning != true)
        {
            _logger?.LogWarning("Attempted to send request when client is not connected");
            return false;
        }
        
        _currentOpCode = request.Opcode;

        try
        {
            await _client!.SendInstant(JsonSerializer.Serialize(request));
            return true;
        }
        catch (Exception e)
        {
            _logger?.LogError(e, "Error sending message to USB2SNES");
            IsConnected = false;
            _pendingRequest = null;
            Disconnected?.Invoke(this, EventArgs.Empty);
            _ = _client?.Reconnect();
            return false;
        }
    }
    
    private async Task<bool> Send(byte[] bytes)
    {
        if (_client?.IsRunning != true)
        {
            _logger?.LogWarning("Attempted to send request when client is not connected");
            return false;
        }
        
        try
        {
            await _client!.SendInstant(bytes);
            return true;
        }
        catch (Exception e)
        {
            _logger?.LogError(e, "Error sending bytes to USB2SNES");
            IsConnected = false;
            _pendingRequest = null;
            Disconnected?.Invoke(this, EventArgs.Empty);
            _ = _client?.Reconnect();
            return false;
        }
    }

    private async Task ParseDeviceInfo(string message)
    {
        var response = JsonSerializer.Deserialize<Usb2SnesDeviceListResponse>(message);

        if (response?.Results == null || response.Results.Count == 0)
        {
            _logger?.LogError("Invalid response from USB2SNES: {Message}", message);
            return;
        }

        var infoString = string.Join(", ", response.Results);
        if (_prevInfo != infoString)
        {
            _prevInfo = infoString;
            _logger?.LogInformation("usb2snes info: {Message}", infoString);
        }
        
        var rom = response?.Results?.Skip(2).FirstOrDefault();
        
        var items = response?.Results ?? new List<string>();
        _supportedFunctionality.CanReadMemory = true;
        _supportedFunctionality.CanAccessFiles = !items.Contains("NO_FILE_CMD");
        _supportedFunctionality.CanPerformCommands = !items.Contains("NO_CONTROL_CMD");
        _supportedFunctionality.CanReadRom = !items.Contains("NO_ROM_READ");
        _supportedFunctionality.CanWriteRom = !items.Contains("NO_ROM_WRITE");

        if (!IsConnected)
        {
            IsConnected = true;
            Connected?.Invoke(this, EventArgs.Empty);
        }

        if (rom == "No Info" || rom?.EndsWith(".bin") == true)
        {
            do
            {
                await Task.Delay(TimeSpan.FromMilliseconds(2000));
            } while (_pendingRequest != null);

            if (_client?.IsRunning == true)
            {
                _ = Send(new Usb2SnesRequest()
                {
                    Opcode = DeviceInfoOpCode,
                    Space = "SNES"
                });
            }

            return;
        }
        
        await Task.Delay(TimeSpan.FromSeconds(1));
        
        await GetAddress(new SnesSingleMemoryRequest()
        {
            MemoryRequestType = SnesMemoryRequestType.RetrieveMemory,
            Address = 0x7e0020,
            Length = 1,
            SnesMemoryDomain = SnesMemoryDomain.ConsoleRAM,
            AddressFormat = AddressFormat.Snes9x,
            SniMemoryMapping = MemoryMapping.Unknown
        });
    }

    private async Task ParseFileList(string message)
    {
        var response = JsonSerializer.Deserialize<Usb2SnesDeviceListResponse>(message);

        if (response?.Results == null || response.Results.Count == 0)
        {
            _logger?.LogError("Invalid response from USB2SNES: {Message}", message);
            return;
        }
        
        if (_pendingRequest is not SnesFileListRequest request || _currentFileListFolder == null)
        {
            _logger?.LogError("Invalid request for ParseFileList");
            return;
        }
        
        for (var i = 0; i < response.Results.Count; i += 2)
        {
            var isFolder = response.Results[i] == "0";
            var name = response.Results[i + 1];

            if (name.StartsWith('.'))
            {
                continue;
            }

            var newFile = new SnesFile()
            {
                FullPath = $"{_currentFileListFolder.FullPath}/{name}",
                ParentName = _currentFileListFolder.Name,
                Name = name,
                IsFolder = isFolder
            };

            if (request.Recursive && isFolder)
            {
                _pendingListFolders.Add(newFile);
            }

            if (request.SnesFileMatches(newFile))
            {
                _foundListFiles.Add(newFile);    
            }
        }

        if (_pendingListFolders.Count > 0)
        {
            _currentFileListFolder = _pendingListFolders[0];
            _pendingListFolders.Remove(_currentFileListFolder);
            await Send(new Usb2SnesRequest()
            {
                Opcode = FileListOpCode,
                Space = "SNES",
                Operands = new List<string>() { _currentFileListFolder.FullPath }
            });
        }
        else
        {
            _pendingRequest = null;
            FileListReceived?.Invoke(this, new SnesFileListResponseEventArgs()
            {
                Request = request,
                Files = _foundListFiles
            });
            _currentFileListFolder = null;
            _pendingListFolders.Clear();
            _foundListFiles.Clear();
        }
    }
    #endregion

    #region Classes
    class Usb2SnesDeviceListResponse
    {
        public List<string>? Results { get; set; }
    }
    
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local")]
    class Usb2SnesRequest
    {
        /// <summary>
        /// The type of request being sent to USB2SNES
        /// </summary>
        public string? Opcode { get; set; }
        /// <summary>
        /// Where to get the data from in USB2SNES (always should be SNES probably?)
        /// </summary>
        public string? Space { get; set; }
        /// <summary>
        /// Parameters for the request
        /// </summary>
        public ICollection<string>? Operands { get; set; }
    }
    #endregion
}