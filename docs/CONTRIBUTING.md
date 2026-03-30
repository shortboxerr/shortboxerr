# Contributing

## Branching and pull requests

- **Default branch:** `main` (integration and releases).
- **Work in feature branches** branched from `main` (e.g. `feat/…`, `fix/…`, `chore/…`).
- **Open pull requests into `main`**, not direct pushes to `main` when branch protection is enabled.
- **Do not use a long-lived `integration` branch:** all changes land via PR to `main`. The historical `dev` branch is retired (remove your local copy after `main` is updated: `git branch -d dev`).

### Typical workflow (merge to `main`)

`main` is **protected**: integrate only through a **pull request** with required status checks passing.

1. `git fetch origin && git checkout main && git pull origin main`
2. `git checkout -b chore/your-topic` (or `feat/…`, `fix/…`)
3. Implement and commit until the slice is complete.
4. Run [quality gates](#quality-gates) (or CI-equivalent in the dev container).
5. `git push -u origin chore/your-topic`
6. Open **one** PR **into `main`** (UI or `gh pr create --base main`).
7. Wait for CI; fix failures; address review comments (including CodeRabbit) as appropriate. Merge when green.
8. Locally: `git checkout main && git pull origin main`, then `git branch -d chore/your-topic`.

**Merge history:** `git log --oneline` and GitHub's PR list are the source of truth. No supplemental logs are maintained.

## Commits

- Use [Conventional Commits](https://www.conventionalcommits.org/) (`feat:`, `fix:`, `chore:`, `docs:`, etc.) to keep history and future SemVer automation consistent.

## Quality gates

Before opening a PR:

- `dotnet build`
- `dotnet test`
- `cd ui && npm run lint` (zero warnings enforced)
- `cd ui && npm run build`

Run commands inside the [development container](https://github.com/shortboxerr/shortboxerr/blob/main/README.md#development-dev-container) when applicable.

## Automated PR review (CodeRabbit)

This repository uses [CodeRabbit](https://coderabbit.ai/) (GitHub App) for AI-assisted pull request summaries and review comments. For **public** repositories, typical usage is covered by the free offering; install the app from [coderabbitai on GitHub](https://github.com/apps/coderabbitai) on the organization or repository.

- **Repository configuration:** [`.coderabbit.yaml`](../.coderabbit.yaml) at the repo root controls review profile, path filters, and auto-review behavior.
- **Adjusting behavior:** Change `.coderabbit.yaml` in a branch and merge via PR. App installation, permissions, and which repositories are enabled are managed in GitHub under **Settings → Integrations → Applications** (or the org equivalent).

## Security

- **Report vulnerabilities:** use private channels — see [`.github/SECURITY.md`](../.github/SECURITY.md) (do not use public issues for undisclosed security bugs).
- Do not commit secrets, API keys, or local AI tool state. Authoritative blocklist and policy: [SECURITY.md](./SECURITY.md) (section *AI and Dev Tooling: Do Not Commit*). Ignored patterns: root `.gitignore`.
- **Cursor / MCP:** Keep `.cursor/mcp.json` free of tokens; use `~/.cursor/mcp.json` or env for PATs. See [.cursor/README.md](../.cursor/README.md).
- **Dev container:** Never commit `.devcontainer/local-secrets/` (gitignored); used only for optional local GitHub token file.
- CI runs `npm audit` (UI and E2E), **OSV-Scanner** on both `package-lock.json` files, **NuGet Audit** (fails restore/build on high/critical advisories), and **Gitleaks** on full history; fix findings before merging.
