using SnesConnectorLibrary.Connectors;
using SnesConnectorLibrary.Requests;
using SnesConnectorLibrary.Responses;

namespace SnesConnectorLibrary;

/// <summary>
/// Interface for the SnesConnectorService
/// </summary>
public interface ISnesConnectorService : IDisposable
{
    /// <summary>
    /// Event for when the active connector successfully connects to the SNES
    /// </summary>
    public event EventHandler? Connected;

    /// <summary>
    /// Event for when the active connector disconnects from the SNES
    /// </summary>
    public event EventHandler? Disconnected;
    
    /// <summary>
    /// Event for when a game was detected in the emulator
    /// </summary>
    public event EventHandler? GameDetected;

    /// <summary>
    /// Event for when the active connector receives a message from the SNES
    /// </summary>
    public event SnesMemoryResponseEventHandler? MemoryReceived;

    /// <summary>
    /// Event for when the active connector updates memory
    /// </summary>
    public event SnesResponseEventHandler<SnesMemoryRequest>? MemoryUpdated; 

    /// <summary>
    /// Event for when files have been received from the SNES
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
    /// Event for when a directory has been created on the SNES
    /// </summary>
    public event SnesResponseEventHandler<SnesCreateDirectoryRequest>? DirectoryCreated;
    
    /// <summary>
    /// Event for when a directory has been deleted from the SNES
    /// </summary>
    public event SnesResponseEventHandler<SnesDeleteDirectoryRequest>? DirectoryDeleted;
    
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
    /// Makes a request to the SNES via the active connector
    /// </summary>
    /// <param name="request">The request to make to the SNES. The action will be determined by the type of SnesRequest
    /// object passed in.</param>
    /// <returns>True if the request can be made at this time.</returns>
    public bool MakeRequest(SnesRequest request);
    
    /// <summary>
    /// Makes a single request to either GET or PUT memory to the SNES via the active connector. Reading/writing the ROM
    /// is not supported on snes9x cores and in SNI.
    /// </summary>
    /// <param name="request">The request to make to the SNES</param>
    /// <returns>True if the request can be made to the connector</returns>
    public bool MakeMemoryRequest(SnesSingleMemoryRequest request);
    
    /// <summary>
    /// Makes a single request to either GET or PUT memory to the SNES via the active connector. Reading/writing the ROM
    /// is not supported on snes9x cores and in SNI.
    /// </summary>
    /// <param name="request">The request to make to the SNES</param>
    /// <returns>Response object with the requested memory</returns>
    public Task<SnesSingleMemoryResponse> MakeMemoryRequestAsync(SnesSingleMemoryRequest request);

    /// <summary>
    /// Requests a list of files from the SNES. Only supported on hardware connected via SNI and USB2SNES.
    /// </summary>
    /// <param name="request">The request to make to the snes</param>
    /// <returns>True if the request can be made to the connector</returns>
    public bool GetFileList(SnesFileListRequest request);
    
    /// <summary>
    /// Requests a list of files from the SNES. Only supported on hardware connected via SNI and USB2SNES.
    /// </summary>
    /// <param name="request">The request to make to the snes</param>
    /// <returns>Response object with the file list</returns>
    public Task<SnesFileListResponse> GetFileListAsync(SnesFileListRequest request);

    /// <summary>
    /// Attempts to boot a rom on the SNES. Only supported on hardware connected via SNI and USB2SNES.
    /// </summary>
    /// <param name="request">The request to make to the SNES</param>
    /// <returns>True if the request can be made to the connector</returns>
    public bool BootRom(SnesBootRomRequest request);
    
    /// <summary>
    /// Attempts to boot a rom on the SNES. Only supported on hardware connected via SNI and USB2SNES.
    /// </summary>
    /// <param name="request">The request to make to the SNES</param>
    /// <returns>Response object for if booting could be performed</returns>
    public Task<SnesBootRomResponse> BootRomAsync(SnesBootRomRequest request);

    /// <summary>
    /// Attepts to upload a file to the SNES. Only supported on hardware connected via SNI and USB2SNES.
    /// </summary>
    /// <param name="request">The request to make to the SNES</param>
    /// <returns>True if the request can be made to the connector</returns>
    public bool UploadFile(SnesUploadFileRequest request);
    
    /// <summary>
    /// Attepts to upload a file to the SNES. Only supported on hardware connected via SNI and USB2SNES.
    /// </summary>
    /// <param name="request">The request to make to the SNES</param>
    /// <returns>Response object for if the request could be performed</returns>
    public Task<SnesUploadFileResponse> UploadFileAsync(SnesUploadFileRequest request);
    
    /// <summary>
    /// Attepts to delete a file from the SNES. Only supported on hardware connected via SNI and USB2SNES.
    /// </summary>
    /// <param name="request">The request to make to the SNES</param>
    /// <returns>True if the request can be made to the connector</returns>
    public bool DeleteFile(SnesDeleteFileRequest request);
    
    /// <summary>
    /// Attepts to delete a file from the SNES. Only supported on hardware connected via SNI and USB2SNES.
    /// </summary>
    /// <param name="request">The request to make to the SNES</param>
    /// <returns>Response object for if the request could be performed</returns>
    public Task<SnesDeleteFileResponse> DeleteFileAsync(SnesDeleteFileRequest request);
    
    /// <summary>
    /// Attempts to create a directory on the SNES. Only supported on hardware connected via SNI and USB2SNES.
    /// </summary>
    /// <param name="request">The request to make to the SNES</param>
    /// <returns>True if the request can be made to the connector</returns>
    public bool CreateDirectory(SnesCreateDirectoryRequest request);
    
    /// <summary>
    /// Attempts to create a directory on the SNES. Only supported on hardware connected via SNI and USB2SNES.
    /// </summary>
    /// <param name="request">The request to make to the SNES</param>
    /// <returns>Response object for if the request could be performed</returns>
    public Task<SnesCreateDirectoryResponse> CreateDirectoryAsync(SnesCreateDirectoryRequest request);
    
    /// <summary>
    /// Attempts to delete a directory from the SNES. Only supported on hardware connected via SNI and USB2SNES.
    /// </summary>
    /// <param name="request">The request to make to the SNES</param>
    /// <returns>True if the request can be made to the connector</returns>
    public bool DeleteDirectory(SnesDeleteDirectoryRequest request);
    
    /// <summary>
    /// Attempts to delete a directory from the SNES. Only supported on hardware connected via SNI and USB2SNES.
    /// </summary>
    /// <param name="request">The request to make to the SNES</param>
    /// <returns>Response object for if the request could be performed</returns>
    public Task<SnesDeleteDirectoryResponse> DeleteDirectoryAsync(SnesDeleteDirectoryRequest request);

    /// <summary>
    /// Makes a recurring scheduled request to the SNES via the active connector
    /// </summary>
    /// <param name="request">The request to make, including details of when it should run</param>
    /// <returns>The added request</returns>
    public SnesRecurringMemoryRequest AddRecurringMemoryRequest(SnesRecurringMemoryRequest request);

    /// <summary>
    /// Removes a previously added scheduled request
    /// </summary>
    /// <param name="request">The request to remove</param>
    public void RemoveRecurringRequest(SnesRecurringMemoryRequest request);

    /// <summary>
    /// Removes all queued and scheduled requests and affiliated data
    /// </summary>
    public void ClearRequests();

    /// <summary>
    /// Creates all of the Lua script files at the provided location
    /// </summary>
    /// <param name="folder">The folder to create the Lua scripts at</param>
    /// <returns>True if successful, false otherwise</returns>
    public bool CreateLuaScriptsFolder(string folder);

    /// <summary>
    /// Retrieves the functionality of the current connector
    /// </summary>
    public ConnectorFunctionality CurrentConnectorFunctionality { get; }
    
    /// <summary>
    /// Updates the amount of time for a timeout when no message has been received
    /// </summary>
    /// <param name="seconds">The duration to set the timeout value to</param>
    public void UpdateTimeoutSeconds(int seconds);


}