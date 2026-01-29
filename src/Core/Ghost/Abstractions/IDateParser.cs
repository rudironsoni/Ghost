using System.Text.Json;

namespace Ghost.Abstractions;

public interface IDateParser
{
    DateOnly? ParseDate(string? input);
    (DateOnly? Start, DateOnly? End) ParseDateRange(string? input);
    DateTime? ParseRelativeDate(string? input);
}
