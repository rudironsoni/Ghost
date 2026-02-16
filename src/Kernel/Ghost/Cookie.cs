namespace Ghost;

/// <summary>
/// Represents a browser cookie.
/// </summary>
public sealed class Cookie
{
    /// <summary>
    /// Gets or sets the cookie name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the cookie value.
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the cookie domain.
    /// </summary>
    public string? Domain { get; set; }

    /// <summary>
    /// Gets or sets the cookie path.
    /// </summary>
    public string? Path { get; set; }

    /// <summary>
    /// Gets or sets the cookie expiration date.
    /// </summary>
    public DateTime? Expires { get; set; }

    /// <summary>
    /// Gets or sets whether the cookie is HTTP only.
    /// </summary>
    public bool HttpOnly { get; set; }

    /// <summary>
    /// Gets or sets whether the cookie is secure.
    /// </summary>
    public bool Secure { get; set; }

    /// <summary>
    /// Gets or sets the SameSite attribute.
    /// </summary>
    public string? SameSite { get; set; }
}
