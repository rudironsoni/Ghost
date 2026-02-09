namespace Ghost.Testing.Fakes;

/// <summary>
/// Fake browser context for testing scenarios that need context-level operations.
/// </summary>
public class FakeContext
{
    private readonly List<FakePage> _pages = [];

    public IReadOnlyList<FakePage> Pages => _pages;

    public FakePage NewPage()
    {
        var page = new FakePage();
        _pages.Add(page);
        return page;
    }

    public void Close()
    {
        _pages.Clear();
    }
}
