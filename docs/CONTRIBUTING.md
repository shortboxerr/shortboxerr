# Contributing

## Branching and pull requests

- **Default branch:** `main` (integration and releases).
- **Work in feature branches** branched from `main` (e.g. `feat/…`, `fix/…`, `chore/…`).
- **Open pull requests into `main`**, not direct pushes to `main` when branch protection is enabled.
- The **`dev` branch** may be used temporarily for compatibility or batch integration; prefer short-lived branches + PRs for new work.

## Commits

- Use [Conventional Commits](https://www.conventionalcommits.org/) (`feat:`, `fix:`, `chore:`, `docs:`, etc.) to keep history and future SemVer automation consistent.

## Quality gates

Before opening a PR:

- `dotnet build`
- `dotnet test`
- `cd ui && npm run lint` (zero warnings enforced)
- `cd ui && npm run build`

Run commands inside the [development container](https://github.com/shortboxerr/shortboxerr/blob/main/README.md#development-dev-container) when applicable.

## Security

- Do not commit secrets, API keys, or local AI tool state. Authoritative blocklist and policy: [SECURITY.md](./SECURITY.md) (section *AI and Dev Tooling: Do Not Commit*). Ignored patterns: root `.gitignore`.
- **Cursor / MCP:** Keep `.cursor/mcp.json` free of tokens; use `~/.cursor/mcp.json` or env for PATs. See [.cursor/README.md](../.cursor/README.md).
- **Dev container:** Never commit `.devcontainer/local-secrets/` (gitignored); used only for optional local GitHub token file.
- CI runs `npm audit` (UI and E2E), `dotnet list package --vulnerable`, and **Gitleaks** on full history; fix findings before merging.
