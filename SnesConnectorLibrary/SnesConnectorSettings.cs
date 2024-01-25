namespace SnesConnectorLibrary;

public class SnesConnectorSettings
{
    public SnesConnectorType ConnectorType { get; set; }
    public string Usb2SnesAddress { get; set; } = "";
    public string SniAddress { get; set; } = "";
    public string ClientName { get; set; } = "Test";
}