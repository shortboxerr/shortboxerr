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

### ESLint Accepted Warnings (UI)

The UI uses `ui/eslint.config.js` with some rules downgraded to `warn`. These were reviewed for security and app safety (BACKLOG 14.25):

| Rule | Security / safety assessment |
|------|-----------------------------|
| **react-hooks/set-state-in-effect** | State is synced from URL or API (theme, tab, view mode). React escapes by default; we do not inject URL or API strings into `dangerouslySetInnerHTML`. No credentials in URL. **Accepted.** |
| **react-refresh/only-export-components** | Co-location of hooks (e.g. `useTheme`) with components. No impact on credentials or injection. **Accepted.** |
| **@typescript-eslint/no-explicit-any** | Used in `api/client.ts` for generated/callback types. Backend DTOs must never expose secrets (see API Responses above). When touching those call sites, prefer proper types or `unknown` + guards to avoid masking sensitive response shapes. **Accepted with caveat.** |
| **react-hooks/static-components** | Inline component definitions; affects correctness/performance, not credentials or XSS. **Accepted.** |

When adding or changing accepted warnings in `eslint.config.js`, re-check that the pattern does not weaken credential handling, logging, or injection defenses, and update this section if needed.

### Build output (`wwwroot` / Vite `dist`)

- Treat published UI assets under `wwwroot` (or `ui/dist`) as **build artifacts**, not a source of truth for secrets.
- Do **not** inject API keys, tokens, or passwords via Vite `define`, env files, or CI variables into the client bundle. The app loads credentials from the API after the user configures settings; the SPA must not embed runtime secrets.
- When reviewing build or Docker changes, confirm nothing copies `.env` or secret files into the image or static output.

## NuGet supply chain

- **Pinned transitive packages:** `Directory.Build.props` at the repo root adds direct references so known-vulnerable transitive versions (e.g. `Microsoft.Extensions.Caching.Memory`, `System.Text.Json`, `System.Text.Encodings.Web`) resolve to patched releases. The test project also pins legacy `System.Net.Http` / `System.Text.RegularExpressions` where the graph still pulled 4.3.0.
- **Framework alignment:** Application packages (`Microsoft.AspNetCore.*`, `Microsoft.EntityFrameworkCore.*`, etc.) should stay on a consistent **8.0.x** line (patch bumps together when upgrading). Individual `Microsoft.Extensions.*` packages do not always publish every patch number; use NuGet version lists rather than assuming `8.0.11` exists for all extension packages.
- **Verification:** Run `dotnet list package --vulnerable --include-transitive` in the dev container after dependency changes. CI runs the same check (see `.github/workflows/ci.yml`).

## E2E test dependencies

- `tests/e2e/` has its own `package.json` and `node_modules`. It is **not** shipped in release images; keep it that way.
- Periodically run `npm audit` in `tests/e2e/` (e.g. when touching Playwright or adding e2e deps).

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
- [ ] New dependencies: no new high/critical NuGet advisories without mitigation (`dotnet list package --vulnerable`)
- [ ] Frontend build does not embed secrets in `wwwroot` bundles

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

## AI and Dev Tooling: Do Not Commit

The following paths and patterns **must never be committed** to the repo. They are listed in `.gitignore`; this section is the authoritative blocklist and policy.

### Blocklist

| Pattern | Reason |
|--------|--------|
| `.cursor/agent-transcripts/` | Agent chat transcripts may contain context you do not want in repo history. |
| `.devcontainer/local-secrets/` | Local PAT files (e.g. `github_token`) used by `.cursor/github-mcp.sh`; never commit. |
| `.cursor/**/env`, `.cursor/**/*.env` | MCP or tool config may hold API keys/tokens; use global Cursor config (e.g. `~/.cursor/mcp.json`) for tokens. |
| `.aider*` | Aider AI editor state. |
| `.continue/` | Continue.dev state and config. |
| `.env`, `.env.local`, `.env.*.local` | Local environment and secrets. |
| `*.secrets.json` | User secrets (e.g. `appsettings.secrets.json`). |

### Policy

- **Before committing:** Ensure no blocklisted file is staged. If you use Cursor MCP with a GitHub token, store it only in **global** config (e.g. `~/.cursor/mcp.json`), not in the project’s `.cursor/mcp.json` (see `.cursor/README.md`).
- **If you accidentally committed a secret:** Revoke the credential immediately (e.g. GitHub token), then remove it from history (e.g. `git filter-repo` or BFG) and force-push. Document the incident in this file or `docs/DECISIONS.md` if significant.
- **Audit:** Periodically run `git log --all --name-only --pretty=format:''` and check for any blocklisted path; if found, fix history and update `.gitignore`/this section.
- **History spot-check (Iteration 234, 2026-03-28):** `git log` over `.cursor/agent-transcripts/`, `.env` / `.env.local`, and `*.secrets.json` patterns showed **no** commits touching those paths in this repository. No history rewrite required.

### Committed MCP config (verification)

The repo’s **`.cursor/mcp.json`** must stay **token-free** (launcher only, e.g. `bash` + `.cursor/github-mcp.sh`). Tokens are supplied via global Cursor config, process env (`GITHUB_PERSONAL_ACCESS_TOKEN` / `GH_TOKEN`), `gh auth token`, or gitignored `.devcontainer/local-secrets/github_token` — see `.cursor/README.md`.
