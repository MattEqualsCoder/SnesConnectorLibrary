namespace SnesConnectorLibrary;

/// <summary>
/// A memory retrieval request that is repeated on a specific interval and when certain conditions are met
/// </summary>
public class SnesRecurringMemoryRequest : SnesMemoryRequest
{
    public new SnesMemoryRequestType RequestType => SnesMemoryRequestType.Retrieve;

    /// <summary>
    /// The time in between successive requests
    /// </summary>
    public double FrequencySeconds { get; init; }
    
    /// <summary>
    /// Function that, if set, will be called to see if the request should be made or not.
    /// </summary>
    public Func<bool>? Filter { get; init; }
    
    /// <summary>
    /// If the callbacks should only trigger when the value has changed
    /// </summary>
    public bool RespondOnChangeOnly { get; init; }
    
    internal DateTime NextRunTime => LastRunTime + TimeSpan.FromSeconds(FrequencySeconds);
    
    internal bool CanRun => DateTime.Now > NextRunTime && (Filter == null || Filter?.Invoke() == true);

    internal DateTime LastRunTime = DateTime.MinValue;
    
    internal string Key => $"{Address}_{Length}_{SnesMemoryDomain}";
}