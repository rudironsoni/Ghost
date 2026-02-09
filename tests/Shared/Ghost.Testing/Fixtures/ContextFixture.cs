using Ghost.Testing.Fakes;

namespace Ghost.Testing.Fixtures;

/// <summary>
/// xUnit fixture for context-level tests. Provides a shared browser context
/// with isolated page instances for each test.
/// </summary>
public class ContextFixture : IAsyncDisposable
{
    public ContextFixture()
    {
        Context = new FakeContext();
    }

    public FakeContext Context { get; }

    public FakePage CreatePage() => Context.NewPage();

    public ValueTask DisposeAsync()
    {
        Context.Close();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
