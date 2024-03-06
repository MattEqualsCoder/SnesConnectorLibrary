using SnesConnectorLibrary.Connectors;

namespace SnesConnectorLibrary.Requests;

/// <summary>
/// Request for uploading a file to the SNES
/// </summary>
public class SnesUploadFileRequest : SnesRequest
{
    internal override SnesRequestType RequestType => SnesRequestType.UploadFile;

    /// <summary>
    /// The file on the computer to upload to the SNES
    /// </summary>
    public required string LocalFilePath { get; set; }
    
    /// <summary>
    /// Location of where the file should go to on the SNES
    /// </summary>
    public required string TargetFilePath { get; set; }
    
    /// <summary>
    /// Callback for when the file has been uploaded to the SNES
    /// </summary>
    public Action? OnComplete { get; set; }

    internal override bool CanPerformRequest(ConnectorFunctionality functionality) => functionality.CanAccessFiles;
}