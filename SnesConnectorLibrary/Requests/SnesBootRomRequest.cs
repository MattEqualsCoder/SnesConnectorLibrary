using SnesConnectorLibrary.Connectors;

namespace SnesConnectorLibrary.Requests;

/// <summary>
/// Request for booting a rom on the SNES
/// </summary>
public class SnesBootRomRequest : SnesRequest
{
    internal override SnesRequestType RequestType => SnesRequestType.BootRom;
    
    /// <summary>
    /// The path to the rom file to boot
    /// </summary>
    public required string Path { get; init; }
    
    /// <summary>
    /// Callback for after the request to boot the rom has been sent to the SNES
    /// </summary>
    public Action? OnComplete { get; set; }

    internal override bool CanPerformRequest(ConnectorFunctionality functionality) => functionality.CanPerformCommands;
}