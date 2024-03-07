using SnesConnectorLibrary.Requests;

namespace SnesConnectorLibrary.Responses;

public class SnesMemoryResponseEventArgs : SnesResponseEventArgs<SnesMemoryRequest>
{
    /// <summary>
    /// The data returned by the SNES
    /// </summary>
    public required SnesData Data { get; set; }
}