using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace SnesConnectorLibrary.Connectors;

public class LuaConnector : ISnesConnector
{
    private readonly ILogger<LuaConnector> _logger;
    private TcpListener? _tcpListener;
    private bool _isEnabled;
    private bool _isBizHawk;
    private Socket? _socket;
    private SnesMemoryRequest? _lastReadMessage;
    private DateTime? _lastMessageTime;

    public LuaConnector(ILogger<LuaConnector> logger)
    {
        _logger = logger;
    }
    
    public void Dispose()
    {
        // TODO release managed resources here
    }

    public event EventHandler? OnConnected;
    public event EventHandler? OnDisconnected;
    public event SnesDataReceivedEventHandler? OnMessage;
    
    public void Connect(SnesConnectorSettings settings)
    {
        if (IsConnected)
        {
            return;
        }

        _isEnabled = true;

        _ = StartSocketServer(settings);
        _ = MonitorSocket();
    }

    public void Disconnect()
    {
        MarkAsDisconnected();
        
        if (_socket?.Connected == true)
        {
            _socket?.Disconnect(true);    
        }

        _tcpListener?.Stop();

        _isEnabled = false;

    }

    public bool IsConnected { get; private set; }
    
    public void GetAddress(SnesMemoryRequest request)
    {
        var msgString = $"Read|{TranslateAddress(request)}|{request.Length}|{GetDomainString(request.SnesMemoryDomain)}\n\0";
        try
        {
            _lastReadMessage = request;
            _socket?.Send(Encoding.ASCII.GetBytes(msgString));
        }
        catch (SocketException ex)
        {
            _logger.LogError(ex, "Error sending message");
            MarkAsDisconnected();
        }
    }

    public async Task PutAddress(SnesMemoryRequest request)
    {
        if (_socket?.Connected != true)
        {
            _logger.LogWarning("Socket is not connected");
            return;
        }
        
        if (request.Data == null)
        {
            throw new InvalidOperationException("No data sent to to PutAddress");
        }
        var data = request.Data.ToArray().Select(x => x.ToString());


        var hex = string.Join("|", data);
        var msgString = _isBizHawk
            ? $"Write|{TranslateAddress(request)}|{GetDomainString(request.SnesMemoryDomain)}|{hex}\n\0"
            : $"Write|{TranslateAddress(request)}|{hex}\n\0";
        
        try
        {
            await _socket.SendAsync(Encoding.ASCII.GetBytes(msgString));
        }
        catch (SocketException ex)
        {
            _logger.LogError(ex, "Error sending message");
            MarkAsDisconnected();
        }
    }

    public bool CanMakeRequest => IsConnected && _lastReadMessage == null && _socket?.Connected == true;

    private async Task StartSocketServer(SnesConnectorSettings settings)
    {
        var address = IPAddress.Loopback;
        var port = 65398;
        if (!string.IsNullOrEmpty(settings.LuaAddress))
        {
            if (settings.LuaAddress.Contains(':'))
            {
                var parts = settings.LuaAddress.Split(":");
                if (!IPAddress.TryParse(parts[0], out address) || !int.TryParse(parts[1], out port))
                {
                    throw new InvalidOperationException($"Invalid Lua address of {settings.LuaAddress}");
                }
            }
            else if (!IPAddress.TryParse(settings.LuaAddress, out address))
            {
                throw new InvalidOperationException($"Invalid Lua address of {settings.LuaAddress}");
            }
        }
        
        _logger.LogInformation("Starting socket server {IP}:{Port}", address.ToString(), port);

        _tcpListener = new TcpListener(address, port);
        _tcpListener.Start();
        while (_isEnabled)
        {
            try
            {
                _socket = await _tcpListener.AcceptSocketAsync();
                _logger.LogInformation("Accepted socket connection");
                if (_socket?.Connected == true)
                {
                    await using var stream = new NetworkStream(_socket);
                    using var reader = new StreamReader(stream);
                    try
                    {
                        _lastReadMessage = null;
                        IsConnected = true;
                        _lastMessageTime = DateTime.Now;
                        _ = Task.Run(async () =>
                        {
                            await Task.Delay(TimeSpan.FromSeconds(1));
                            _socket?.Send("Version\n\0"u8.ToArray());
                        });
                            
                        var line = await reader.ReadLineAsync();
                        while (line != null && _socket?.Connected == true)
                        {
                            _lastMessageTime = DateTime.Now;

                            if (line.StartsWith("Version"))
                            {
                                _logger.LogInformation("Received version from Lua connection: {Version}", line);
                                _isBizHawk = line.Contains("Bizhawk", StringComparison.OrdinalIgnoreCase);
                                OnConnected?.Invoke(this, EventArgs.Empty);
                            }
                            else
                            {
                                var prevRequest = _lastReadMessage!;
                                _lastReadMessage = null;

                                if (line.Trim().StartsWith("{"))
                                {
                                    var result = JsonSerializer.Deserialize<Dictionary<string, List<byte>>>(line);
                                    if (result?.TryGetValue("data", out var data) is true)
                                    {
                                        OnMessage?.Invoke(this, new SnesDataReceivedEventArgs()
                                        {
                                            Request = prevRequest,
                                            Data = new SnesData(prevRequest.Address, data.ToArray())
                                        });
                                    }
                                    
                                }
                                else
                                {
                                    OnMessage?.Invoke(this, new SnesDataReceivedEventArgs()
                                    {
                                        Request = prevRequest,
                                        Data = new SnesData(prevRequest.Address, HexStringToByteArray(line))
                                    });
                                }
                            }
                                
                            line = await reader.ReadLineAsync();
                        }
                            
                        _logger.LogInformation("Socket disconnected");
                        MarkAsDisconnected();
                    }
                    catch (Exception e)
                    {
                        _logger.LogWarning(e, "Error with socket connection");
                        MarkAsDisconnected();
                    }
                }
            }
            catch (Exception e)
            {
                if (_isEnabled)
                {
                    _logger.LogWarning(e, "Error with socket connection");
                }
            }
        }
    }
    
    private string GetDomainString(SnesMemoryDomain domain)
    {
        switch (domain)
        {
            case SnesMemoryDomain.WRAM:
                return "WRAM";
            case SnesMemoryDomain.CartRAM:
                return "CARTRAM";
            case SnesMemoryDomain.CartROM:
                return "CARTROM";
            default:
                return "";
        }
    }
    
    private static byte[] HexStringToByteArray(string hex) {
        return Enumerable.Range(0, hex.Length)
            .Where(x => x % 2 == 0)
            .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
            .ToArray();
    }

    
    private async Task MonitorSocket()
    {
        while (_isEnabled)
        {
            if (_socket != null && _lastMessageTime != null && (DateTime.Now - _lastMessageTime.Value).TotalSeconds > 5)
            {
                MarkAsDisconnected();
                _socket.Close();
                _socket = null;
            }

            await Task.Delay(TimeSpan.FromSeconds(1));
        }
    }

    private void MarkAsDisconnected()
    {
        if (IsConnected)
        {
            IsConnected = false;
            _lastReadMessage = null;
            _lastMessageTime = null;
            OnDisconnected?.Invoke(this, new());
        }
    }
    
    private int TranslateAddress(SnesMemoryRequest message)
    {
        if (!_isBizHawk)
        {
            return message.Address;
        }
        
        if (message.SnesMemoryDomain == SnesMemoryDomain.CartROM)
        {
            return message.Address;
        }
        else if (message.SnesMemoryDomain == SnesMemoryDomain.CartRAM)
        {
            var offset = 0x0;
            var remaining = message.Address - 0xa06000;
            while (remaining >= 0x2000)
            {
                remaining -= 0x10000;
                offset += 0x2000;
            }
            return offset + remaining;
        }
        else if (message.SnesMemoryDomain == SnesMemoryDomain.WRAM)
        {
            return message.Address - 0x7e0000;
        }
        return message.Address;
    }
}