using System.Diagnostics.CodeAnalysis;
using System.Net.WebSockets;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SNI;
using Websocket.Client;

namespace SnesConnectorLibrary.Connectors;

internal class Usb2SnesConnector : ISnesConnector
{
    private readonly ILogger<Usb2SnesConnector>? _logger;
    private WebsocketClient? _client;
    private bool _hasReceivedMessage;
    private string _clientName = "SnesConnectorLibrary";
    private SnesMemoryRequest? _pendingRequest;
    private ConnectionStep _connectionStep = ConnectionStep.DeviceList;
    private readonly List<byte> _bytes = new();

    public Usb2SnesConnector()
    {
    }
    
    public Usb2SnesConnector(ILogger<Usb2SnesConnector> logger)
    {
        _logger = logger;
    }

    public event EventHandler? OnConnected;
    public event EventHandler? OnDisconnected;
    public event SnesDataReceivedEventHandler? OnMessage;
    
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
        
        _client = new WebsocketClient(new Uri(address));
        _client.ErrorReconnectTimeout = TimeSpan.FromSeconds(5);
        _client.ReconnectTimeout = TimeSpan.FromSeconds(5);
        
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
    
    public bool IsConnected { get; private set; }

    public bool CanMakeRequest => IsConnected && _pendingRequest == null;

    private void OnClientConnected(ReconnectionInfo info)
    {
        _logger?.LogInformation("Client connected: {Type}", info.Type);
        _pendingRequest = null;
        _hasReceivedMessage = false;
        _connectionStep = ConnectionStep.DeviceList;
        
        _client!.SendInstant(JsonSerializer.Serialize(new Usb2SnesRequest()
        {
            Opcode = "DeviceList",
            Space = "SNES"
        }));
    }
    
    private void OnClientDisconnected(DisconnectionInfo info)
    {
        if (IsConnected)
        {
            _logger?.LogInformation("Client disconnected: {Type}", info.Type);
            IsConnected = false;
            OnDisconnected?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnClientMessage(ResponseMessage msg)
    {
        if (msg.MessageType == WebSocketMessageType.Text)
        {
            if (!string.IsNullOrEmpty(msg.Text))
            {
                if (_connectionStep == ConnectionStep.DeviceList)
                {
                    _ = AttachToDevice(msg.Text.Trim());    
                }
                else if (_connectionStep == ConnectionStep.Info)
                {
                    _ = ParseDeviceInfo(msg.Text.Trim());
                }
            }
        }
        else if (msg is { MessageType: WebSocketMessageType.Binary, Binary: not null })
        {
            _bytes.AddRange(msg.Binary);

            if (_bytes.Count >= _pendingRequest?.Length)
            {
                var request = _pendingRequest!;
                _pendingRequest = null;

                if (!_hasReceivedMessage)
                {
                    IsConnected = true;
                    OnConnected?.Invoke(this, EventArgs.Empty);
                    _hasReceivedMessage = true;
                }
                else
                {
                    OnMessage?.Invoke(this, new SnesDataReceivedEventArgs()
                    {
                        Request = request,
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

        _logger?.LogDebug("Sending request for memory location {Address} of {Length}", address, length);
        await _client.SendInstant(JsonSerializer.Serialize(new Usb2SnesRequest()
        {
            Opcode = "GetAddress",
            Space = "SNES",
            Operands = new List<string>() { address, length }
        }));
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
        
        await _client.SendInstant(JsonSerializer.Serialize(new Usb2SnesRequest()
        {
            Opcode = "PutAddress",
            Space = "SNES",
            Operands = new List<string>() { address, length }
        }));
        
        await Task.Delay(TimeSpan.FromMilliseconds(50));
        
        await _client.SendInstant(request.Data.ToArray());

        _pendingRequest = null;
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
            return;
        }
            
        _logger?.LogInformation("Connecting to USB2SNES device {Device} as {ClientName}", response.Results.First(), _clientName);
            
        await _client!.SendInstant(JsonSerializer.Serialize(new Usb2SnesRequest()
        {
            Opcode = "Attach",
            Space = "SNES",
            Operands = new List<string> { response.Results.First() }
        }));
        
        await Task.Delay(TimeSpan.FromMilliseconds(500));

        await _client.SendInstant(JsonSerializer.Serialize(new Usb2SnesRequest()
        {
            Opcode = "Name",
            Space = "SNES",
            Operands = new List<string> { _clientName }
        }));

        await Task.Delay(TimeSpan.FromMilliseconds(500));

        _connectionStep = ConnectionStep.Info;
        
        await _client.SendInstant(JsonSerializer.Serialize(new Usb2SnesRequest()
        {
            Opcode = "Info",
            Space = "SNES"
        }));
        _logger?.LogInformation("Requested info from usb2snes");
    }

    private async Task ParseDeviceInfo(string message)
    {
        _logger?.LogInformation("usb2snes info: {Message}", message);
        
        var response = JsonSerializer.Deserialize<Usb2SnesDeviceListResponse>(message);
        var rom = response?.Results?.Skip(2).FirstOrDefault();

        if (rom == "No Info" || rom?.EndsWith(".bin") == true)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(2000));

            if (_client?.IsRunning == true)
            {
                await _client!.SendInstant(JsonSerializer.Serialize(new Usb2SnesRequest()
                {
                    Opcode = "Info",
                    Space = "SNES"
                }));
            }

            return;
        }
        
        await Task.Delay(TimeSpan.FromSeconds(1));
        
        await GetAddress(new SnesMemoryRequest()
        {
            RequestType = SnesMemoryRequestType.Retrieve,
            Address = 0x7e0020,
            Length = 1,
            SnesMemoryDomain = SnesMemoryDomain.ConsoleRAM,
            AddressFormat = AddressFormat.Snes9x,
            SniMemoryMapping = MemoryMapping.Unknown
        });
    }

    public int TranslateAddress(SnesMemoryRequest message) => message.GetTranslatedAddress(AddressFormat.FxPakPro);

    enum ConnectionStep
    {
        DeviceList,
        Info
    }
    
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
}