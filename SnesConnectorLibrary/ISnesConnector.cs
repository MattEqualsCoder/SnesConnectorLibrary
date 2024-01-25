namespace SnesConnectorLibrary;

public interface ISnesConnector : IDisposable
{
    public event EventHandler? OnConnected;

    public event EventHandler? OnDisconnected;

    public event SnesDataReceivedEventHandler? OnMessage;

    public void Connect(SnesConnectorSettings settings);

    public void Disconnect();
    
    public bool IsConnected { get; }

    public void GetAddress(SnesMemoryRequest request);

    public Task PutAddress(SnesMemoryRequest request);

    public bool CanMakeRequest { get; }
}