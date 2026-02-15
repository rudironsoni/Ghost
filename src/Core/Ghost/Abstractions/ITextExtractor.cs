using System.Threading.Tasks;

namespace Ghost.Abstractions;

public interface ITextExtractor
{
    public Task<string> ExtractTextAsync(Ghost.IElement element, string? selector = null);
    public Task<string> ExtractInnerTextAsync(Ghost.IElement element);
}
