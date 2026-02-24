# Security Guidelines

This document describes the security practices and patterns used in Shortboxerr for handling sensitive data.

## Credential Storage

### Encryption at Rest

All sensitive credentials are encrypted before storage using AES-256-GCM authenticated encryption:

- **Algorithm**: AES-256-GCM (Galois/Counter Mode)
- **Key Derivation**: PBKDF2-SHA256 with 100,000 iterations
- **Key Source**: Machine-specific identifier (ensures credentials can only be decrypted on the same machine)
- **Format**: `ENC:1:{base64(nonce + ciphertext + tag)}`

### Marking Sensitive Properties

Properties that contain credentials must be marked with the `[SensitiveCredential]` attribute:

```csharp
public class MySettings
{
    public string Username { get; set; } = "";
    
    [SensitiveCredential]
    public string Password { get; set; } = "";
    
    [SensitiveCredential]
    public string ApiKey { get; set; } = "";
}
```

The `SettingsService` automatically encrypts/decrypts properties with this attribute.

### Files Involved

| File | Purpose |
|------|---------|
| `ICredentialEncryptionService.cs` | Interface and `[SensitiveCredential]` attribute |
| `CredentialEncryptionService.cs` | AES-256-GCM implementation |
| `SettingsService.cs` | Auto-encrypt on save, auto-decrypt on load |

## API Responses

### Never Return Plaintext Passwords

API endpoints must never return plaintext passwords. Use boolean flags or masked values:

```csharp
// CORRECT: Use HasPassword flag
public class MetronSettingsResponse
{
    public string Username { get; set; } = "";
    public bool HasPassword { get; set; }  // Never return actual password
}

// CORRECT: Mask API keys
public class SettingsResponse
{
    public bool HasApiKey { get; set; }
    public string? MaskedApiKey { get; set; }  // e.g., "abc1...xyz9"
}
```

### Masking Helper

Use the masking pattern for API keys when display is needed:

```csharp
private static string? MaskApiKey(string? apiKey)
{
    if (string.IsNullOrEmpty(apiKey) || apiKey.Length < 8)
        return null;
    return $"{apiKey[..4]}...{apiKey[^4..]}";
}
```

### Intentional Full Key Access

Some endpoints intentionally return full API keys (for user configuration). These should:
- Be on separate, explicit endpoints (e.g., `/settings/apikey/full`)
- Not be called by default UI views
- Only be used when user explicitly requests to see the key

## Logging

### Automatic Redaction

The `SensitiveDataDestructuringPolicy` automatically masks sensitive fields in Serilog logs:

**Masked field names** (case-insensitive, including partial matches):
- `apikey`, `api_key`, `apiKey`
- `password`, `passwd`, `pwd`
- `token`, `access_token`, `refresh_token`
- `secret`, `secretkey`, `secret_key`
- `credential`, `credentials`
- `authorization`, `auth`, `bearer`
- `connectionstring`, `connection_string`

### Manual Masking

When logging URLs that may contain credentials, use helper methods:

```csharp
// Example from NewznabClient
_logger?.LogDebug("Searching {Url}", MaskApiKey(url, indexer.ApiKey));

private static string MaskApiKey(string url, string apiKey)
{
    if (string.IsNullOrEmpty(apiKey)) return url;
    return url.Replace(apiKey, "***APIKEY***");
}
```

### Never Log

- Raw passwords or API keys
- Authentication headers
- Session tokens
- Full credentials objects

## Frontend Security

### Password Fields

Always use `type="password"` for credential inputs:

```tsx
<input
  type="password"
  value={password}
  onChange={(e) => setPassword(e.target.value)}
  placeholder="Enter password"
/>
```

### Storage Restrictions

- **Never** store credentials in `localStorage` or `sessionStorage`
- Credentials should only exist in React state during form editing
- Clear credential state after form submission

### Console Logging

- **Never** log passwords, API keys, or tokens to console
- Use environment checks if debug logging is needed:

```typescript
if (import.meta.env.DEV) {
  console.log('Request URL:', url);  // OK: no credentials
  // console.log('Password:', password);  // NEVER DO THIS
}
```

## Code Review Checklist

When reviewing code that handles credentials, verify:

- [ ] Sensitive properties marked with `[SensitiveCredential]`
- [ ] API responses use `HasPassword`/`HasApiKey` flags, not actual values
- [ ] No `Log*` calls with credential variables
- [ ] Password inputs use `type="password"`
- [ ] No `localStorage`/`sessionStorage` for credentials
- [ ] No `console.log` with credential values
- [ ] URLs with credentials in query strings are not logged
- [ ] Error messages don't include credential values

## Adding New Credential Types

When adding a new service that requires credentials:

1. **Define settings class** with `[SensitiveCredential]` on sensitive properties
2. **Create API endpoint** that returns `HasPassword: true/false` instead of actual password
3. **Add frontend form** with `type="password"` inputs
4. **Test encryption** by verifying database contains `ENC:1:` prefixed values
5. **Verify logging** doesn't expose credentials in any log level

## Testing

The `CredentialEncryptionServiceTests` suite verifies:

- Encryption/decryption round-trip
- Unique ciphertext per encryption (random nonce)
- Handling of empty/null values
- Prevention of double-encryption
- Detection of encrypted vs plaintext values
- Handling of corrupted/tampered data

Run tests: `dotnet test --filter "CredentialEncryption"`
