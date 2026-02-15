using System.Text.Json;
namespace Ghost.Abstractions;

public interface IJsonLdExtractor
{
    public IEnumerable<T> Extract<T>(string html);
    public IEnumerable<JsonElement> ExtractRaw(string html);
}
