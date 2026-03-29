# Security policy

## Supported versions

Security fixes are applied on **`main`** via pull request. Use the latest tagged release or `main` when deploying.

## Reporting a vulnerability

**Do not** open a **public** issue for undisclosed security vulnerabilities (exploitable bugs, credential leaks, remote code execution, etc.).

Preferred options:

1. **[Private vulnerability reporting](https://docs.github.com/en/code-security/security-advisories/guidance-on-reporting-and-writing-information-about-vulnerabilities/privately-reporting-a-security-vulnerability)** — if enabled for this repo, use **Security → Report a vulnerability**.
2. **GitHub Security Advisories** — maintainers may draft an advisory; reporters with appropriate access can coordinate there.
3. **Maintainer contact** — use the contact method shown on the organization or maintainer profile if listed.

Please include:

- Short description of the issue and suspected impact
- Affected area (API, UI, Docker image, dependencies, etc.)
- Steps to reproduce, if safe to share
- Whether you believe the issue is already exploitable in default configurations

We aim to acknowledge valid reports in a reasonable timeframe. This project is maintained on a best-effort basis.

## Security architecture and coding standards

See **[docs/SECURITY.md](../docs/SECURITY.md)** in this repository for credential handling, logging, API behavior, CI security checks, threat model notes, and review checklists.

## Dependency and CI posture

Automated checks on push/PR include vulnerable NuGet detection, `npm audit` (high severity and above) for `ui/` and `tests/e2e`, ESLint with zero warnings for the UI, and Gitleaks on git history. See [`.github/workflows/ci.yml`](workflows/ci.yml).
