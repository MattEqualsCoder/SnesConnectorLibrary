namespace SnesConnectorLibrary;

/// <summary>
/// A memory request that is repeated on a specific interval and when certain conditions are met
/// </summary>
public class SnesRecurringMemoryRequest : SnesMemoryRequest
{
    /// <summary>
    /// The time in between successive requests
    /// </summary>
    public double FrequencySeconds { get; set; }
    
    /// <summary>
    /// When the request will be available to be called next
    /// </summary>
    public DateTime NextRunTime => LastRunTime + TimeSpan.FromSeconds(FrequencySeconds);
    
    /// <summary>
    /// Function that, if set, will be called to see if the request should be made or not.
    /// </summary>
    public Func<bool>? Filter { get; set; }

    /// <summary>
    /// If the request is available to be ran at the current moment
    /// </summary>
    public bool CanRun => DateTime.Now > NextRunTime && (Filter == null || Filter?.Invoke() == true);
    
    internal DateTime LastRunTime = DateTime.MinValue;
}