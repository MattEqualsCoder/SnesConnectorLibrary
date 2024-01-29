using System.Net.WebSockets;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Websocket.Client;

namespace SnesConnectorLibrary.Connectors;

public class Usb2SnesConnector : ISnesConnector
{
    private readonly ILogger<Usb2SnesConnector> _logger;
    private WebsocketClient? _client;
    private bool _hasReceivedMessage;
    private string _clientName = "SnesConnectorLibrary";
    private SnesMemoryRequest? _pendingRequest;
    private ConnectionStep _connectionStep = ConnectionStep.DeviceList;

    public Usb2SnesConnector(ILogger<Usb2SnesConnector> logger)
    {
        _logger = logger;
    }

    public bool IsConnected { get; private set; }

    public bool CanMakeRequest => IsConnected && _pendingRequest == null;

    public event EventHandler? OnConnected;
    public event EventHandler? OnDisconnected;
    public event SnesDataReceivedEventHandler? OnMessage;
    
    public void Connect(SnesConnectorSettings settings)
    {
        _clientName = settings.ClientName;
        
        if (IsConnected)
        {
            Disconnect();    
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
        _logger.LogInformation("Attempting to connect to usb2snes server at {Address}", address);
        
        _client = new WebsocketClient(new Uri(address));
        _client.ErrorReconnectTimeout = TimeSpan.FromSeconds(5);
        _client.ReconnectTimeout = TimeSpan.FromSeconds(5);
        
        _client.ReconnectionHappened.Subscribe(OnClientConnected);
        _client.DisconnectionHappened.Subscribe(OnClientDisconnected);
        _client.MessageReceived.Subscribe(OnClientMessage);

        _client.Start();
    }
    
    public void Disconnect()
    {
        _client?.Dispose();
        _client = null;
        IsConnected = false;
    }

    public void Dispose()
    {
        _client?.Dispose();
        _client = null;
        IsConnected = false;
    }

    private void OnClientConnected(ReconnectionInfo info)
    {
        _logger.LogInformation("Client connected: {Type}", info.Type);
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
            _logger.LogInformation("Client disconnected: {Type}", info.Type);
            IsConnected = false;
            OnDisconnected?.Invoke(this, EventArgs.Empty);
        }
    }

    private int _currentByteCount = 0;
    private List<byte> _bytes = new List<byte>();

    private void OnClientMessage(ResponseMessage msg)
    {
        if (msg.MessageType == WebSocketMessageType.Text)
        {
            _logger.LogInformation("Receiving text data: {Text}", msg.Text?.Trim());
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
            _currentByteCount = _currentByteCount + msg.Binary.Length;
            _bytes.AddRange(msg.Binary);

            if (_currentByteCount >= _pendingRequest.Length)
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
                        Data = new SnesData(request.Address, _bytes.ToArray())
                    });
                }
            }
        }
        else
        {
            _logger.LogInformation("Other");
        }
    }

    public void GetAddress(SnesMemoryRequest request)
    {
        if (_client == null)
        {
            return;
        }

        _pendingRequest = request;
        var address = TranslateAddress(request).ToString("X");
        var length = request.Length.ToString("X");
        _currentByteCount = 0;
        _bytes.Clear();

        _logger.LogInformation("Sending request for memory location {Address} of {Length}", address, length);
        _client.Send(JsonSerializer.Serialize(new Usb2SnesRequest()
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
        
        _logger.LogInformation("PutAddress Send");
        
        _client.Send(JsonSerializer.Serialize(new Usb2SnesRequest()
        {
            Opcode = "PutAddress",
            Space = "SNES",
            Operands = new List<string>() { address, length }
        }));
        
        await Task.Delay(TimeSpan.FromMilliseconds(50));
        
        _client.Send(request.Data.ToArray());

        _pendingRequest = null;
    }

    private async Task AttachToDevice(string message)
    {
        var response = JsonSerializer.Deserialize<Usb2SnesDeviceListResponse>(message);
        if (response?.Results == null)
        {
            _logger.LogError("Invalid json response {Text}", message);
            return;
        }

        if (response.Results.Count == 0)
        {
            _logger.LogInformation("No devices found");
            return;
        }
            
        _logger.LogInformation("Connecting to USB2SNES device {Device} as {ClientName}", response.Results.First(), _clientName);
            
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
        _logger.LogInformation("Requested info");
    }

    private async Task ParseDeviceInfo(string message)
    {
        
        _logger.LogInformation("usb2snes info: {Message}", message);
        
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
        
        await Task.Delay(TimeSpan.FromMilliseconds(3000));
        
        _logger.LogInformation("usb2snes rom: {Message}", rom);
        GetAddress(new SnesMemoryRequest()
        {
            RequestType = SnesMemoryRequestType.GetAddress,
            Address = 0x7e0020,
            Length = 1,
            SnesMemoryDomain = SnesMemoryDomain.Memory
        });
    }

    private int TranslateAddress(SnesMemoryRequest message)
    {
        if (message.SnesMemoryDomain == SnesMemoryDomain.Rom)
        {
            return message.Address;
        }
        else if (message.SnesMemoryDomain == SnesMemoryDomain.SaveRam)
        {
            var offset = 0x0;
            var remaining = message.Address - 0xa06000;
            while (remaining >= 0x2000)
            {
                remaining -= 0x10000;
                offset += 0x2000;
            }
            return 0xE00000 + offset + remaining;
        }
        else if (message.SnesMemoryDomain == SnesMemoryDomain.Memory)
        {
            return message.Address + 0x770000;
        }
        return message.Address;
    }

    enum ConnectionStep
    {
        DeviceList,
        Info
    }
}