using SnesConnectorLibrary.Connectors;

namespace SnesConnectorLibrary.Requests;

public class SnesCreateDirectoryRequest : SnesRequest
{
    internal override SnesRequestType RequestType => SnesRequestType.MakeDirectory;
    
    /// <summary>
    /// The path of the directory to create on the SNES
    /// </summary>
    public required string Path { get; set; }
    
    /// <summary>
    /// Callback for after creating the directory on the SNES
    /// </summary>
    public Action? OnComplete { get; set; }

    internal override bool CanPerformRequest(ConnectorFunctionality functionality) => functionality.CanAccessFiles;
}