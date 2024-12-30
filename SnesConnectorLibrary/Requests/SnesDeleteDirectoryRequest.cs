using SnesConnectorLibrary.Connectors;

namespace SnesConnectorLibrary.Requests;

public class SnesDeleteDirectoryRequest : SnesRequest
{
    internal override SnesRequestType RequestType => SnesRequestType.DeleteDirectory;
    
    /// <summary>
    /// The path of the directory to delete on the SNES
    /// </summary>
    public required string Path { get; set; }
    
    /// <summary>
    /// Callback for after deleting the directory on the SNES
    /// </summary>
    public Action? OnComplete { get; set; }

    internal override bool CanPerformRequest(ConnectorFunctionality functionality) => functionality.CanAccessFiles;
}