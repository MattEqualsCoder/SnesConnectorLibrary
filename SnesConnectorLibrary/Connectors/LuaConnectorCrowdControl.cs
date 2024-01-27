﻿using Microsoft.Extensions.Logging;

namespace SnesConnectorLibrary.Connectors;

public class LuaConnectorCrowdControl : LuaConnectorEmoTracker
{
    public LuaConnectorCrowdControl(ILogger<LuaConnector> logger) : base(logger)
    {
    }

    protected override int GetDefaultPort() => 23884;

}