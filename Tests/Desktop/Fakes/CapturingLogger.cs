#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Tests.Desktop.Fakes;

/// <summary>
/// Minimal in-memory <see cref="ILogger{T}"/> that records every emitted entry.
/// Tests assert on the captured list rather than mocking framework behaviour.
/// </summary>
internal sealed class CapturingLogger<T> : ILogger<T>
{
    public sealed record Entry(LogLevel Level, string Message, Exception? Exception);

    public List<Entry> Entries { get; } = new();

    IDisposable? ILogger.BeginScope<TState>(TState state) => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        Entries.Add(new Entry(logLevel, formatter(state, exception), exception));
    }
}
