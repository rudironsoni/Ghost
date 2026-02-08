using System.Security.Cryptography;
using System.Text;

namespace Ghost.Session;

/// <summary>
/// Provides encryption and decryption for sensitive session data.
/// Uses AES-256-GCM for authenticated encryption.
/// </summary>
public static class SessionEncryption
{
    private const int KeySize = 32; // 256 bits
    private const int NonceSize = 12; // 96 bits for GCM
    private const int TagSize = 16; // 128 bits

    /// <summary>
    /// Encrypt data using AES-256-GCM.
    /// </summary>
    /// <param name="plainText">The plaintext to encrypt.</param>
    /// <param name="key">The encryption key (32 bytes for AES-256).</param>
    /// <returns>Base64-encoded encrypted data with format: nonce|tag|ciphertext</returns>
    public static string Encrypt(string plainText, byte[] key)
    {
        ArgumentNullException.ThrowIfNull(plainText);
        ArgumentNullException.ThrowIfNull(key);

        if (key.Length != KeySize)
        {
            throw new ArgumentException($"Key must be {KeySize} bytes for AES-256", nameof(key));
        }

        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var nonce = new byte[NonceSize];
        var tag = new byte[TagSize];
        var ciphertext = new byte[plainBytes.Length];

        RandomNumberGenerator.Fill(nonce);

        using var aesGcm = new AesGcm(key, TagSize);
        aesGcm.Encrypt(nonce, plainBytes, ciphertext, tag);

        // Format: nonce|tag|ciphertext
        var result = new byte[NonceSize + TagSize + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, result, NonceSize, TagSize);
        Buffer.BlockCopy(ciphertext, 0, result, NonceSize + TagSize, ciphertext.Length);

        return Convert.ToBase64String(result);
    }

    /// <summary>
    /// Decrypt data using AES-256-GCM.
    /// </summary>
    /// <param name="encryptedData">Base64-encoded encrypted data with format: nonce|tag|ciphertext</param>
    /// <param name="key">The decryption key (32 bytes for AES-256).</param>
    /// <returns>The decrypted plaintext.</returns>
    public static string Decrypt(string encryptedData, byte[] key)
    {
        ArgumentNullException.ThrowIfNull(encryptedData);
        ArgumentNullException.ThrowIfNull(key);

        if (key.Length != KeySize)
        {
            throw new ArgumentException($"Key must be {KeySize} bytes for AES-256", nameof(key));
        }

        var encryptedBytes = Convert.FromBase64String(encryptedData);

        if (encryptedBytes.Length < NonceSize + TagSize)
        {
            throw new ArgumentException("Invalid encrypted data format", nameof(encryptedData));
        }

        var nonce = new byte[NonceSize];
        var tag = new byte[TagSize];
        var ciphertext = new byte[encryptedBytes.Length - NonceSize - TagSize];

        Buffer.BlockCopy(encryptedBytes, 0, nonce, 0, NonceSize);
        Buffer.BlockCopy(encryptedBytes, NonceSize, tag, 0, TagSize);
        Buffer.BlockCopy(encryptedBytes, NonceSize + TagSize, ciphertext, 0, ciphertext.Length);

        var plainBytes = new byte[ciphertext.Length];

        using var aesGcm = new AesGcm(key, TagSize);
        aesGcm.Decrypt(nonce, ciphertext, tag, plainBytes);

        return Encoding.UTF8.GetString(plainBytes);
    }

    /// <summary>
    /// Generate a cryptographically secure random key for AES-256.
    /// </summary>
    /// <returns>32-byte key suitable for AES-256.</returns>
    public static byte[] GenerateKey()
    {
        var key = new byte[KeySize];
        RandomNumberGenerator.Fill(key);
        return key;
    }

    /// <summary>
    /// Derive a key from a password using PBKDF2.
    /// </summary>
    /// <param name="password">The password to derive the key from.</param>
    /// <param name="salt">Salt for key derivation. Should be unique per session store.</param>
    /// <param name="iterations">Number of iterations (default: 100,000).</param>
    /// <returns>32-byte key suitable for AES-256.</returns>
    public static byte[] DeriveKeyFromPassword(string password, byte[] salt, int iterations = 100000)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(salt);

        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(KeySize);
    }

    /// <summary>
    /// Generate a cryptographically secure salt.
    /// </summary>
    /// <param name="size">Size of the salt in bytes (default: 16).</param>
    /// <returns>Random salt bytes.</returns>
    public static byte[] GenerateSalt(int size = 16)
    {
        var salt = new byte[size];
        RandomNumberGenerator.Fill(salt);
        return salt;
    }
}
