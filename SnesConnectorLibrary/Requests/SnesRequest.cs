using SnesConnectorLibrary.Connectors;

namespace SnesConnectorLibrary.Requests;

public abstract class SnesRequest
{
    internal abstract SnesRequestType RequestType { get; }

    internal abstract bool CanPerformRequest(ConnectorFunctionality functionality);
}