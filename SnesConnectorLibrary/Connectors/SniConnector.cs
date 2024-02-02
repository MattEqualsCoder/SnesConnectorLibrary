using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using SNI;

namespace SnesConnectorLibrary.Connectors;

internal class SniConnector : ISnesConnector
{
    private readonly ILogger<SniConnector>? _logger;
    private string? _address;
    private Devices.DevicesClient? _devices;
    private DeviceMemory.DeviceMemoryClient? _memory;
    private string? _deviceAddress;
    private SnesMemoryRequest? _pendingRequest;
    private DateTime? _lastMessageTime;
    private bool _isEnabled;
    private GrpcChannel? _channel;
    
    public SniConnector()
    {
    }
    
    public SniConnector(ILogger<SniConnector> logger)
    {
        _logger = logger;
    }
    
    public event EventHandler? OnConnected;
    public event EventHandler? OnDisconnected;
    public event SnesDataReceivedEventHandler? OnMessage;
    
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
        
        _channel = GrpcChannel.ForAddress(new Uri(_address));
        _devices = new Devices.DevicesClient(_channel);
        _memory = new DeviceMemory.DeviceMemoryClient(_channel);
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
    
    public void Dispose()
    {
        Disable();
        GC.SuppressFinalize(this);
    }
    
    public async Task GetAddress(SnesMemoryRequest request)
    {
        if (_memory == null)
        {
            _logger?.LogWarning("Attempted to make GetAddress call when not connected");
            return;
        }
        
        _pendingRequest = request;
        _logger?.LogDebug("Making request to device {Device}", _deviceAddress);
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
            
            OnMessage?.Invoke(this, new SnesDataReceivedEventArgs()
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
        }
        catch (Exception e)
        {
            _logger?.LogError(e, "Unable to send message to SNI");
            _ = MarkAsDisconnected();
        }
        
    }

    public bool IsConnected { get; private set; }
    
    public bool CanMakeRequest => IsConnected && !string.IsNullOrEmpty(_deviceAddress) && _pendingRequest == null;

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
                    IsConnected = true;
                    _lastMessageTime = DateTime.Now;
                    _ = MonitorConnection();
                    OnConnected?.Invoke(this, EventArgs.Empty);
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

    public int TranslateAddress(SnesMemoryRequest message) => message.GetTranslatedAddress(AddressFormat.FxPakPro);
    
    private async Task MonitorConnection()
    {
        while (IsConnected)
        {
            if (_lastMessageTime != null && (DateTime.Now - _lastMessageTime.Value).TotalSeconds > 10)
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
            _pendingRequest = null;
            _lastMessageTime = null;
            _deviceAddress = null;
            OnDisconnected?.Invoke(this, EventArgs.Empty);
            
            if (_isEnabled)
            {
                await Task.Delay(TimeSpan.FromSeconds(3));
                _ = GetDevices();
            }
        }
    }
}