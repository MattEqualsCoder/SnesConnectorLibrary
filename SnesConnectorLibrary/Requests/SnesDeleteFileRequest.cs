using SnesConnectorLibrary.Connectors;

namespace SnesConnectorLibrary.Requests;

/// <summary>
/// Request for deleting a file on the SNES
/// </summary>
public class SnesDeleteFileRequest : SnesRequest
{
    internal override SnesRequestType RequestType => SnesRequestType.DeleteFile;
    
    /// <summary>
    /// The path of the file to delete from the SNES
    /// </summary>
    public required string Path { get; init; }
    
    /// <summary>
    /// Callback for after deleting the file from the SNES
    /// </summary>
    public Action? OnComplete { get; set; }

    internal override bool CanPerformRequest(ConnectorFunctionality functionality) => functionality.CanAccessFiles;
}