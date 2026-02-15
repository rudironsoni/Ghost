namespace Ghost.Abstractions;

public interface IDeduplicationService
{
    public string GenerateId(string title, string company);
}
