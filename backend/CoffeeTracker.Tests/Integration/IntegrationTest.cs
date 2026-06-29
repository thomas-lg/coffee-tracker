namespace CoffeeTracker.Tests.Integration;

// Base class giving each test method its own freshly-booted API + throwaway DB.
// xUnit constructs a new test-class instance per [Fact], so the factory (and its
// database) are isolated per test — which matters here because the first registered
// user becomes admin, so shared state would make registration tests order-dependent.
// The client/app boot lazily, so a test that spins up its own ApiFactory (e.g. with
// registration disabled) doesn't pay for the default one.
public abstract class IntegrationTest : IDisposable
{
    protected ApiFactory Factory { get; } = new();

    private HttpClient? _client;
    protected HttpClient Client => _client ??= Factory.CreateClient();

    public void Dispose()
    {
        _client?.Dispose();
        Factory.Dispose();
        GC.SuppressFinalize(this);
    }
}
