namespace SnesConnectorLibrary.Connectors;

/// <summary>
/// Interface for all of the SNES connectors
/// </summary>
public interface ISnesConnector : IDisposable
{
    /// <summary>
    /// Event for when the connector successfully establishes a connection to the SNES
    /// </summary>
    public event EventHandler? OnConnected;

    /// <summary>
    /// Event for when the connect loses a connection with the SNES
    /// </summary>
    public event EventHandler? OnDisconnected;

    /// <summary>
    /// Event for when the connector receives a message from the SNES
    /// </summary>
    public event SnesDataReceivedEventHandler? OnMessage;

    /// <summary>
    /// Enables the connector to start attempting to connect to the SNES
    /// </summary>
    /// <param name="settings">The connector settings to use</param>
    public void Enable(SnesConnectorSettings settings);

    /// <summary>
    /// Disables the connector and disposes of any active connections
    /// </summary>
    public void Disable();
    
    /// <summary>
    /// Gets a block of memory from the SNES via the connector
    /// </summary>
    /// <param name="request">The request with the details of the memory to receive</param>
    public Task GetAddress(SnesMemoryRequest request);

    /// <summary>
    /// Sets a block of memory to specific values via the connector
    /// </summary>
    /// <param name="request">The request with the details of the memory to update</param>
    public Task PutAddress(SnesMemoryRequest request);
    
    /// <summary>
    /// If the connector has an active connection with the SNES
    /// </summary>
    public bool IsConnected { get; }

    /// <summary>
    /// If the connector is currently available to take requests
    /// </summary>
    public bool CanMakeRequest { get; }
}