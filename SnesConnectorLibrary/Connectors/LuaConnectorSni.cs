using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace SnesConnectorLibrary.Connectors;

internal class LuaConnectorSni : LuaConnector
{
    public LuaConnectorSni(ILogger<LuaConnector> logger) : base(logger)
    {
    }
    
    public override async Task GetAddress(SnesMemoryRequest request)
    {
        if (Socket?.Connected != true)
        {
            Logger.LogWarning("Socket is not connected");
            return;
        }
        
        var msgString = $"Read|{TranslateAddress(request)}|{request.Length}|{GetDomainString(request.SnesMemoryDomain)}\n\0";
        try
        {
            CurrentRequest = request;
            await Socket.SendAsync(Encoding.ASCII.GetBytes(msgString));
        }
        catch (SocketException ex)
        {
            Logger.LogError(ex, "Error sending message");
            MarkAsDisconnected();
        }
    }

    public override async Task PutAddress(SnesMemoryRequest request)
    {
        if (Socket?.Connected != true)
        {
            Logger.LogWarning("Socket is not connected");
            return;
        }
        
        if (request.Data == null)
        {
            throw new InvalidOperationException("No data sent to to PutAddress");
        }
        var data = request.Data.ToArray().Select(x => x.ToString());


        var hex = string.Join("|", data);
        var msgString = IsBizHawk
            ? $"Write|{TranslateAddress(request)}|{GetDomainString(request.SnesMemoryDomain)}|{hex}\n\0"
            : $"Write|{TranslateAddress(request)}|{hex}\n\0";
        
        try
        {
            await Socket.SendAsync(Encoding.ASCII.GetBytes(msgString));
        }
        catch (SocketException ex)
        {
            Logger.LogError(ex, "Error sending message");
            MarkAsDisconnected();
        }
    }

    protected override int GetDefaultPort() => 65398;

    protected override async Task SendInitialMessage()
    {
        await Task.Delay(TimeSpan.FromSeconds(1));
        Socket?.Send("Version\n\0"u8.ToArray());
    }

    protected override void ProcessLine(string line, SnesMemoryRequest? request)
    {
        if (line.StartsWith("Version"))
        {
            Logger.LogInformation("Received version from Lua connection: {Version}", line);
            IsBizHawk = line.Contains("Bizhawk", StringComparison.OrdinalIgnoreCase);
            MarkConnected();
        }
        else if (request != null)
        {
            if (line.Trim().StartsWith("{"))
            {
                var result = JsonSerializer.Deserialize<Dictionary<string, List<byte>>>(line);
                if (result?.TryGetValue("data", out var data) is true)
                {
                    ProcessRequestBytes(request,  data.ToArray());
                }
                                    
            }
            else
            {
                ProcessRequestBytes(request, HexStringToByteArray(line));
            }
        }
    }

    protected override async Task<string?> ReadNextLine(StreamReader reader)
    {
        return await reader.ReadLineAsync();
    }
}