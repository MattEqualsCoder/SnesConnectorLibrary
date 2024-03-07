namespace SnesConnectorLibrary;

/// <summary>
/// Struct to hold settings for using the SNES Connector Service
/// </summary>
public class SnesConnectorSettings
{
    /// <summary>
    /// The type of connector requested for getting/sending data to the SNES
    /// </summary>
    public SnesConnectorType ConnectorType { get; set; }
    
    /// <summary>
    /// The address and port used for QUSB2SNES/USB2SNES/SNI (in USB2SNES mode). Uses 127.0.0.1:8080 if not provided.
    /// </summary>
    public string Usb2SnesAddress { get; set; } = "";
    
    /// <summary>
    /// The address and port used for SNI. Uses 127.0.0.1:8191 if not provided.
    /// </summary>
    public string SniAddress { get; set; } = "";
    
    /// <summary>
    /// The address and ported used for the various Lua connectors. Fallback port depends on the type of Lua connector.
    /// </summary>
    public string LuaAddress { get; set; } = "";

    /// <summary>
    /// Duration until timing out an active connector if there are no responses
    /// </summary>
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// The client named sent to USB2SNES/QUSB2SNES
    /// </summary>
    public string ClientName { get; set; } = "SnesConnectorLibrary";
}