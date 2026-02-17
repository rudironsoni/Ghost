using System.Text;
using System.Text.Json;

namespace Ghost.Cloud.Delivery.Formatters;

public sealed class JsonFormatter : IResultFormatter
{
    private static readonly JsonSerializerOptions s_options = new() { WriteIndented = true };

    public string FormatType => "json";
    public string Extension => "json";
    public string ContentType => "application/json";

    public byte[] FormatData(List<JsonElement> items)
    {
        var wrapper = new { items };
        string json = JsonSerializer.Serialize(wrapper, s_options);
        return Encoding.UTF8.GetBytes(json);
    }
}
