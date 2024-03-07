namespace SnesConnectorLibrary.Connectors;

public struct ConnectorFunctionality
{
    public bool CanReadMemory { get; set; }
    public bool CanReadRom { get; set; }
    public bool CanWriteRom { get; set; }
    public bool CanPerformCommands { get; set; }
    public bool CanAccessFiles { get; set; }
}