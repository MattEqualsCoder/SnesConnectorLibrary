using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace SnesConnectorLibrary.Connectors;

internal class LuaConnectorEmoTracker : LuaConnector
{
    public LuaConnectorEmoTracker()
    {
    }
    
    public LuaConnectorEmoTracker(ILogger<LuaConnector> logger) : base(logger)
    {
    }
    
     public override async Task GetAddress(SnesMemoryRequest request)
    {
        CurrentRequest = request;
        await Send(new EmoTrackerRequest()
        {
            Type = 0x0F,
            Address = TranslateAddress(request),
            Value = request.Length,
            Domain = IsBizHawk ? GetDomainString(request.SnesMemoryDomain) : ""
        });
    }

    public override async Task PutAddress(SnesMemoryRequest request)
    {
        if (request.Data == null)
        {
            throw new InvalidOperationException("PutData called without any data");
        }

        if (IsBizHawk)
        {
            var byteArray = request.Data.ToArray();
            var length = byteArray.Length;
            var index = 0;
            while (index < length)
            {
                if (length - index >= 4)
                {
                    await Send(new EmoTrackerRequest()
                    {
                        Type = 0x12,
                        Address = TranslateAddress(request),
                        Value = BitConverter.ToInt32(byteArray.Skip(index).Take(4).ToArray()),
                        Domain = IsBizHawk ? GetDomainString(request.SnesMemoryDomain) : ""
                    });
                    index += 4;
                }
                else if (length - index >= 2)
                {
                    await Send(new EmoTrackerRequest()
                    {
                        Type = 0x11,
                        Address = TranslateAddress(request),
                        Value = BitConverter.ToInt16(byteArray.Skip(index).Take(2).ToArray()),
                        Domain = IsBizHawk ? GetDomainString(request.SnesMemoryDomain) : ""
                    });
                    index += 2;
                }
                else
                {
                    await Send(new EmoTrackerRequest()
                    {
                        Type = 0x10,
                        Address = TranslateAddress(request),
                        Value = byteArray[index],
                        Domain = IsBizHawk ? GetDomainString(request.SnesMemoryDomain) : ""
                    });
                    index++;
                }
            }
        }
        else
        {
            _ = Send(new EmoTrackerRequest()
            {
                Type = 0x1F,
                Address = TranslateAddress(request),
                Block = Convert.ToBase64String(request.Data.ToArray()),
                Domain = IsBizHawk ? GetDomainString(request.SnesMemoryDomain) : ""
            });
        }
    }

    public override AddressFormat TargetAddressFormat => IsBizHawk ? AddressFormat.BizHawk : AddressFormat.Snes9x;
    
    protected override int GetDefaultPort() => 43884;

    protected override async Task SendInitialMessage()
    {
        await Task.Delay(TimeSpan.FromSeconds(0.5));
        await Send(new EmoTrackerRequest()
        {
            Type = 0xE2
        });
    }

    protected override void ProcessLine(string line, SnesMemoryRequest? request)
    {
        var message = JsonSerializer.Deserialize<EmoTrackerResponse>(line);

        if (message?.Type == 0xE2)
        {
            IsBizHawk = message.Message != "Not Supported" && message.Message != "Unsupported";
            Logger?.LogInformation("Determined as running emulator {Type} ({Message})", IsBizHawk ? "BizHawk" : "Snes9x", message.Message);
            MarkConnected();
        }
        else if (message?.Type == 0x0F && request != null)
        {
            ProcessRequestBytes(request, Convert.FromBase64String(message.Block));
        }
    }

    protected override async Task<string?> ReadNextLine(StreamReader reader)
    {
        char[] buffer = new char[1];
        StringBuilder builder = new StringBuilder();
        var isBuildingResponse = false;
        var finishedResponse = false;
        var isEnabled = IsEnabled;

        while (isEnabled == IsEnabled)
        {
            await reader.ReadAsync(buffer, 0, 1);
            if (buffer[0] == '{')
            {
                isBuildingResponse = true;
            }

            if (isBuildingResponse)
            {
                builder.Append(buffer[0]);
            }

            if (buffer[0] == '}')
            {
                finishedResponse = true;
                break;
            }
        }

        if (finishedResponse)
        {
            return builder.ToString();
        }
        else
        {
            return null;
        }
    }
    
    private async Task Send(string message)
    {
        if (Socket == null)
        {
            Logger?.LogWarning("Attempted to send message with no valid socket");
            return;
        }
        var bytes = BitConverter.GetBytes(message.Length).Reverse().ToArray();
        try
        {
            await Socket.SendAsync(bytes);
            await Task.Delay(TimeSpan.FromMilliseconds(10));
            await Socket.SendAsync(Encoding.ASCII.GetBytes(message));
        }
        catch (SocketException ex)
        {
            Logger?.LogError(ex, "Failed to send message to socket");
            MarkAsDisconnected();
        }
    }
    
    private async Task Send(EmoTrackerRequest message)
    {
        await Send(JsonSerializer.Serialize(message));
    }

    class EmoTrackerResponse
    {
        [JsonPropertyName("message")]
        public string Message { get; set; } = "";

        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("address")]
        public long Address { get; set; }

        [JsonPropertyName("stamp")]
        public long Stamp { get; set; }

        [JsonPropertyName("value")]
        public int Value { get; set; }

        [JsonPropertyName("type")]
        public int Type { get; set; }

        [JsonPropertyName("block")]
        public string Block { get; set; } = "";

    }

    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local")]
    class EmoTrackerRequest
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("address")]
        public long Address { get; set; }

        [JsonPropertyName("value")]
        public int Value { get; set; }

        [JsonPropertyName("type")]
        public int Type  { get; set; }

        [JsonPropertyName("block")]
        public string Block { get; set; } = "";
        
        [JsonPropertyName("domain")]
        public string Domain { get; set; } = "";
    }
}