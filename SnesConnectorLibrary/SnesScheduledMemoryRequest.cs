namespace SnesConnectorLibrary;

public class SnesScheduledMemoryRequest : SnesMemoryRequest
{
    public double FrequencySeconds { get; set; } = 0;
    public DateTime LastRunTime = DateTime.MinValue;
    public DateTime NextRunTime => LastRunTime + TimeSpan.FromSeconds(FrequencySeconds);
    public Func<bool>? Filter { get; set; }

    public bool ShouldRun => DateTime.Now > NextRunTime && (Filter == null || Filter?.Invoke() == true);
}