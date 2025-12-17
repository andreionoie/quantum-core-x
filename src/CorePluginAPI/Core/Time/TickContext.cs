namespace QuantumCore.API.Core.Time;

public readonly record struct TickContext(
    TimeSpan Elapsed,
    ServerTimestamp Now);
