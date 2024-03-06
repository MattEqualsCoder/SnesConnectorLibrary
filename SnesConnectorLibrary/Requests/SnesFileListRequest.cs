using SnesConnectorLibrary.Connectors;
using SnesConnectorLibrary.Responses;

namespace SnesConnectorLibrary.Requests;

/// <summary>
/// Request for retrieving files from the SNES
/// </summary>
public class SnesFileListRequest : SnesRequest
{
    internal override SnesRequestType RequestType => SnesRequestType.GetFileList;
    
    /// <summary>
    /// Path to search
    /// </summary>
    public string Path { get; set; } = "";
    
    /// <summary>
    /// If subdirectories should be searched
    /// </summary>
    public bool Recursive { get; set; }
    
    /// <summary>
    /// Function to filter what results are returned
    /// </summary>
    public Func<SnesFile, bool>? Filter { get; set; }
    
    /// <summary>
    /// Callback for after the file list has been received from the SNES
    /// </summary>
    public Action<List<SnesFile>>? OnResponse { get; set; }

    internal override bool CanPerformRequest(ConnectorFunctionality functionality)
    {
        return functionality.CanAccessFiles;
    }

    internal bool SnesFileMatches(SnesFile file) => Filter == null || Filter.Invoke(file);
}