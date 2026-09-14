using Xunit;

namespace CoffeeTracker.Tests;

/// <summary>
/// A <see cref="FactAttribute"/> for tests that can only run on Unix, the ones driving
/// a /bin/sh stub executable or Unix file permissions.
///
/// These used to open with <c>if (OperatingSystem.IsWindows()) { return; }</c>, which
/// reports a pass on Windows having asserted nothing. That is the worst possible outcome
/// for the tests it was applied to: the OCR process-kill path, the concurrency gate, and
/// the partial-write cleanup are the trickiest code in their adapters, and a developer on
/// Windows saw them green. Setting Skip here makes the runner say "skipped", with the
/// reason, so the gap is visible where it exists and CI still runs them.
/// </summary>
public sealed class UnixOnlyFactAttribute : FactAttribute
{
    public UnixOnlyFactAttribute(string reason)
    {
        if (OperatingSystem.IsWindows())
        {
            Skip = reason;
        }
    }
}
