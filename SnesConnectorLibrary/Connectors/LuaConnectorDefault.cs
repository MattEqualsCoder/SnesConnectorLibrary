﻿using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace SnesConnectorLibrary.Connectors;

public class LuaConnectorDefault : LuaConnector
{
    public LuaConnectorDefault(ILogger<LuaConnector> logger) : base(logger)
    {
    }

    public override void GetAddress(SnesMemoryRequest request)
    {
        CurrentRequest = request;
        _ = SendRequest(new LuaRequest()
        {
            Action = "read_block",
            Domain = GetDomainString(request.SnesMemoryDomain),
            Address = TranslateAddress(request),
            Length = request.Length
        });
    }

    public override async Task PutAddress(SnesMemoryRequest request)
    {
        if (request.Data == null)
        {
            Logger.LogWarning("Attempted to write a null byte array");
            return;
        }
        
        await SendRequest(new LuaRequest()
        {
            Action = "write_bytes",
            Domain = GetDomainString(request.SnesMemoryDomain),
            Address = TranslateAddress(request),
            WriteValues = request.Data
        });
    }

    protected override int GetDefaultPort() => 6969;

    protected override async Task SendInitialMessage()
    {
        await SendRequest(new LuaRequest()
        {
            Action = "version"
        });
    }

    protected override void ProcessLine(string line, SnesMemoryRequest? request)
    {
        var response = JsonSerializer.Deserialize<LuaResponse>(line);
        if (response == null)
        {
            Logger.LogError("Invalid response of {Line}", line);
            return;
        }

        if (response.Action == "version")
        {
            Logger.LogInformation("Connected to emulator {Name}", response.Value);
            IsBizHawk = response.Value == "BizHawk";
            MarkConnected();
        }
        else if (response.Action == "read_block" && request != null)
        {
            ProcessRequestBytes(request, response.Bytes!.ToArray());
        }
    }

    protected override async Task<string?> ReadNextLine(StreamReader reader)
    {
        return await reader.ReadLineAsync();
    }

    private async Task SendRequest(LuaRequest request)
    {
        if (Socket == null)
        {
            Logger.LogWarning("Attempted to send request to null socket");
            return;
        }
        try
        {
            var message = JsonSerializer.Serialize(request);
            await Socket.SendAsync(Encoding.ASCII.GetBytes(message+"\0"));
        }
        catch (SocketException ex)
        {
            Logger.LogError(ex, "Error sending message");
            MarkAsDisconnected();
        }
    }

    class LuaRequest
    {
        public string Action { get; set; } = "";
        public string Domain { get; set; } = "";
        public int Address { get; set; }
        public int Length { get; set; }
        public ICollection<byte>? WriteValues { get; set; }
    }
    
    class LuaResponse
    {
        public string Action { get; set; } = "";
        public int Address { get; set; }
        public int Length { get; set; }
        public ICollection<byte>? Bytes { get; set; }
        public string Value { get; set; } = "";
    }
}