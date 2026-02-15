using System.Security.Cryptography;
using System.Text;

namespace Ghost.Utilities;

public class DeduplicationService : IDeduplicationService
{
    public string GenerateId(string title, string company)
    {
        title ??= string.Empty;
        company ??= string.Empty;
        string normalized = ($"{title}|{company}").Trim().ToLowerInvariant();
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
