using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace SnesConnectorLibrary.Connectors;

public abstract class LuaConnector : ISnesConnector
{
    protected readonly ILogger<LuaConnector> Logger;
    private TcpListener? _tcpListener;
    protected bool IsEnabled;
    protected bool IsBizHawk;
    protected Socket? Socket;
    protected SnesMemoryRequest? CurrentRequest;
    private DateTime? _lastMessageTime;

    public LuaConnector(ILogger<LuaConnector> logger)
    {
        Logger = logger;
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

        IsEnabled = true;

        _ = StartSocketServer(settings);
        _ = MonitorSocket();
    }

    public void Disconnect()
    {
        MarkAsDisconnected();
        
        if (Socket?.Connected == true)
        {
            Socket?.Disconnect(true);    
        }

        _tcpListener?.Stop();

        IsEnabled = false;

    }

    public bool IsConnected { get; private set; }

    public abstract void GetAddress(SnesMemoryRequest request);

    public abstract Task PutAddress(SnesMemoryRequest request);

    public bool CanMakeRequest => IsConnected && CurrentRequest == null && Socket?.Connected == true;

    protected void MarkConnected()
    {
        Logger.LogInformation("Connected!");
        CurrentRequest = null;
        IsConnected = true;
        _lastMessageTime = DateTime.Now;
        OnConnected?.Invoke(this, EventArgs.Empty);
    }

    protected abstract int GetDefaultPort();

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

    protected abstract Task SendInitialMessage();

    protected void ProcessRequestBytes(SnesMemoryRequest request, byte[] data)
    {
        OnMessage?.Invoke(this, new SnesDataReceivedEventArgs()
        {
            Request = request,
            Data = new SnesData(request.Address, data)
        });
    }

    protected abstract void ProcessLine(string line, SnesMemoryRequest? request);

    protected abstract Task<string?> ReadNextLine(StreamReader reader);
    
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
        }
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

    protected void MarkAsDisconnected()
    {
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
}