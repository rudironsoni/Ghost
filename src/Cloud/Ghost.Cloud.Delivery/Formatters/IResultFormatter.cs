namespace Ghost.Cloud.Delivery.Formatters;

public interface IResultFormatter
{
    public string FormatType { get; }
    public string Extension { get; }
    public string ContentType { get; }
    public byte[] FormatData(List<JsonElement> items);
}
