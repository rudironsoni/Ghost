namespace Ghost;

public sealed class ClickOptions
{
    public string Button { get; set; } = "left";
    public int ClickCount { get; set; } = 1;
    public int Delay { get; set; }
    public IEnumerable<string> Modifiers { get; set; } = Array.Empty<string>();
}
