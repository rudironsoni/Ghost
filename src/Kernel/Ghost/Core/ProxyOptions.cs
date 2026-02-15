using System.Collections.Generic;

namespace Ghost.Kernel;

public class ProxyOptions
{
    public string Strategy { get; set; } = "RoundRobin";
}

public class ProxySourceConfig
{
    public bool Enabled { get; set; } = true;
    public string? Type { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public List<string> Hosts { get; set; } = new(); // Renamed from Items
    public string? Url { get; set; } // For API
}
