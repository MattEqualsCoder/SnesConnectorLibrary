namespace SnesConnectorLibrary;

/// <summary>
/// Interface for the SnesConnectorService
/// </summary>
public interface ISnesConnectorService : IDisposable
{
    /// <summary>
    /// Event for when the active connector successfully connects to the SNES
    /// </summary>
    public event EventHandler? OnConnected;

    /// <summary>
    /// Event for when the active connector disconnects from the SNES
    /// </summary>
    public event EventHandler? OnDisconnected;

    /// <summary>
    /// Event for when the active connector receives a message from the SNES
    /// </summary>
    public event SnesDataReceivedEventHandler? OnMessage;
    
    /// <summary>
    /// Creates the default instance of an ISnesConnectorService
    /// </summary>
    /// <returns>The created ISnesConnectorService</returns>
    public static ISnesConnectorService CreateService()
    {
        return new SnesConnectorService();
    }

    /// <summary>
    /// If the connector is currently connected to the SNES
    /// </summary>
    public bool IsConnected { get; }

    /// <summary>
    /// Attempts to connect to the SNES via a connector. Will disconnect the previous connector if there is one.
    /// </summary>
    /// <param name="type">The SNES connector type to connect to</param>
    public void Connect(SnesConnectorType type);

    /// <summary>
    /// Attempts to connect to the SNES via a connector. Will disconnect the previous connector if there is one.
    /// </summary>
    /// <param name="settings">The connector settings to use for connecting to the SNES</param>
    public void Connect(SnesConnectorSettings settings);

    /// <summary>
    /// Disconnects the active connector
    /// </summary>
    public void Disconnect();

    /// <summary>
    /// Makes a single request to either GET or PUT memory to the SNES via the active connector
    /// </summary>
    /// <param name="request">The request to make to the SNES</param>
    public void MakeRequest(SnesSingleMemoryRequest request);

    /// <summary>
    /// Makes a recurring scheduled request to the SNES via the active connector
    /// </summary>
    /// <param name="request">The request to make, including details of when it should run</param>
    /// <returns>The added request</returns>
    public SnesRecurringMemoryRequest AddRecurringRequest(SnesRecurringMemoryRequest request);

    /// <summary>
    /// Removes a previously added scheduled request
    /// </summary>
    /// <param name="request">The request to remove</param>
    public void RemoveRecurringRequest(SnesRecurringMemoryRequest request);

    /// <summary>
    /// Creates all of the Lua script files at the provided location
    /// </summary>
    /// <param name="folder">The folder to create the Lua scripts at</param>
    /// <returns>True if successful, false otherwise</returns>
    public bool CreateLuaScriptsFolder(string folder);


}