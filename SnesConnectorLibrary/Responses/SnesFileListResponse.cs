namespace SnesConnectorLibrary.Responses;

public class SnesFileListResponse
{
    public required bool Successful { get; set; }
    public required List<SnesFile> Files { get; set; }
}