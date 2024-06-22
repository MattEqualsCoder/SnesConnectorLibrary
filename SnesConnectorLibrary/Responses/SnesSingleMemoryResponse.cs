namespace SnesConnectorLibrary.Responses;

public class SnesSingleMemoryResponse
{
    public required bool Successful { get; set; }
    public required SnesData Data { get; set; }
    public bool HasData => Data.Raw.Length > 0;
}