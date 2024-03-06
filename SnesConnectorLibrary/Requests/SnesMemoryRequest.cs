using SnesConnectorLibrary.Connectors;
using SnesConnectorLibrary.Responses;
using SNI;

namespace SnesConnectorLibrary.Requests;

/// <summary>
/// A request to the SNES to either get or put memory
/// </summary>
public class SnesMemoryRequest : SnesRequest
{
    internal override SnesRequestType RequestType => SnesRequestType.Memory;
    
    /// <summary>
    /// Whether the request is for updating or retrieving memory
    /// </summary>
    public required SnesMemoryRequestType MemoryRequestType { get; set; }
    
    /// <summary>
    /// The memory address to retrieve or update. Note that this is in the snes9x memory ranges of WRAM starting at
    /// 0x7E0000, SRAM starting at 0xA06000.
    /// </summary>
    public required int Address { get; set; }
    
    /// <summary>
    /// How many bytes are to be retrieved from the SNES, if applicable
    /// </summary>
    public int Length { get; set; }
    
    /// <summary>
    /// What type of memory should be retrieved from the SNES
    /// </summary>
    public required SnesMemoryDomain SnesMemoryDomain { get; set; }
    
    /// <summary>
    /// Memory mapping used by SNI
    /// </summary>
    public required MemoryMapping SniMemoryMapping { get; set; }
    
    /// <summary>
    /// The bytes to be updated on the SNES, if applicable
    /// </summary>
    public ICollection<byte>? Data { get; set; }
    
    /// <summary>
    /// The address format of the request to use for converting to the proper format for the connector
    /// </summary>
    public required AddressFormat AddressFormat { get; set;  }
    
    /// <summary>
    /// Callback function when data is successfully retrieved from the SNES, if applicable
    /// </summary>
    public Action<SnesData>? OnResponse { get; set; }

    /// <summary>
    /// Gets the address translated to the requested format
    /// </summary>
    /// <param name="to">The address format to conver to</param>
    /// <returns>The converted address location</returns>
    public int GetTranslatedAddress(AddressFormat to) =>
        AddressConversions.Convert(Address, SnesMemoryDomain, AddressFormat, to);

    /// <summary>
    /// If the request can be performed with the available connector functionality
    /// </summary>
    /// <param name="connectorFunctionality">The current connector's functionality</param>
    /// <returns>True if the connector can perform this request</returns>
    internal override bool CanPerformRequest(ConnectorFunctionality connectorFunctionality)
    {
        if (SnesMemoryDomain is SnesMemoryDomain.CartridgeSave or SnesMemoryDomain.ConsoleRAM && connectorFunctionality.CanReadMemory)
        {
            return true;
        }
        else if (SnesMemoryDomain is SnesMemoryDomain.Rom && connectorFunctionality.CanReadRom)
        {
            return true;
        }

        return false;
    }
}