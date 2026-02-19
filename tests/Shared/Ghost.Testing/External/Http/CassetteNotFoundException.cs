namespace Ghost.Testing.External.Http;

public sealed class CassetteNotFoundException : InvalidOperationException
{
    public CassetteNotFoundException(string message)
        : base(message)
    {
    }
}
