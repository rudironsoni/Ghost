namespace Ghost.Abstractions;

public interface ITextExtractor
{
    string ExtractText(Ghost.IElement element, string? selector = null);
    string ExtractInnerText(Ghost.IElement element);
}
