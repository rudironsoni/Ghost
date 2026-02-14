using System.Threading.Tasks;

namespace Ghost.Abstractions;

public interface ITextExtractor
{
    Task<string> ExtractTextAsync(Ghost.IElement element, string? selector = null);
    Task<string> ExtractInnerTextAsync(Ghost.IElement element);
}
