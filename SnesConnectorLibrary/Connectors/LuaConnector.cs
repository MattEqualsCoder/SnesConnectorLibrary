using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace SnesConnectorLibrary.Connectors;

internal abstract class LuaConnector : ISnesConnector
{
    protected readonly ILogger<LuaConnector> Logger;
    protected bool IsEnabled;
    protected bool IsBizHawk;
    protected Socket? Socket;
    protected SnesMemoryRequest? CurrentRequest;
    private TcpListener? _tcpListener;
    private DateTime? _lastMessageTime;

    protected LuaConnector(ILogger<LuaConnector> logger)
    {
        Logger = logger;
    }
    
    public event EventHandler? OnConnected;
    public event EventHandler? OnDisconnected;
    public event SnesDataReceivedEventHandler? OnMessage;
    
    public void Enable(SnesConnectorSettings settings)
    {
        if (IsConnected)
        {
            return;
        }

        IsEnabled = true;

        _ = StartSocketServer(settings);
        _ = MonitorSocket();
    }

    public void Disable()
    {
        IsEnabled = false;
        MarkAsDisconnected();
        _tcpListener?.Stop();
    }
    
    public void Dispose()
    {
        IsConnected = false;
        Disable();
        GC.SuppressFinalize(this);
    }
    
    public abstract Task GetAddress(SnesMemoryRequest request);

    public abstract Task PutAddress(SnesMemoryRequest request);

    public bool IsConnected { get; private set; }
    
    public bool CanMakeRequest => IsConnected && CurrentRequest == null && Socket?.Connected == true;

    protected abstract int GetDefaultPort();
    
    protected abstract Task SendInitialMessage();
    
    protected abstract void ProcessLine(string line, SnesMemoryRequest? request);

    protected abstract Task<string?> ReadNextLine(StreamReader reader);

    protected void MarkConnected()
    {
        Logger.LogInformation("Connected!");
        CurrentRequest = null;
        IsConnected = true;
        _lastMessageTime = DateTime.Now;
        OnConnected?.Invoke(this, EventArgs.Empty);
    }

    protected void ProcessRequestBytes(SnesMemoryRequest request, byte[] data)
    {
        OnMessage?.Invoke(this, new SnesDataReceivedEventArgs()
        {
            Request = request,
            Data = new SnesData(request.Address, data)
        });
    }

    protected string GetDomainString(SnesMemoryDomain domain)
    {
        switch (domain)
        {
            case SnesMemoryDomain.Memory:
                return "WRAM";
            case SnesMemoryDomain.SaveRam:
                return "CARTRAM";
            case SnesMemoryDomain.Rom:
                return "CARTROM";
            default:
                return "";
        }
    }
    
    protected static byte[] HexStringToByteArray(string hex) {
        return Enumerable.Range(0, hex.Length)
            .Where(x => x % 2 == 0)
            .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
            .ToArray();
    }
    
    protected void MarkAsDisconnected()
    {
        if (Socket?.Connected == true)
        {
            Socket?.Disconnect(true);    
        }
        
        if (IsConnected)
        {
            IsConnected = false;
            CurrentRequest = null;
            _lastMessageTime = null;
            OnDisconnected?.Invoke(this, EventArgs.Empty);
        }
    }
    
    protected int TranslateAddress(SnesMemoryRequest message)
    {
        if (!IsBizHawk)
        {
            return message.Address;
        }
        
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
            return offset + remaining;
        }
        else if (message.SnesMemoryDomain == SnesMemoryDomain.Memory)
        {
            return message.Address - 0x7e0000;
        }
        return message.Address;
    }

    private void GetAddressAndPort(SnesConnectorSettings settings, out IPAddress address, out int port)
    {
        address = IPAddress.Loopback;
        port = GetDefaultPort();
        if (!string.IsNullOrEmpty(settings.LuaAddress))
        {
            if (settings.LuaAddress.Contains(':'))
            {
                var parts = settings.LuaAddress.Split(":");
                if (IPAddress.TryParse(parts[0], out var parsedAddress) && int.TryParse(parts[1], out var parsedPort))
                {
                    address = parsedAddress;
                    port = parsedPort;
                }
                else
                {
                    throw new InvalidOperationException($"Invalid Lua address of {settings.LuaAddress}");
                }
            }
            else if (IPAddress.TryParse(settings.LuaAddress, out var parsedAddress))
            {
                address = parsedAddress;
            }
            else
            {
                throw new InvalidOperationException($"Invalid Lua address of {settings.LuaAddress}");
            }
        }
    }

    private async Task StartSocketServer(SnesConnectorSettings settings)
    {
        GetAddressAndPort(settings, out var address, out var port);
        
        Logger.LogInformation("Starting socket server {IP}:{Port}", address.ToString(), port);

        _tcpListener = new TcpListener(address, port);
        _tcpListener.Start();
        while (IsEnabled)
        {
            try
            {
                Socket = await _tcpListener.AcceptSocketAsync();
                Logger.LogInformation("Accepted socket connection");
                if (Socket?.Connected == true)
                {
                    await using var stream = new NetworkStream(Socket);
                    using var reader = new StreamReader(stream);
                    try
                    {
                        _ = SendInitialMessage();
                            
                        var line = await ReadNextLine(reader);
                        while (line != null && Socket?.Connected == true)
                        {
                            _lastMessageTime = DateTime.Now;
                            var prevRequest = CurrentRequest!;
                            CurrentRequest = null;
                            ProcessLine(line, prevRequest);
                            line = await ReadNextLine(reader);
                        }
                            
                        Logger.LogInformation("Socket disconnected");
                        MarkAsDisconnected();
                    }
                    catch (Exception e)
                    {
                        Logger.LogWarning(e, "Error with socket connection");
                        MarkAsDisconnected();
                    }
                }
            }
            catch (Exception e)
            {
                if (IsEnabled)
                {
                    Logger.LogWarning(e, "Error with socket connection");
                }
            }
            
            await Task.Delay(TimeSpan.FromSeconds(3));
        }
    }
    
    private async Task MonitorSocket()
    {
        while (IsEnabled)
        {
            if (Socket != null && _lastMessageTime != null && (DateTime.Now - _lastMessageTime.Value).TotalSeconds > 5)
            {
                MarkAsDisconnected();
                Socket.Close();
                Socket = null;
            }

            await Task.Delay(TimeSpan.FromSeconds(1));
        }
    }
}