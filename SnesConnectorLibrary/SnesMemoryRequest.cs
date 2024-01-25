namespace SnesConnectorLibrary;

public class SnesMemoryRequest
{
    public required SnesMemoryRequestType RequestType { get; set; }
    public required int Address { get; set; }
    public int Length { get; set; }
    public required SnesMemoryDomain SnesMemoryDomain { get; set; }
    public ICollection<byte>? Data { get; set; }
    public Action<SnesData>? OnResponse { get; set; }
}