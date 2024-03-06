namespace SnesConnectorLibrary.Responses;

public class SnesFile
{
    public string FullPath { get; set; } = "";
    public string ParentName { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsFolder { get; set; }
}