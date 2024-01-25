namespace SnesConnectorLibrary;

public class SnesDataReceivedEventArgs
{
    public SnesMemoryRequest Request { get; set; }
    public SnesData Data { get; set; }
}