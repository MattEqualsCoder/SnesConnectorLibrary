namespace SnesConnectorLibrary;

public class SnesDataReceivedEventArgs
{
    public required SnesMemoryRequest Request { get; set; }
    public required SnesData Data { get; set; }
}