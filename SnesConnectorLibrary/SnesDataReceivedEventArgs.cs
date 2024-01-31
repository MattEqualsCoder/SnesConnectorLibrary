namespace SnesConnectorLibrary;

public class SnesDataReceivedEventArgs
{
    /// <summary>
    /// The original request sent to the SNES
    /// </summary>
    public required SnesMemoryRequest Request { get; set; }
    
    /// <summary>
    /// The data returned by the SNES
    /// </summary>
    public required SnesData Data { get; set; }
}