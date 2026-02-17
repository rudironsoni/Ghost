using System.Text;

namespace Ghost.Cloud.Delivery.Formatters;

public sealed class NdJsonFormatter : IResultFormatter
{
    public string FormatType => "ndjson";
    public string Extension => "ndjson";
    public string ContentType => "application/x-ndjson";

    public byte[] FormatData(List<JsonElement> items)
    {
        var sb = new StringBuilder();
        foreach (JsonElement item in items)
        {
            sb.AppendLine(item.ToString());
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }
}
