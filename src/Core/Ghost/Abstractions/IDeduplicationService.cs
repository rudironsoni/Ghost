namespace Ghost.Abstractions;

public interface IDeduplicationService
{
    string GenerateId(string title, string company);
}
