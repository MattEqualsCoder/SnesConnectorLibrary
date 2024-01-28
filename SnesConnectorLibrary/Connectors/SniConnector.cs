using System.Runtime.InteropServices;
using System.Xml;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using SNI;

namespace SnesConnectorLibrary.Connectors;

public class SniConnector : ISnesConnector
{
    private ILogger<SniConnector> _logger;
    private string? _address;
    private Devices.DevicesClient? _devices;
    private DeviceMemory.DeviceMemoryClient? _memory;
    private string? _deviceAddress;
    private SnesMemoryRequest? _pendingRequest;
    private DateTime? _lastMessageTime;
    private bool _isEnabled;
    private GrpcChannel? _channel;
    
    public SniConnector(ILogger<SniConnector> logger)
    {
        _logger = logger;
    }
    
    public void Dispose()
    {
        _channel?.Dispose();
        _isEnabled = false;
    }

    public event EventHandler? OnConnected;
    public event EventHandler? OnDisconnected;
    public event SnesDataReceivedEventHandler? OnMessage;
    
    public void Connect(SnesConnectorSettings settings)
    {
        if (IsConnected)
        {
            Disconnect();    
        }

        _isEnabled = true;
        var address = string.IsNullOrEmpty(settings.Usb2SnesAddress) ? "http://localhost:8191" : settings.Usb2SnesAddress;
        if (!address.Contains(':'))
        {
            address += ":8191";
        }

        if (!address.StartsWith("http://"))
        {
            address = "http://" + address;
        }

        _address = address;
        _logger.LogInformation("Attempting to connect to SNI server at {Address}", _address);
        
        _channel = GrpcChannel.ForAddress(new Uri(_address));
        _devices = new Devices.DevicesClient(_channel);
        _memory = new DeviceMemory.DeviceMemoryClient(_channel);
        _ = GetDevices();
    }
    
    public void Disconnect()
    {
        _isEnabled = false;
        _channel?.Dispose();
        _ = MarkAsDisconnected();
    }

    public bool IsConnected { get; private set; }
    
    public void GetAddress(SnesMemoryRequest request)
    {
        _pendingRequest = request;
        _logger.LogInformation("Making request to device {Device}", _deviceAddress);
        try
        {
            var response = _memory.SingleRead(new SingleReadMemoryRequest()
            {
                Uri = _deviceAddress,
                Request = new ReadMemoryRequest()
                {
                    RequestAddress = TranslateAddress(request),
                    RequestAddressSpace = AddressSpace.FxPakPro,
                    RequestMemoryMapping = MemoryMapping.ExHiRom,
                    Size = (uint)request.Length
                }
            }, new CallOptions(deadline: DateTime.UtcNow.AddSeconds(3)));
            var bytes = response.Response.Data.ToArray();
            _pendingRequest = null;
            _lastMessageTime = DateTime.Now;
            ;
            OnMessage?.Invoke(this, new SnesDataReceivedEventArgs()
            {
                Request = request,
                Data = new SnesData(request.Address, bytes)
            });
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Unable to send message to SNI");
            _ = MarkAsDisconnected();
        }
    }

    public async Task PutAddress(SnesMemoryRequest request)
    {
        if (_memory == null || request.Data == null)
        {
            _logger.LogWarning("Invalid PutAddress request");
            return;
        }

        try
        {
            var response = await _memory.SingleWriteAsync(new SingleWriteMemoryRequest()
            {
                Uri = _deviceAddress,
                Request = new WriteMemoryRequest()
                {
                    RequestAddress = TranslateAddress(request),
                    RequestAddressSpace = AddressSpace.FxPakPro,
                    RequestMemoryMapping = MemoryMapping.ExHiRom,
                    Data = ByteString.CopyFrom(request.Data.ToArray())
                }
            }, new CallOptions(deadline: DateTime.UtcNow.AddSeconds(3)));
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Unable to send message to SNI");
            _ = MarkAsDisconnected();
        }
        
    }

    public bool CanMakeRequest => IsConnected && !string.IsNullOrEmpty(_deviceAddress) && _pendingRequest == null;

    private async Task GetDevices()
    {
        if (_devices == null)
        {
            _logger.LogWarning("SNI Devices is not set");
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
                    _logger.LogInformation("Connecting to device: {Name}", device.DisplayName);
                    _deviceAddress = device.Uri;
                    IsConnected = true;
                    _lastMessageTime = DateTime.Now;
                    _ = MonitorConnection();
                    OnConnected?.Invoke(this, EventArgs.Empty);
                    break;
                }
            }
            catch (Exception e)
            {
                // Do nothing as SNI is probably not running
            }

            await Task.Delay(TimeSpan.FromSeconds(3));
        }
    }
    
    private uint TranslateAddress(SnesMemoryRequest message)
    {
        if (message.SnesMemoryDomain == SnesMemoryDomain.Rom)
        {
            return (uint)message.Address;
        }
        else if (message.SnesMemoryDomain == SnesMemoryDomain.SaveRam)
        {
            return (uint)message.Address - 0xa06000 + 0xE00000;
        }
        else if (message.SnesMemoryDomain == SnesMemoryDomain.Memory)
        {
            return (uint)message.Address - 0x7E0000 + 0xF50000;
        }
        return (uint)message.Address;
    }
    
    private async Task MonitorConnection()
    {
        while (IsConnected)
        {
            if (_lastMessageTime != null && (DateTime.Now - _lastMessageTime.Value).TotalSeconds > 10)
            {
                _ = MarkAsDisconnected();
                _logger.LogInformation("Disconnected due to no responses");
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