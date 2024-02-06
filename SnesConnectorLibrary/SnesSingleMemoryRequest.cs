using SNI;

namespace SnesConnectorLibrary;

/// <summary>
/// A request to the SNES to either get or put memory
/// </summary>
public class SnesSingleMemoryRequest : SnesMemoryRequest 
{
    /// <summary>
    /// The type of request (retrieve or update)
    /// </summary>
    public new required SnesMemoryRequestType RequestType { get; set; }
}