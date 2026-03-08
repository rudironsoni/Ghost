---
name: dotnet-security
description: >
  Security hardening and best practices for .NET applications. Navigation skill
  covering OWASP Top 10, authentication, authorization, cryptography, secrets
  management, and secure coding. For building secure applications.

  Keywords: security, owasp, authentication, authorization, cryptography, jwt,
  oauth, secrets, hardening
---
# .NET Security

Security hardening and best practices for .NET applications. This meta-skill provides navigation to ~10 security-focused skills covering OWASP Top 10, authentication, authorization, cryptography, secrets management, and secure coding patterns.

## When to Use This Skill

Load this skill when:
- Implementing OWASP Top 10 mitigations
- Setting up authentication and authorization
- Choosing cryptographic algorithms
- Managing secrets and credentials
- Reviewing code for security issues
- Hardening APIs and web applications

## Quick Navigation

### OWASP & Vulnerabilities

| Need | Load Skill | Level |
|------|------------|-------|
| OWASP Top 10 (2021) | `dotnet-security-owasp` | Advanced |
| Input validation | `dotnet-input-validation` | Intermediate |
| Security headers | `dotnet-security-owasp` | Advanced |
| Rate limiting | `dotnet-security-owasp` | Advanced |

### Authentication & Authorization

| Need | Load Skill | Level |
|------|------------|-------|
| API security (JWT, OAuth) | `dotnet-api-security` | Advanced |
| Blazor auth | `dotnet-blazor-auth` | Intermediate |
| Passkeys | `dotnet-api-security` | Advanced |

### Cryptography

| Need | Load Skill | Level |
|------|------------|-------|
| Algorithm selection | `dotnet-cryptography` | Advanced |
| Encryption/Hashing | `dotnet-cryptography` | Advanced |
| Key derivation | `dotnet-cryptography` | Advanced |

### Secrets Management

| Need | Load Skill | Level |
|------|------------|-------|
| User secrets | `dotnet-csharp-configuration` | Intermediate |
| Secret rotation | `dotnet-secrets-management` | Intermediate |
| Environment variables | `dotnet-secrets-management` | Intermediate |

## Security Decision Trees

### Which Authentication?

```
API → JWT Bearer (dotnet-api-security)
  ↓
Web App → OIDC/Cookies (dotnet-api-security)
  ↓
SPA → BFF pattern (dotnet-api-security)
  ↓
Mobile → OAuth 2.1 (dotnet-api-security)
```

### Which OWASP Mitigation?

| Threat | Mitigation | Skill |
|--------|------------|-------|
| Injection | Parameterized queries | `dotnet-security-owasp` |
| Broken Access Control | RBAC/ABAC | `dotnet-api-security` |
| XSS | Output encoding | `dotnet-security-owasp` |
| Insecure Deserialization | JSON-only | `dotnet-security-owasp` |
| Security Misconfig | Hardening | `dotnet-security-owasp` |

### Which Cryptography?

| Purpose | Algorithm | Skill |
|---------|-----------|-------|
| Symmetric encryption | AES-GCM | `dotnet-cryptography` |
| Asymmetric encryption | RSA-OAEP | `dotnet-cryptography` |
| Hashing | SHA-256/SHA-3 | `dotnet-cryptography` |
| Signatures | ECDSA/RSA-PSS | `dotnet-cryptography` |
| Password hashing | Argon2id | `dotnet-cryptography` |
| Key derivation | HKDF | `dotnet-cryptography` |

## Complete Skill List

### OWASP & Vulnerabilities (2 skills)
- `dotnet-security-owasp` - OWASP Top 10 mitigation
- `dotnet-input-validation` - Request validation

### Authentication & Authorization (3 skills)
- `dotnet-api-security` - Identity, OAuth, JWT
- `dotnet-blazor-auth` - Blazor auth flows
- `dotnet-csharp-configuration` - User secrets

### Cryptography (1 skill)
- `dotnet-cryptography` - Algorithms, hashing, encryption

### Secrets Management (2 skills)
- `dotnet-secrets-management` - Secret management
- `dotnet-csharp-configuration` - Configuration security

### Secure Coding (2 skills)
- `dotnet-security-owasp` - Deprecated API warnings
- `dotnet-csharp-coding-standards` - Secure coding conventions

## Security Checklist

### Application Security

- [ ] Input validation on all boundaries
- [ ] Output encoding for dynamic content
- [ ] Authentication on all endpoints
- [ ] Authorization checks at business logic
- [ ] Secure defaults (fail closed)
- [ ] Security headers configured
- [ ] Rate limiting implemented
- [ ] Audit logging enabled

### Data Security

- [ ] Encryption at rest for sensitive data
- [ ] Encryption in transit (TLS 1.3)
- [ ] Secrets externalized (no hardcoding)
- [ ] Secure key management
- [ ] Data retention policies
- [ ] Backup encryption

### API Security

- [ ] JWT validation (issuer, audience, expiry)
- [ ] Scope/claim validation
- [ ] CORS properly configured
- [ ] CSRF protection where needed
- [ ] API versioning for deprecations

## Security Patterns

### Input Validation

```csharp
// Server-side validation
var validator = new CreateUserValidator();
var result = await validator.ValidateAsync(request);
if (!result.IsValid)
    return Results.ValidationProblem(result.ToDictionary());

// Never trust client input
public sealed class CreateUserRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = null!;
    
    [Required, MinLength(12)]
    public string Password { get; set; } = null!;
}
```

### Secure Configuration

```csharp
// Use user secrets in development
builder.Configuration.AddUserSecrets<Program>();

// Never commit secrets
if (builder.Environment.IsProduction())
{
    builder.Configuration.AddAzureKeyVault(...);
}
```

### Output Encoding

```csharp
// Razor encodes by default
@Model.UserInput  // HTML encoded

// Manual encoding when needed
var encoded = HtmlEncoder.Default.Encode(userInput);
```

## Cross-References

- **Web Development** → `dotnet-web`
- **API Design** → `dotnet-api-design`
- **Architecture** → `dotnet-architecture`
- **Fundamentals** → `dotnet-fundamentals`

## Security Headers

| Header | Purpose | Config |
|--------|---------|--------|
| Content-Security-Policy | XSS prevention | Strict CSP |
| X-Frame-Options | Clickjacking | DENY |
| X-Content-Type-Options | MIME sniffing | nosniff |
| Referrer-Policy | Privacy | strict-origin |
| Permissions-Policy | Feature policy | Minimal |
| Strict-Transport-Security | HTTPS enforcement | Max-age |

## Version Assumptions

- .NET 8.0+ for modern security features
- Identity requires .NET 6.0+
- Passkeys require .NET 8.0+
- Cryptography APIs are version-stable

## Common Vulnerabilities

| CWE | Issue | Prevention |
|-----|-------|------------|
| CWE-79 | XSS | Output encoding |
| CWE-89 | SQL Injection | Parameterized queries |
| CWE-200 | Info Exposure | Error handling |
| CWE-259 | Hardcoded Password | Secret management |
| CWE-284 | Improper Access Control | Authorization checks |
| CWE-352 | CSRF | Anti-forgery tokens |
| CWE-434 | Unrestricted Upload | File validation |
| CWE-502 | Deserialization | JSON-only, type constraints |

## See Also

- [OWASP Top 10 2021](https://owasp.org/Top10/)
- [Microsoft Security Docs](https://learn.microsoft.com/en-us/dotnet/standard/security/)
- [INDEX.md](/.rulesync/skills/INDEX.md) - Complete skill index
- [TAXONOMY.md](/.rulesync/skills/TAXONOMY.md) - Taxonomy schema
