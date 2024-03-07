using SnesConnectorLibrary.Requests;

namespace SnesConnectorLibrary.Responses;

public class SnesResponseEventArgs<T> where T : SnesRequest
{
    /// <summary>
    /// The original request sent to the SNES
    /// </summary>
    public required T Request { get; set; }
}