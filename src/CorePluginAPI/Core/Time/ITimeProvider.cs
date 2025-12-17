namespace QuantumCore.API.Core.Time;

public interface ITimeProvider
{
    ServerTimestamp Now { get; }
    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default);
}
