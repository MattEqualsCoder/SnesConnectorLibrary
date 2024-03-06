using SnesConnectorLibrary.Requests;

namespace SnesConnectorLibrary.Responses;

public class SnesFileListResponseEventArgs : SnesResponseEventArgs<SnesFileListRequest>
{
    /// <summary>
    /// The data returned by the SNES
    /// </summary>
    public required List<SnesFile> Files { get; set; }
}