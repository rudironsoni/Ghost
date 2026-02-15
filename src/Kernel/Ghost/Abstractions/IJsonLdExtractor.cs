using System.Text.Json;
namespace Ghost;

public interface IJsonLdExtractor
{
    public IEnumerable<T> Extract<T>(string html);
    public IEnumerable<JsonElement> ExtractRaw(string html);
}
