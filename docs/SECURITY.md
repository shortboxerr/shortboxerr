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
| **@typescript-eslint/no-explicit-any** | **Required** for new/changed API shapes: explicit request/response interfaces in `api/client.ts` (see Iteration 237), not `any`. **Narrow exceptions:** third-party typings you do not control, or a single escape hatch for truly dynamic JSON with a short comment and follow-up issue. Backend DTOs must never expose secrets (see API Responses above). |
| **react-hooks/static-components** | Inline component definitions; affects correctness/performance, not credentials or XSS. **Accepted.** |

When adding or changing accepted warnings in `eslint.config.js`, re-check that the pattern does not weaken credential handling, logging, or injection defenses, and update this section if needed.

### Build output (`wwwroot` / Vite `dist`)

- Treat published UI assets under `wwwroot` (or `ui/dist`) as **build artifacts**, not a source of truth for secrets.
- Do **not** inject API keys, tokens, or passwords via Vite `define`, env files, or CI variables into the client bundle. The app loads credentials from the API after the user configures settings; the SPA must not embed runtime secrets.
- When reviewing build or Docker changes, confirm nothing copies `.env` or secret files into the image or static output.

## NuGet supply chain

- **Pinned transitive packages:** `Directory.Build.props` at the repo root adds direct references so known-vulnerable transitive versions (e.g. `Microsoft.Extensions.Caching.Memory`, `System.Text.Json`, `System.Text.Encodings.Web`) resolve to patched releases. The test project also pins legacy `System.Net.Http` / `System.Text.RegularExpressions` where the graph still pulled 4.3.0.
- **Framework alignment:** Application packages (`Microsoft.AspNetCore.*`, `Microsoft.EntityFrameworkCore.*`, etc.) should stay on a consistent **8.0.x** line (patch bumps together when upgrading). Individual `Microsoft.Extensions.*` packages do not always publish every patch number; use NuGet version lists rather than assuming `8.0.11` exists for all extension packages.
- **Verification:** `dotnet restore` / `dotnet build` enforce **NuGet Audit** (high/critical transitive and direct) via root `Directory.Build.props`; failures surface as `NU1901`–`NU1904`. Optionally run `dotnet list package --vulnerable --include-transitive` locally for a readable report (exit code is not relied on in CI).

## E2E test dependencies

- `tests/e2e/` has its own `package.json` and `node_modules`. It is **not** shipped in release images; keep it that way.
- Periodically run `npm audit` in `tests/e2e/` (e.g. when touching Playwright or adding e2e deps).

## Lightweight threat model (data flow and deployment)

Short, informal model for operators and reviewers (not a formal STRIDE exercise). **Update this section** when authentication, storage, or deployment assumptions change.

### Trust boundaries

| Zone | Role |
|------|------|
| **Operator browser** | Loads the SPA from the app host; talks to `/api/*` via the UI. Must not persist indexer/service credentials in browser storage (see Frontend Security). |
| **Application host** | Runs Shortboxerr (Kestrel or container). Holds SQLite DB, log files, cover cache, and decrypted credentials **in process memory** while handling requests. |
| **SQLite database** | Stores settings; sensitive fields use **encryption at rest** (`ENC:1:`). A copied DB file is not enough to recover secrets on another machine (machine-bound key). |
| **External services** | ComicVine, Metron, indexers, download clients, etc. Outbound calls use credentials the user configured; treat those providers as separate trust zones. |

### API access control

- **`/api/*`** (except documented exemptions such as `/health`, `/ping`, `/swagger`, `/signalr`, `/api/v1/setup`) can require an **API key** when the operator enables API key authentication in settings (see `ApiKeyMiddleware`).
  - **Preferred:** send the key with the **`X-Api-Key`** header.
  - **Legacy compatibility only:** `apikey` query parameter. Do **not** use query strings for new clients—URLs appear in logs, proxies, browser history, and monitoring. Do not put API keys in `localStorage` or bookmarkable links; prefer the header (or HTTPS request body where the API allows it).
  - When auth is **disabled**, `/api/*` is open to anyone who can reach the host (**LAN-wide exposure risk**).
- **Static UI** (`/`, assets under `wwwroot`) is served without API key checks; the SPA obtains data by calling `/api/*` (with key when enabled).

### Transport and exposure

- **TLS:** Terminate HTTPS at a **reverse proxy** (nginx, Traefik, Caddy, cloud LB) for any deployment reachable beyond a single trusted machine. Plain HTTP is only appropriate on loopback or an isolated management network.
- **Network:** Prefer binding to localhost or a private interface unless the deployment model requires LAN access; combine with firewall rules and optional API key auth.

### Automated checks (CI)

| Job / step | Purpose |
|------------|---------|
| **NuGet Audit** (restore/build, via `Directory.Build.props`) | Treats high/critical advisory matches as **errors** (`NU1901`–`NU1904`), including transitive packages. |
| **`npm audit`** (`ui/`, `tests/e2e`, `--audit-level=high`) | Fails on high/critical npm advisories for those trees. |
| **`npm run lint`** (UI only, `--max-warnings 0`) | Blocks new ESLint warnings (includes hooks/refresh rules relevant to safe React patterns). |
| **Gitleaks** (full history) | Detects accidentally committed secrets. For repos under a **GitHub Organization**, the action requires a **`GITLEAKS_LICENSE`** GitHub Actions secret (free [Starter](https://gitleaks.io/products.html) tier covers one repo); the workflow passes it as `env.GITLEAKS_LICENSE`. |
| **OSV-Scanner** (`ui/package-lock.json`, `tests/e2e/package-lock.json`) | Second opinion vs [OSV.dev](https://osv.dev/) for locked npm dependencies (complements `npm audit`). |
| **Docker build** (after above) | Ensures the release image still builds; image runs the API as a **non-root** user (see below). |
| **Record merged PR** (on PR merge to `main`) | Appends [`docs/AUTO_MERGE_LOG.md`](./AUTO_MERGE_LOG.md) via a short-lived PR (`merge-log/*` branch). Uses `GITHUB_TOKEN` for branch push; optional secret **`AUTO_MERGE_LOG_PAT`** (fine-grained PAT) to open/merge that PR when Actions cannot create PRs. See **`docs/CONTRIBUTING.md`**. |

Workflow: `.github/workflows/ci.yml` and `.github/workflows/record-merged-pr.yml`. Contributor expectations: `docs/CONTRIBUTING.md`. **Vulnerability disclosure:** `.github/SECURITY.md`.

**Semgrep** or other custom static analysis remains optional if you want additional rules beyond the above.

### GitHub Actions secrets and future release workflows

Applies when adding or changing workflows that publish releases, images, or packages (**EPIC 22**).

- **Least privilege:** Prefer the default **`GITHUB_TOKEN`** with the narrowest workflow **permissions** (`permissions:` block). Use a **fine-grained PAT** or **GitHub App** only when `GITHUB_TOKEN` cannot perform the required API calls; scope to the single repo (or fewer) and minimum permissions.
- **Storage:** Define secrets in **GitHub Actions secrets** or **environments** (use [environments](https://docs.github.com/en/actions/deployment/targeting-different-environments/using-environments-for-deployment) with required reviewers for production release jobs when appropriate).
- **Logs and artifacts:** Do not print secret values or base64-encoded credentials in `run:` scripts. Avoid uploading untrusted build logs that might contain env dumps. (Routine CI today avoids echoing secrets; extend the same discipline to release jobs.)
- **Rotation:** Rotate PATs when maintainers leave, after suspected leak, or on a regular calendar. Revoke old tokens in GitHub **Settings → Developer settings** (or org **Personal access tokens**).
- **NuGet/npm coverage:** .NET packages are enforced via **NuGet Audit** on restore/build; npm via **`npm audit`** and **OSV-Scanner** on lockfiles. Keep lockfiles committed so scans stay deterministic.

### Docker image

The **`Dockerfile`** builds a **multi-stage** image: only `src/Shortboxerr.Api` (and dependencies) is published; **tests and e2e** trees are not copied into the image. Runtime stage uses **`mcr.microsoft.com/dotnet/aspnet:8.0`**, runs as user **`shortboxerr`** (non-root), and does **not** bake in `.env`, user secrets, or dev-only paths. Build-time secrets must not be passed as `ARG`/`ENV` for layers that ship to registry; use runtime env or orchestrator secrets for production configuration.

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
- [ ] New dependencies: no new high/critical NuGet advisories without mitigation (restore/build must succeed with NuGet Audit enabled)
- [ ] Frontend build does not embed secrets in `wwwroot` bundles
- [ ] Undisclosed security issues are **not** discussed in public issues before fix (use `.github/SECURITY.md`)

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
- **History spot-check (how to verify):** From the repo root, inspect whether blocklisted paths ever entered history, and record any finding in `docs/DECISIONS.md` or the worklog rather than asserting a one-time pass in this file. Example commands:
  - `git log --all --oneline -- .cursor/agent-transcripts/`
  - `git log --all --oneline -- .env .env.local ':(glob)*.secrets.json'`

### Committed MCP config (verification)

The repo’s **`.cursor/mcp.json`** must stay **token-free** (launcher only, e.g. `bash` + `.cursor/github-mcp.sh`). Tokens are supplied via global Cursor config, process env (`GITHUB_PERSONAL_ACCESS_TOKEN` / `GH_TOKEN`), `gh auth token`, or gitignored `.devcontainer/local-secrets/github_token` — see `.cursor/README.md`.
