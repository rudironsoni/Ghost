using System.Text.Json;

namespace Ghost;

public interface IDateParser
{
    public DateOnly? ParseDate(string? input);
    public (DateOnly? Start, DateOnly? End) ParseDateRange(string? input);
    public DateTime? ParseRelativeDate(string? input);
}
