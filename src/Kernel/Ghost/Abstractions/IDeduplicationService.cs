namespace Ghost;

public interface IDeduplicationService
{
    public string GenerateId(string title, string company);
}
