namespace SnesConnectorLibrary.Requests;

/// <summary>
/// Enum for the different types of requests that can be made to the SNES
/// </summary>
public enum SnesMemoryRequestType
{
    /// <summary>
    /// Retrieve memory from the SNES
    /// </summary>
    RetrieveMemory,
    
    /// <summary>
    /// Updates memory on the SNES
    /// </summary>
    UpdateMemory,
}