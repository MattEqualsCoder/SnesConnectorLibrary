using SnesConnectorLibrary.Requests;
using SnesConnectorLibrary.Responses;

namespace SnesConnectorLibrary.Connectors;

/// <summary>
/// Interface for all of the SNES connectors
/// </summary>
internal interface ISnesConnector : IDisposable
{
    /// <summary>
    /// Event for when the connector successfully establishes a connection to the SNES
    /// </summary>
    public event EventHandler? Connected;
    
    /// <summary>
    /// Event for when the connector can send memory requests to the SNES
    /// </summary>
    public event EventHandler? GameDetected;

    /// <summary>
    /// Event for when the connect loses a connection with the SNES
    /// </summary>
    public event EventHandler? Disconnected;

    /// <summary>
    /// Event for when the connector receives a message from the SNES
    /// </summary>
    public event SnesMemoryResponseEventHandler? MemoryReceived;

    /// <summary>
    /// Event for when the connector updates memory on the SNES
    /// </summary>
    public event SnesResponseEventHandler<SnesMemoryRequest>? MemoryUpdated;

    /// <summary>
    /// Event for when the connector receives a list of files from the SNES
    /// </summary>
    public event SnesFileListResponseEventHandler? FileListReceived;
    
    /// <summary>
    /// Event for when a rom has been successfully booted
    /// </summary>
    public event SnesResponseEventHandler<SnesBootRomRequest>? RomBooted;

    /// <summary>
    /// Event for when a file has been uploaded to the SNES
    /// </summary>
    public event SnesResponseEventHandler<SnesUploadFileRequest>? FileUploaded;

    /// <summary>
    /// Event for when a file has been deleted from the SNES
    /// </summary>
    public event SnesResponseEventHandler<SnesDeleteFileRequest>? FileDeleted;
    
    /// <summary>
    /// Event for when a directed has been created on the SNES
    /// </summary>
    public event SnesResponseEventHandler<SnesCreateDirectoryRequest>? DirectoryCreated;

    /// <summary>
    /// Event for when a directory has been deleted from the SNES
    /// </summary>
    public event SnesResponseEventHandler<SnesDeleteDirectoryRequest>? DirectoryDeleted;

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
    public Task RetrieveMemory(SnesMemoryRequest request);

    /// <summary>
    /// Sets a block of memory to specific values via the connector
    /// </summary>
    /// <param name="request">The request with the details of the memory to update</param>
    public Task UpdateMemory(SnesMemoryRequest request);
    
    /// <summary>
    /// If the connector has an active connection with the SNES
    /// </summary>
    public bool IsConnected { get; }
    
    /// <summary>
    /// If a game is currently detected and memory requests can be made
    /// </summary>
    public bool IsGameDetected { get; }

    /// <summary>
    /// If the connector is currently available to take requests
    /// </summary>
    public bool CanProcessRequests { get; }

    /// <summary>
    /// If the connector can process this particular request
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    public bool CanMakeRequest(SnesRequest request);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    public int TranslateAddress(SnesMemoryRequest message);

    /// <summary>
    /// Updates the amount of time for a timeout when no message has been received
    /// </summary>
    /// <param name="seconds">The duration to set the timeout value to</param>
    public void UpdateTimeoutSeconds(int seconds);

    /// <summary>
    /// The functionality of the connector
    /// </summary>
    public ConnectorFunctionality SupportedFunctionality { get; }

    /// <summary>
    /// Requests to list the files on the SNES
    /// </summary>
    /// <param name="request">The request to send to the SNES</param>
    public Task ListFiles(SnesFileListRequest request);

    /// <summary>
    /// Boots a rom file on the SNES
    /// </summary>
    /// <param name="request">The request to send to the SNES</param>
    public Task BootRom(SnesBootRomRequest request);

    /// <summary>
    /// Uploads a file to the SNES
    /// </summary>
    /// <param name="request">The request to send to the SNES</param>
    public Task UploadFile(SnesUploadFileRequest request);
    
    /// <summary>
    /// Deletes a file from the SNES
    /// </summary>
    /// <param name="request">The request to send to the SNES</param>
    public Task DeleteFile(SnesDeleteFileRequest request);
    
    /// <summary>
    /// Creates a directory on the SNES
    /// </summary>
    /// <param name="request">The request to send to the SNES</param>
    public Task CreateDirectory(SnesCreateDirectoryRequest request);
    
    /// <summary>
    /// Deletes a directory from the SNES
    /// </summary>
    /// <param name="request">The request to send to the SNES</param>
    public Task DeleteDirectory(SnesDeleteDirectoryRequest request);
}