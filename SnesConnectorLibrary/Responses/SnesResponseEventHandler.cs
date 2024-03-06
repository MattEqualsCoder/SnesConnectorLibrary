using SnesConnectorLibrary.Requests;

namespace SnesConnectorLibrary.Responses;

public delegate void SnesResponseEventHandler<T>(object sender, SnesResponseEventArgs<T> e) where T : SnesRequest;
