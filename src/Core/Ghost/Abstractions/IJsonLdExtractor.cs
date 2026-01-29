using System.Text.Json;
namespace Ghost.Abstractions;

public interface IJsonLdExtractor
{
    IEnumerable<T> Extract<T>(string html);
    IEnumerable<JsonElement> ExtractRaw(string html);
}
