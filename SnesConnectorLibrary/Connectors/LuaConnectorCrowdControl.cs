using Microsoft.Extensions.Logging;

namespace SnesConnectorLibrary.Connectors;

internal class LuaConnectorCrowdControl : LuaConnectorEmoTracker
{
    public LuaConnectorCrowdControl(ILogger<LuaConnector> logger) : base(logger)
    {
    }

    protected override int GetDefaultPort() => 23884;

}