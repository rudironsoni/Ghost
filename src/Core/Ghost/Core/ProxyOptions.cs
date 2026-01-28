using System.Collections.Generic;

namespace Ghost.Core;

public class ProxyOptions
{
    public string Strategy { get; set; } = "RoundRobin";
    public StaticProxyConfig Static { get; set; } = new();
    public ApiProxyConfig Api { get; set; } = new();
}

public class StaticProxyConfig
{
    public bool Enabled { get; set; }
    public List<string> Items { get; set; } = new();
}

public class ApiProxyConfig
{
    public bool Enabled { get; set; }
    public string? Url { get; set; }
}
