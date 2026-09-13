using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace CoffeeTracker.Tests.Ocr;

/// <summary>
/// Routes an adapter's own logging into the test output.
///
/// The OCR adapter never throws: every failure — a process that would not start, a
/// non-zero exit, a run past the timeout — is logged and degraded to
/// <c>OcrResult.Unavailable</c>, which is right for a web request and blinding for a
/// test. With <c>NullLogger</c> the benchmark could only report *that* the engine went
/// unavailable, which is the half of the message nobody can act on.
/// </summary>
public sealed class TestOutputLogger<T>(ITestOutputHelper output) : ILogger<T>
{
    public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        output.WriteLine($"[{logLevel}] {formatter(state, exception)}");
        if (exception is not null)
        {
            output.WriteLine(exception.ToString());
        }
    }

    private sealed class NullScope : IDisposable
    {
        internal static readonly NullScope Instance = new();

        public void Dispose()
        {
            // Nothing is scoped; the type exists only to satisfy BeginScope.
        }
    }
}
