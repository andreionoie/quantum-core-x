using System.Diagnostics;
using QuantumCore.API.Core.Time;

namespace QuantumCore.Core.Time;

public class StopwatchTimeProvider : ITimeProvider
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    public ServerTimestamp Now => new(_stopwatch.Elapsed);

    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default)
    {
        if (delay <= TimeSpan.Zero)
        {
            return Task.CompletedTask;
        }

        return Task.Delay(delay, cancellationToken);
    }
}
